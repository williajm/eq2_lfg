using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using Eq2Lfg.Core.Census;
using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Roster;
using Eq2Lfg.Core.Watching;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.App.Services;

public sealed record MonitorStatus(
    string? LogFile,
    string? ActiveCharacter,
    int RosterCount,
    int CensusRefreshed,
    DateTimeOffset? LastCensusRefresh);

/// <summary>
/// The app's engine room: tails the active log on a UI-thread timer, runs every chat
/// line through the analyzer, and raises events for traffic, matches, and opportunities.
/// </summary>
public sealed class MonitorService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RelocateInterval = TimeSpan.FromSeconds(10);

    private readonly AppSettings settings;
    private readonly DispatcherTimer timer;
    private readonly HttpClient httpClient = new();
    private readonly Dictionary<string, DateTimeOffset> opportunityAlerts = [];

    private CharacterDataService dataService;
    private LfgMessageAnalyzer analyzer;
    private MatchEngine matchEngine;
    private CooldownTracker adCooldown;
    private LogTailer? tailer;
    private ActiveLog? activeLog;
    private DateTimeOffset lastRelocate = DateTimeOffset.MinValue;
    private DateTimeOffset lastCensusRefresh = DateTimeOffset.MinValue;
    private bool censusRefreshRunning;
    private int censusRefreshedCount;
    private DateTimeOffset? censusRefreshedAt;

    // Snapshot of the settings the live components were built from, so ApplySettings
    // only rebuilds (and loses state in) components whose configuration changed.
    private int appliedCooldownMinutes;
    private string appliedServiceId;
    private string appliedDirectory;
    private (int Players, int Archetypes, int Spread, int Window, bool Mentor) appliedOpportunity;

    public ZoneTable Zones { get; }
    public IReadOnlyList<GameCharacter> Roster { get; private set; } = [];
    public GroupOpportunityDetector OpportunityDetector { get; private set; }
    public MonitorStatus Status { get; private set; } = new(null, null, 0, 0, null);

    public event Action<LfgPost>? TrafficSeen;
    public event Action<LfgPost, IReadOnlyList<MatchResult>, bool>? MatchFound;
    public event Action<GroupOpportunity>? OpportunityFound;

    /// <summary>Raised when opportunity state is discarded (e.g. the log switched servers).</summary>
    public event Action? OpportunitiesReset;

    public event Action<MonitorStatus>? StatusChanged;

    public MonitorService(AppSettings settings)
    {
        this.settings = settings;
        Zones = ZoneTable.LoadOrSeed(AppSettings.DefaultZonesPath);
        analyzer = new LfgMessageAnalyzer(Zones);
        matchEngine = CreateMatchEngine();
        OpportunityDetector = CreateOpportunityDetector();
        adCooldown = new CooldownTracker(TimeSpan.FromMinutes(settings.CooldownMinutes));
        dataService = CreateDataService();
        appliedCooldownMinutes = settings.CooldownMinutes;
        appliedServiceId = settings.CensusServiceId;
        appliedDirectory = settings.Eq2Directory;
        appliedOpportunity = OpportunitySnapshot();
        timer = new DispatcherTimer { Interval = PollInterval };
        timer.Tick += (_, _) => Poll();
    }

    private MatchEngine CreateMatchEngine() =>
        new(new MatchOptions
        {
            LevelTolerance = settings.LevelTolerance,
            AllowMentorDown = settings.AllowMentorDown,
        });

    private GroupOpportunityDetector CreateOpportunityDetector() =>
        new(new GroupOpportunityOptions
        {
            MinPlayers = settings.OpportunityMinPlayers,
            MinArchetypes = settings.OpportunityMinArchetypes,
            LevelSpread = settings.OpportunityLevelSpread,
            Window = TimeSpan.FromMinutes(settings.OpportunityWindowMinutes),
            AllowMentorDown = settings.AllowMentorDown,
        });

    private CharacterDataService CreateDataService()
    {
        var serviceId = NullIfEmpty(settings.CensusServiceId);
        return new CharacterDataService(
            new CensusClient(httpClient, serviceId), AppSettings.DefaultCachePath)
        {
            // Anonymous Census access allows roughly 10 requests/minute; a registered
            // service ID lifts the limit and can be queried much faster.
            RequestSpacing = serviceId is null
                ? TimeSpan.FromSeconds(6.5)
                : TimeSpan.FromSeconds(1),
        };
    }

    private (int, int, int, int, bool) OpportunitySnapshot() =>
        (settings.OpportunityMinPlayers, settings.OpportunityMinArchetypes,
            settings.OpportunityLevelSpread, settings.OpportunityWindowMinutes,
            settings.AllowMentorDown);

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public void Start()
    {
        Roster = RosterLoader.Load(settings.Eq2Directory);
        PublishStatus();
        _ = RefreshCensusAsync();
        timer.Start();
    }

    /// <summary>
    /// Re-read tunables after the user changes settings. Stateful components (cooldowns,
    /// observed player posts, census client) are only rebuilt when their own configuration
    /// actually changed, so unrelated edits don't clear history or re-enable alerts.
    /// </summary>
    public void ApplySettings()
    {
        matchEngine = CreateMatchEngine();

        if (settings.CooldownMinutes != appliedCooldownMinutes)
        {
            appliedCooldownMinutes = settings.CooldownMinutes;
            adCooldown = new CooldownTracker(TimeSpan.FromMinutes(settings.CooldownMinutes));
        }

        if (OpportunitySnapshot() != appliedOpportunity)
        {
            appliedOpportunity = OpportunitySnapshot();
            var replacement = CreateOpportunityDetector();
            foreach (var post in OpportunityDetector.ActivePosts(DateTimeOffset.UtcNow))
            {
                replacement.Observe(post);
            }

            OpportunityDetector = replacement;
        }

        if (!string.Equals(settings.CensusServiceId, appliedServiceId, StringComparison.Ordinal))
        {
            appliedServiceId = settings.CensusServiceId;
            dataService = CreateDataService();
            lastCensusRefresh = DateTimeOffset.MinValue;
        }

        if (!string.Equals(settings.Eq2Directory, appliedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            appliedDirectory = settings.Eq2Directory;
            Roster = RosterLoader.Load(settings.Eq2Directory);
            activeLog = null;
            tailer = null;
            lastRelocate = DateTimeOffset.MinValue;
            lastCensusRefresh = DateTimeOffset.MinValue;
            PublishStatus();
        }
    }

    /// <summary>Re-create the analyzer after the zone table is edited.</summary>
    public void ApplyZoneChanges()
    {
        Zones.Save(AppSettings.DefaultZonesPath);
        analyzer = new LfgMessageAnalyzer(Zones);
    }

    /// <summary>
    /// Characters eligible for matching: enabled and on the active log's server —
    /// chat channels are per-server, so characters elsewhere can't join anyway.
    /// </summary>
    public IEnumerable<GameCharacter> EnabledCharacters() =>
        RosterFilter.Eligible(Roster, settings, activeLog?.Server);

    private void Poll()
    {
        var now = DateTimeOffset.UtcNow;

        if (tailer is null || now - lastRelocate > RelocateInterval)
        {
            lastRelocate = now;
            Relocate();
        }

        if (now - lastCensusRefresh > TimeSpan.FromMinutes(Math.Max(5, settings.CensusRefreshMinutes)))
        {
            _ = RefreshCensusAsync();
        }

        if (tailer is null)
        {
            return;
        }

        foreach (var line in tailer.ReadNewLines())
        {
            HandleLine(line);
        }
    }

    private void Relocate()
    {
        var found = LogFileLocator.FindMostRecent(settings.Eq2Directory);
        if (found is null)
        {
            if (activeLog is not null)
            {
                activeLog = null;
                tailer = null;
                PublishStatus();
            }

            return;
        }

        if (!string.Equals(found.FilePath, activeLog?.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            // LFG chat is per-server: posts observed on the old server can't form
            // groups with the new one, so crossing servers resets opportunity state.
            if (activeLog is not null
                && !string.Equals(found.Server, activeLog.Server, StringComparison.OrdinalIgnoreCase))
            {
                OpportunityDetector.Clear();
                opportunityAlerts.Clear();
                OpportunitiesReset?.Invoke();
            }

            activeLog = found;
            tailer = new LogTailer(found.FilePath);
            PublishStatus();
        }
    }

    private void HandleLine(string line)
    {
        if (LevelUpDetector.DetectNewLevel(line) is { } newLevel)
        {
            var active = ActiveCharacter();
            if (active is not null)
            {
                dataService.ApplyLevelUp(active, newLevel);
                PublishStatus();
            }
        }

        if (ChatLogLineParser.Parse(line) is not { } message)
        {
            return;
        }

        var post = analyzer.Analyze(message);
        switch (post.Kind)
        {
            case PostKind.GroupAd:
                TrafficSeen?.Invoke(post);
                HandleGroupAd(post);
                break;
            case PostKind.PlayerLfg:
                TrafficSeen?.Invoke(post);
                HandlePlayerPost(post);
                break;
            case PostKind.Spam:
            case PostKind.NotLfg:
                break;
        }
    }

    private void HandleGroupAd(LfgPost post)
    {
        var matches = matchEngine.FindMatches(post, EnabledCharacters());
        if (matches.Count == 0)
        {
            return;
        }

        var shouldAlert = adCooldown.ShouldAlert(post, DateTimeOffset.UtcNow);
        MatchFound?.Invoke(post, matches, shouldAlert);
    }

    private void HandlePlayerPost(LfgPost post)
    {
        OpportunityDetector.Observe(post);
        var opportunity = OpportunityDetector.Evaluate(DateTimeOffset.UtcNow, EnabledCharacters());
        if (opportunity is null)
        {
            return;
        }

        // Per-cluster cooldown: each distinct set of advertisers has its own window,
        // so alternating best-clusters can't bypass suppression.
        var now = DateTimeOffset.UtcNow;
        var cooldown = TimeSpan.FromMinutes(settings.CooldownMinutes);
        if (opportunityAlerts.TryGetValue(opportunity.Signature, out var lastAlert)
            && now - lastAlert < cooldown)
        {
            return;
        }

        opportunityAlerts[opportunity.Signature] = now;
        PruneOpportunityAlerts(now, cooldown);
        OpportunityFound?.Invoke(opportunity);
    }

    private void PruneOpportunityAlerts(DateTimeOffset now, TimeSpan cooldown)
    {
        foreach (var expired in opportunityAlerts
                     .Where(kv => now - kv.Value > cooldown)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            opportunityAlerts.Remove(expired);
        }
    }

    private GameCharacter? ActiveCharacter() =>
        activeLog is null
            ? null
            : Roster.FirstOrDefault(c =>
                c.Name.Equals(activeLog.CharacterName, StringComparison.OrdinalIgnoreCase)
                && c.Server.Equals(activeLog.Server, StringComparison.OrdinalIgnoreCase));

    private async Task RefreshCensusAsync()
    {
        if (censusRefreshRunning)
        {
            return;
        }

        censusRefreshRunning = true;
        lastCensusRefresh = DateTimeOffset.UtcNow;
        try
        {
            censusRefreshedCount = await dataService.PopulateAsync(
                Roster,
                settings.Eq2Directory,
                TimeSpan.FromMinutes(Math.Max(5, settings.CensusRefreshMinutes)));
            censusRefreshedAt = DateTimeOffset.UtcNow;
            PublishStatus();
        }
        catch (IOException)
        {
            // Cache write issues must not kill the monitor.
        }
        finally
        {
            censusRefreshRunning = false;
        }
    }

    private void PublishStatus()
    {
        Status = new MonitorStatus(
            activeLog is null ? null : Path.GetFileName(activeLog.FilePath),
            ActiveCharacter()?.Display ?? activeLog?.CharacterName,
            Roster.Count,
            censusRefreshedCount,
            censusRefreshedAt);
        StatusChanged?.Invoke(Status);
    }

    public void Dispose()
    {
        timer.Stop();
        httpClient.Dispose();
    }
}

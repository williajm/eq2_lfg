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
    private readonly CharacterDataService dataService;
    private readonly DispatcherTimer timer;
    private readonly HttpClient httpClient = new();

    private LfgMessageAnalyzer analyzer;
    private MatchEngine matchEngine;
    private CooldownTracker adCooldown;
    private LogTailer? tailer;
    private ActiveLog? activeLog;
    private DateTimeOffset lastRelocate = DateTimeOffset.MinValue;
    private DateTimeOffset lastCensusRefresh = DateTimeOffset.MinValue;
    private string? lastOpportunitySignature;
    private DateTimeOffset lastOpportunityAlert = DateTimeOffset.MinValue;
    private bool censusRefreshRunning;
    private int censusRefreshedCount;
    private DateTimeOffset? censusRefreshedAt;

    public ZoneTable Zones { get; }
    public IReadOnlyList<GameCharacter> Roster { get; private set; } = [];
    public GroupOpportunityDetector OpportunityDetector { get; private set; }
    public MonitorStatus Status { get; private set; } = new(null, null, 0, 0, null);

    public event Action<LfgPost>? TrafficSeen;
    public event Action<LfgPost, IReadOnlyList<MatchResult>, bool>? MatchFound;
    public event Action<GroupOpportunity>? OpportunityFound;
    public event Action<MonitorStatus>? StatusChanged;

    public MonitorService(AppSettings settings)
    {
        this.settings = settings;
        Zones = ZoneTable.LoadOrSeed(AppSettings.DefaultZonesPath);
        analyzer = new LfgMessageAnalyzer(Zones);
        matchEngine = CreateMatchEngine();
        OpportunityDetector = CreateOpportunityDetector();
        adCooldown = new CooldownTracker(TimeSpan.FromMinutes(settings.CooldownMinutes));
        dataService = new CharacterDataService(
            new CensusClient(httpClient, NullIfEmpty(settings.CensusServiceId)),
            AppSettings.DefaultCachePath);
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

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public void Start()
    {
        Roster = RosterLoader.Load(settings.Eq2Directory);
        PublishStatus();
        _ = RefreshCensusAsync();
        timer.Start();
    }

    /// <summary>Re-read tunables after the user changes settings.</summary>
    public void ApplySettings()
    {
        matchEngine = CreateMatchEngine();
        adCooldown = new CooldownTracker(TimeSpan.FromMinutes(settings.CooldownMinutes));
        OpportunityDetector = CreateOpportunityDetector();
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

        var now = DateTimeOffset.UtcNow;
        var withinCooldown = now - lastOpportunityAlert < TimeSpan.FromMinutes(settings.CooldownMinutes);
        if (opportunity.Signature == lastOpportunitySignature && withinCooldown)
        {
            return;
        }

        lastOpportunitySignature = opportunity.Signature;
        lastOpportunityAlert = now;
        OpportunityFound?.Invoke(opportunity);
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

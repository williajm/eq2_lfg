using Eq2Lfg.Core.Census;
using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Roster;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

/// <summary>Scripted census fake: maps "name@world" to a lookup result.</summary>
internal sealed class FakeCensusClient : ICensusClient
{
    public Dictionary<string, CensusLookup> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int Calls { get; private set; }

    public Task<CensusLookup> LookupAsync(
        string name, string world, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(
            Results.TryGetValue($"{name}@{world}", out var result) ? result : CensusLookup.NotFound);
    }
}

public class CensusServiceTests : IDisposable
{
    private readonly string tempDir = Directory.CreateTempSubdirectory("eq2lfg-census").FullName;

    private string CachePath => Path.Combine(tempDir, "characters.json");

    public void Dispose()
    {
        Directory.Delete(tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static GameCharacter Char(string name, string server = "Wuoshi") =>
        new() { Account = "acct", Server = server, Name = name };

    private CharacterDataService Service(FakeCensusClient fake) =>
        new(fake, CachePath)
        {
            RequestSpacing = TimeSpan.Zero,
            RateLimitBackoff = TimeSpan.Zero,
        };

    [Fact]
    public async Task Found_characters_are_populated_and_cached()
    {
        var fake = new FakeCensusClient();
        fake.Results["Bramwick@Wuoshi"] = new CensusLookup(
            CensusLookupStatus.Found,
            new CensusCharacterInfo("Bramwick", "Wuoshi", "Warden", 59, "provisioner", 45));
        var character = Char("Bramwick");

        var refreshed = await Service(fake).PopulateAsync([character], tempDir, TimeSpan.FromHours(1));

        Assert.Equal(1, refreshed);
        Assert.Equal("Warden", character.Class);
        Assert.Equal(59, character.Level);
        Assert.Equal("census", character.DataSource);
        Assert.True(File.Exists(CachePath));
    }

    [Fact]
    public async Task Fresh_cache_entries_are_not_requeried()
    {
        var fake = new FakeCensusClient();
        fake.Results["Bramwick@Wuoshi"] = new CensusLookup(
            CensusLookupStatus.Found,
            new CensusCharacterInfo("Bramwick", "Wuoshi", "Warden", 59, null, null));
        var service = Service(fake);

        await service.PopulateAsync([Char("Bramwick")], tempDir, TimeSpan.FromHours(1));
        Assert.Equal(1, fake.Calls);

        var again = Char("Bramwick");
        var refreshed = await service.PopulateAsync([again], tempDir, TimeSpan.FromHours(1));

        Assert.Equal(0, refreshed);
        Assert.Equal(1, fake.Calls);
        Assert.Equal("Warden", again.Class);
        Assert.Equal("cache", again.DataSource);
    }

    [Fact]
    public async Task Missing_characters_are_remembered_not_rehammered()
    {
        var fake = new FakeCensusClient();
        var service = Service(fake);

        await service.PopulateAsync([Char("Ghost")], tempDir, TimeSpan.FromHours(1));
        await service.PopulateAsync([Char("Ghost")], tempDir, TimeSpan.FromHours(1));

        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Errors_fall_back_to_class_channel_hint()
    {
        File.WriteAllText(
            Path.Combine(tempDir, "Wuoshi_Bramwick_eq2_uisettings.xml"),
            """<UISettings><Channel index="4" name="Warden" /></UISettings>""");
        var fake = new FakeCensusClient();
        fake.Results["Bramwick@Wuoshi"] = CensusLookup.Error;
        var character = Char("Bramwick");

        var refreshed = await Service(fake).PopulateAsync([character], tempDir, TimeSpan.FromHours(1));

        Assert.Equal(0, refreshed);
        Assert.Equal("Warden", character.Class);
        Assert.Equal("channel-hint", character.DataSource);
    }

    [Fact]
    public void Roster_filter_scopes_to_active_server()
    {
        var settings = new AppSettings();
        var wuoshi = Char("Bramwick");
        var eu = Char("Lode", "Thurgadin");

        var eligible = RosterFilter.Eligible([wuoshi, eu], settings, "Wuoshi").ToList();

        Assert.Single(eligible);
        Assert.Equal("Bramwick", eligible[0].Name);

        // No active server known → no server restriction.
        Assert.Equal(2, RosterFilter.Eligible([wuoshi, eu], settings, null).Count());
    }

    [Fact]
    public void Opportunity_message_is_paste_ready()
    {
        var analyzer = new LfgMessageAnalyzer(ZoneTable.CreateSeeded());
        var detector = new GroupOpportunityDetector();
        var t0 = DateTimeOffset.FromUnixTimeSeconds(1784976000);
        foreach (var (text, speaker) in new[]
                 {
                     ("45 fury LFG", "Vex"), ("48 wizard LFG", "Dorn"), ("52 guardian lf group", "Sella"),
                 })
        {
            detector.Observe(analyzer.Analyze(new ChatMessage
            {
                Timestamp = t0,
                Speaker = speaker,
                Channel = "LFG",
                Text = text,
                Raw = text,
            }));
        }

        var warden = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Bramwick",
            Class = "Warden",
            Level = 50,
        };
        var opportunity = detector.Evaluate(t0.AddMinutes(1), [warden]);

        Assert.NotNull(opportunity);
        var message = OpportunityMessage.Compose(opportunity);
        Assert.Contains("Vex", message);
        Assert.Contains("Dorn", message);
        Assert.Contains("Sella", message);
        Assert.Contains("50 Warden (Bramwick)", message);
        Assert.Contains("form a group", message);
    }
}

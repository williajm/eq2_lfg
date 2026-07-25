using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

public class CooldownAndZoneTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(1784976000);

    private static LfgPost Ad(string text, int minutesAfterT0 = 0, string speaker = "Brakor")
    {
        var analyzer = new LfgMessageAnalyzer(ZoneTable.CreateSeeded());
        return analyzer.Analyze(new ChatMessage
        {
            Timestamp = T0.AddMinutes(minutesAfterT0),
            Speaker = speaker,
            Channel = "LFG",
            Text = text,
            Raw = text,
        });
    }

    [Fact]
    public void Repeat_ad_within_cooldown_is_suppressed()
    {
        var tracker = new CooldownTracker(TimeSpan.FromMinutes(15));

        Assert.True(tracker.ShouldAlert(Ad("need tank cmm")));
        Assert.False(tracker.ShouldAlert(Ad("need tank cmm", 2)));
        Assert.True(tracker.ShouldAlert(Ad("need tank cmm", 20)));
    }

    [Fact]
    public void Materially_changed_ad_alerts_again()
    {
        var tracker = new CooldownTracker(TimeSpan.FromMinutes(15));

        Assert.True(tracker.ShouldAlert(Ad("need tank 4 dps cmm")));
        // Same zone but the wanted roles changed → new alert.
        Assert.True(tracker.ShouldAlert(Ad("need healer cmm", 3)));
    }

    [Fact]
    public void Different_advertisers_do_not_share_cooldown()
    {
        var tracker = new CooldownTracker(TimeSpan.FromMinutes(15));

        Assert.True(tracker.ShouldAlert(Ad("need tank cmm", 0, "Brakor")));
        Assert.True(tracker.ShouldAlert(Ad("need tank cmm", 1, "Melia")));
    }

    [Fact]
    public void Zone_table_seeds_file_and_reloads_edits()
    {
        var dir = Directory.CreateTempSubdirectory("eq2lfg-zones").FullName;
        try
        {
            var path = Path.Combine(dir, "zones.json");

            var seeded = ZoneTable.LoadOrSeed(path);
            Assert.True(File.Exists(path));
            Assert.Equal("Castle Mistmoore", seeded.Resolve("cmm")!.Name);

            seeded.Replace(
            [
                new ZoneEntry
                {
                    Name = "Test Zone", MinLevel = 1, MaxLevel = 10, Abbreviations = ["TZ"],
                },
            ]);
            seeded.Save(path);

            var reloaded = ZoneTable.LoadOrSeed(path);
            Assert.Null(reloaded.Resolve("cmm"));
            Assert.Equal("Test Zone", reloaded.Resolve("tz")!.Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Zone_lookup_matches_whole_words_only()
    {
        var table = ZoneTable.CreateSeeded();

        // "nest" must not fire inside "honestly"; "ck" not inside "back".
        Assert.Null(table.FindInText("honestly no idea, come back later"));
        Assert.Equal("The Nest of the Great Egg", table.FindInText("LFM Nest grp")!.Name);
    }

    [Fact]
    public void Level_up_lines_are_detected()
    {
        Assert.Equal(
            60,
            LevelUpDetector.DetectNewLevel(
                "(1784976999)[Sat Jul 25 11:56:39 2026] You have gained a level! You are now level 60!"));
        Assert.Null(LevelUpDetector.DetectNewLevel("(1784976999)[...] You gained experience."));
    }
}

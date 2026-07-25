using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

public class MatchEngineTests
{
    private readonly LfgMessageAnalyzer analyzer = new(ZoneTable.CreateSeeded());
    private readonly MatchEngine engine = new();

    private static GameCharacter Bramwick => new()
    {
        Account = "testacct",
        Server = "Wuoshi",
        Name = "Bramwick",
        Class = "Warden",
        Level = 59,
    };

    private static GameCharacter Nobwick => new()
    {
        Account = "testacct",
        Server = "Wuoshi",
        Name = "Nobwick",
        Class = "Conjuror",
        Level = 62,
    };

    private LfgPost Ad(string text) => analyzer.Analyze(new ChatMessage
    {
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(1784976352),
        Speaker = "Brakor",
        Channel = "LFG",
        Text = text,
        Raw = text,
    });

    [Fact]
    public void Healer_matches_need_healer_in_zone_within_tolerance()
    {
        // Bramwick is 59; CMM is 60-70; default tolerance 5 → matches.
        var matches = engine.FindMatches(Ad("need healer CMM"), [Bramwick]);

        var match = Assert.Single(matches);
        Assert.Equal("Bramwick", match.Character.Name);
        Assert.Contains("healer", match.Reasons);
        Assert.Contains("Castle Mistmoore 60-70", match.Reasons);
    }

    [Fact]
    public void Wrong_role_does_not_match()
    {
        var matches = engine.FindMatches(Ad("need tank CMM"), [Bramwick]);
        Assert.Empty(matches);
    }

    [Fact]
    public void Level_too_far_below_zone_band_does_not_match()
    {
        var lowWarden = Bramwick;
        lowWarden.Level = 40;

        var matches = engine.FindMatches(Ad("need healer CMM"), [lowWarden]);
        Assert.Empty(matches);
    }

    [Fact]
    public void Explicit_class_request_beats_role_words()
    {
        var matches = engine.FindMatches(Ad("need warden for Nest"), [Bramwick, Nobwick]);

        var match = Assert.Single(matches);
        Assert.Equal("Bramwick", match.Character.Name);
        Assert.Contains("Warden wanted", match.Reasons);
    }

    [Fact]
    public void Multiple_characters_can_match_one_ad()
    {
        // CMM is 60-70; Bramwick 59 fits via tolerance, Nobwick 62 fits directly.
        var matches = engine.FindMatches(Ad("need healer and dps for CMM"), [Bramwick, Nobwick]);

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.Character.Name == "Bramwick");
        Assert.Contains(matches, m => m.Character.Name == "Nobwick");
    }

    [Fact]
    public void Player_posts_produce_no_matches()
    {
        var matches = engine.FindMatches(Ad("52 Warlock LF exp group"), [Bramwick]);
        Assert.Empty(matches);
    }

    [Fact]
    public void Stated_level_respects_tolerance()
    {
        var matches = engine.FindMatches(Ad("need healer for 55 grp"), [Bramwick]);
        var match = Assert.Single(matches);
        Assert.Contains("level 55±5", match.Reasons);

        var farAd = Ad("need healer for 40 grp");
        Assert.Empty(engine.FindMatches(farAd, [Bramwick]));
    }

    [Fact]
    public void Character_with_unknown_level_matches_on_role_alone()
    {
        var unknownLevel = Bramwick;
        unknownLevel.Level = null;

        var matches = engine.FindMatches(Ad("need healer CMM"), [unknownLevel]);
        Assert.Single(matches);
    }
}

using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

public class MentorAndRecruitmentTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(1784976000);

    private readonly LfgMessageAnalyzer analyzer = new(ZoneTable.CreateSeeded());

    private LfgPost Analyze(string text, string speaker = "Brakor", int minutesAfterT0 = 0) =>
        analyzer.Analyze(new ChatMessage
        {
            Timestamp = T0.AddMinutes(minutesAfterT0),
            Speaker = speaker,
            Channel = "LFG",
            Text = text,
            Raw = text,
        });

    [Theory]
    [InlineData("Velocity is lookin for Dirge/Healers/Exceptional DPS for raiding we are gonna raid Saturday/Sunday 7-10PM EST! Also accept all casuals/tradeskillers/backup raiders if any interest or questions shoot me a tell :)")]
    [InlineData("<Lucid Dreams> Seeking sk /monk | Full Time Position Raiders (Saturday 2-5pm EST) PST | Laid Back Atmosphere")]
    [InlineData("Our guild is recruiting healers and tanks, apply on discord")]
    public void Guild_recruitment_is_excluded(string text)
    {
        Assert.Equal(PostKind.Recruitment, Analyze(text).Kind);
    }

    [Fact]
    public void Group_ads_for_dungeons_are_not_recruitment()
    {
        Assert.Equal(PostKind.GroupAd, Analyze("NEED TANK 4 DPS CMM").Kind);
    }

    [Fact]
    public void Mentor_offer_is_parsed_with_floor()
    {
        var post = Analyze("70 zerker/ranger/conj/Fury LFG WILL MENTOR 40+");

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.True(post.WillMentor);
        Assert.Equal(40, post.MentorFloor);
        // 40 is the mentor floor, not a character level.
        Assert.Equal([70], post.StatedLevels);
    }

    [Fact]
    public void Higher_level_character_matches_via_mentoring()
    {
        var engine = new MatchEngine(new MatchOptions { LevelTolerance = 5, AllowMentorDown = true });
        var highWarden = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Bramwick",
            Class = "Warden",
            Level = 70,
        };

        // Stormhold is 15-30 — far below 70, but mentoring makes it possible.
        var matches = engine.FindMatches(Analyze("need healer SH"), [highWarden]);

        var match = Assert.Single(matches);
        Assert.Contains("can mentor down to Stormhold 15-30", match.Reasons);
    }

    [Fact]
    public void Mentor_down_matching_can_be_disabled()
    {
        var engine = new MatchEngine(new MatchOptions { LevelTolerance = 5, AllowMentorDown = false });
        var highWarden = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Bramwick",
            Class = "Warden",
            Level = 70,
        };

        Assert.Empty(engine.FindMatches(Analyze("need healer SH"), [highWarden]));
    }

    [Fact]
    public void Lower_level_character_still_cannot_match_upward()
    {
        var engine = new MatchEngine(new MatchOptions { LevelTolerance = 5, AllowMentorDown = true });
        var lowWarden = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Bramwick",
            Class = "Warden",
            Level = 30,
        };

        Assert.Empty(engine.FindMatches(Analyze("need healer CMM"), [lowWarden]));
    }

    [Fact]
    public void Mentoring_poster_joins_lower_level_cluster()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Analyze("45 guardian LFG", "Vex"));
        detector.Observe(Analyze("48 wizard LFG", "Dorn", 1));
        detector.Observe(Analyze("70 fury LFG WILL MENTOR 40+", "Sella", 2));

        var opportunity = detector.Evaluate(T0.AddMinutes(5), []);

        Assert.NotNull(opportunity);
        Assert.Equal(3, opportunity.Posts.Count);
        Assert.Contains(Role.Healer, opportunity.Archetypes);
    }

    [Fact]
    public void Mentor_floor_limits_how_far_down_a_poster_fits()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Analyze("15 guardian LFG", "Vex"));
        detector.Observe(Analyze("18 wizard LFG", "Dorn", 1));
        detector.Observe(Analyze("70 fury LFG WILL MENTOR 40+", "Sella", 2));

        // Sella won't mentor below 40, so no compatible trio exists.
        Assert.Null(detector.Evaluate(T0.AddMinutes(5), []));
    }
}

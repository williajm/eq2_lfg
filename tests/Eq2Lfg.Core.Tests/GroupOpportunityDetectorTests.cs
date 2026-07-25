using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

public class GroupOpportunityDetectorTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(1784976000);

    private readonly LfgMessageAnalyzer analyzer = new(ZoneTable.CreateSeeded());

    private LfgPost Post(string text, string speaker, int minutesAfterT0 = 0) =>
        analyzer.Analyze(new ChatMessage
        {
            Timestamp = T0.AddMinutes(minutesAfterT0),
            Speaker = speaker,
            Channel = "LFG",
            Text = text,
            Raw = text,
        });

    [Fact]
    public void Three_compatible_players_with_two_archetypes_form_an_opportunity()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("45 fury LFG", "Vex"));
        detector.Observe(Post("48 wizard LFG", "Dorn", 2));
        detector.Observe(Post("52 guardian lf group", "Sella", 4));

        var opportunity = detector.Evaluate(T0.AddMinutes(5), []);

        Assert.NotNull(opportunity);
        Assert.Equal(3, opportunity.Posts.Count);
        Assert.Equal(45, opportunity.MinLevel);
        Assert.Equal(52, opportunity.MaxLevel);
        Assert.Superset(
            new HashSet<Role> { Role.Healer, Role.Dps },
            opportunity.Archetypes.ToHashSet());
    }

    [Fact]
    public void Incompatible_levels_do_not_cluster()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("20 fury LFG", "Vex"));
        detector.Observe(Post("48 wizard LFG", "Dorn"));
        detector.Observe(Post("70 guardian lf group", "Sella"));

        Assert.Null(detector.Evaluate(T0.AddMinutes(5), []));
    }

    [Fact]
    public void Own_character_completes_a_two_player_cluster()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("55 wizard LFG", "Vex"));
        detector.Observe(Post("60 monk lf group", "Dorn"));

        var warden = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Bramwick",
            Class = "Warden",
            Level = 59,
        };

        var opportunity = detector.Evaluate(T0.AddMinutes(5), [warden]);

        Assert.NotNull(opportunity);
        Assert.Equal(2, opportunity.Posts.Count);
        Assert.Single(opportunity.OwnCandidates);
        Assert.Contains(Role.Healer, opportunity.Archetypes);
    }

    [Fact]
    public void Two_players_alone_are_not_enough()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("55 wizard LFG", "Vex"));
        detector.Observe(Post("60 monk lf group", "Dorn"));

        Assert.Null(detector.Evaluate(T0.AddMinutes(5), []));
    }

    [Fact]
    public void Single_archetype_is_not_enough()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("55 wizard LFG", "Vex"));
        detector.Observe(Post("57 warlock LFG", "Dorn"));
        detector.Observe(Post("60 necro lf group", "Sella"));

        Assert.Null(detector.Evaluate(T0.AddMinutes(5), []));
    }

    [Fact]
    public void Old_posts_expire_from_the_window()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("45 fury LFG", "Vex"));
        detector.Observe(Post("48 wizard LFG", "Dorn"));
        detector.Observe(Post("52 guardian lf group", "Sella"));

        Assert.Null(detector.Evaluate(T0.AddMinutes(45), []));
    }

    [Fact]
    public void Multibox_post_clusters_via_any_of_its_levels()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("16 Dirge / 47 Conj / 70 Warden LFG", "Vex"));
        detector.Observe(Post("48 wizard LFG", "Dorn", 1));
        detector.Observe(Post("52 guardian lf group", "Sella", 2));

        var opportunity = detector.Evaluate(T0.AddMinutes(5), []);

        Assert.NotNull(opportunity);
        Assert.Equal(3, opportunity.Posts.Count);
        Assert.Equal(47, opportunity.MinLevel);
        Assert.Equal(52, opportunity.MaxLevel);
    }

    [Fact]
    public void Repost_by_same_advertiser_replaces_rather_than_duplicates()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Post("45 fury LFG", "Vex"));
        detector.Observe(Post("45 fury still LFG", "Vex", 5));
        detector.Observe(Post("48 wizard LFG", "Dorn", 6));

        Assert.Null(detector.Evaluate(T0.AddMinutes(7), []));
    }
}

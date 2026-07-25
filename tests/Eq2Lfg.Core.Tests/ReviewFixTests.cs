using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Watching;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

/// <summary>Regression tests for external code-review findings (fictional player names).</summary>
public class ReviewFixTests : IDisposable
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(1784980000);

    private readonly string tempDir = Directory.CreateTempSubdirectory("eq2lfg-review").FullName;
    private readonly LfgMessageAnalyzer analyzer = new(ZoneTable.CreateSeeded());

    public void Dispose()
    {
        Directory.Delete(tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private LfgPost Analyze(string text, string speaker = "Brakor", int minutesAfterT0 = 0) =>
        analyzer.Analyze(new ChatMessage
        {
            Timestamp = T0.AddMinutes(minutesAfterT0),
            Speaker = speaker,
            Channel = "LFG",
            Text = text,
            Raw = text,
        });

    [Fact]
    public void Tailer_holds_back_partial_lines_until_terminated()
    {
        var path = Path.Combine(tempDir, "eq2log_Partial.txt");
        File.WriteAllText(path, "");
        var tailer = new LogTailer(path);
        tailer.ReadNewLines();

        // The game flushes mid-line: nothing must be delivered yet.
        File.AppendAllText(path, "(123)[ts] Vex tells LFG (3), \"need hea");
        Assert.Empty(tailer.ReadNewLines());

        // The rest of the line arrives: one complete line, not two fragments.
        File.AppendAllText(path, "ler CMM\"\n");
        var line = Assert.Single(tailer.ReadNewLines());
        Assert.Equal("(123)[ts] Vex tells LFG (3), \"need healer CMM\"", line);
    }

    [Fact]
    public void Have_clauses_do_not_create_wanted_roles()
    {
        var post = Analyze("LFM healer for CMM, have tank");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(Role.Healer, post.WantedRoles);
        Assert.DoesNotContain(Role.Tank, post.WantedRoles);
    }

    [Fact]
    public void Guild_mention_alone_is_not_recruitment()
    {
        var post = Analyze("guild group needs healer for CMM");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(Role.Healer, post.WantedRoles);
    }

    [Fact]
    public void Stated_level_range_matches_across_the_whole_range()
    {
        var engine = new MatchEngine(new MatchOptions { LevelTolerance = 0, AllowMentorDown = false });
        var post = Analyze("need healer 40-50 grp");

        GameCharacter Warden(int level) => new()
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "W",
            Class = "Warden",
            Level = level,
        };

        Assert.Single(engine.FindMatches(post, [Warden(48)]));
        Assert.Single(engine.FindMatches(post, [Warden(40)]));
        Assert.Empty(engine.FindMatches(post, [Warden(55)]));
    }

    [Fact]
    public void Own_character_cannot_stretch_the_cluster_beyond_the_spread()
    {
        var detector = new GroupOpportunityDetector();
        detector.Observe(Analyze("50 wizard LFG", "Vex"));
        detector.Observe(Analyze("60 monk lf group", "Dorn", 1));

        // A level-40 healer cannot mentor upward into a 50-60 cluster.
        var lowHealer = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Low",
            Class = "Warden",
            Level = 40,
        };
        Assert.Null(detector.Evaluate(T0.AddMinutes(5), [lowHealer]));

        // A level-52 healer keeps the combined span inside the spread.
        var fittingHealer = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Fit",
            Class = "Warden",
            Level = 52,
        };
        var opportunity = detector.Evaluate(T0.AddMinutes(5), [fittingHealer]);
        Assert.NotNull(opportunity);
        Assert.Single(opportunity.OwnCandidates);
    }
}

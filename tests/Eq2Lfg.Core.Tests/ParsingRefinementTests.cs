using Eq2Lfg.Core.Discovery;
using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

/// <summary>Cases discovered from live LFG traffic (player names fictional throughout).</summary>
public class ParsingRefinementTests
{
    private readonly LfgMessageAnalyzer analyzer = new(ZoneTable.CreateSeeded());

    private LfgPost Analyze(string text) => analyzer.Analyze(new ChatMessage
    {
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(1784980000),
        Speaker = "Brakor",
        Channel = "LFG",
        Text = text,
        Raw = text,
    });

    [Theory]
    [InlineData("CT room for 3m")]
    [InlineData("One spot in CT, upper 30's/lower 40's")]
    [InlineData("LF healer and tank mistmoore catacombs")]
    [InlineData("Klak LF chanter/bard")]
    [InlineData("LFM catacombs")]
    [InlineData("SoF lfm need tank and dps 3/6 pst")]
    public void Room_spot_and_bare_lf_ads_are_group_ads(string text)
    {
        Assert.Equal(PostKind.GroupAd, Analyze(text).Kind);
    }

    [Fact]
    public void Bare_lf_without_role_or_class_is_not_an_ad()
    {
        Assert.Equal(PostKind.NotLfg, Analyze("lf my corpse in commonlands").Kind);
    }

    [Theory]
    [InlineData("need 2 tanks for tonights raid in mmis/fth pst")]
    [InlineData("seeking zerker/monk bruiser for mmis/fth pst")]
    [InlineData("CMM 1 spot chanter/bard")]
    [InlineData("WC group has 1 spot for anything")]
    [InlineData("any heals/dps for WC? 10+")]
    [InlineData("anyone for FG?")]
    [InlineData("Any bard reps for PoA?")]
    [InlineData("Giants exp group LFM!")]
    [InlineData("16+ exp group with 70 mentor lf +1")]
    public void Plural_roles_seeking_spot_and_exp_group_ads_are_group_ads(string text)
    {
        Assert.Equal(PostKind.GroupAd, Analyze(text).Kind);
    }

    [Fact]
    public void Plural_tanks_extracts_tank_role()
    {
        var post = Analyze("unrest Need tanks / healers / support / DPS");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(Role.Tank, post.WantedRoles);
        Assert.Contains(Role.Healer, post.WantedRoles);
    }

    [Fact]
    public void Healer_asking_if_groups_need_one_is_a_player_post()
    {
        var post = Analyze("Any RoV groups need a healer?");

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Contains(Role.Healer, post.WantedRoles);
    }

    [Fact]
    public void Guild_ads_without_angle_brackets_are_recruitment()
    {
        const string tavern = "The Magic Tavern is looking for more members.  We're a do what "
            + "you want whenever you want guild. If you breathe, we want you. Level 113 guild.";
        Assert.Equal(PostKind.Recruitment, Analyze(tavern).Kind);
    }

    [Fact]
    public void Apostrophe_contractions_do_not_match_zone_abbreviations()
    {
        var table = ZoneTable.CreateSeeded();

        // "We're" must not hit Runnyeye's "RE" abbreviation.
        Assert.Null(table.FindInText("We're a friendly bunch, honest"));
        Assert.Equal("Runnyeye", table.FindInText("LFM RE goblins")!.Name);
    }

    [Theory]
    [InlineData("lf tank mistmoore cata", "Mistmoore Catacombs")]
    [InlineData("any heals/dps for WC? 10+", "Wailing Caves")]
    [InlineData("anyone up for SS/PoF exp farm? 50+?", "Pillars of Flame")]
    [InlineData("seeking zerker/monk bruiser for mmis/fth pst", "Mistmoore's Inner Sanctum")]
    [InlineData("1 spot open for chanter for mayong", "Mistmoore's Inner Sanctum")]
    [InlineData("CoV LF heals DPS", "Crypt of Valdoon")]
    public void Newly_observed_zone_shorthand_resolves(string text, string zone)
    {
        Assert.Equal(zone, Analyze(text).ZoneName);
    }

    [Theory]
    [InlineData("LF sorc for CT", "Wizard", "Warlock")]
    [InlineData("need 2 sorcs for CT", "Wizard", "Warlock")]
    [InlineData("Klak LF1M chanter/bard pst", "Illusionist", "Dirge")]
    [InlineData("DFC clear needs a cleric, pst", "Templar", "Inquisitor")]
    public void Archetype_terms_expand_to_their_subclasses(string text, string first, string second)
    {
        var post = Analyze(text);

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(first, post.Classes);
        Assert.Contains(second, post.Classes);
    }

    [Fact]
    public void Non_leather_means_cleric_or_shaman_healer()
    {
        var post = Analyze("PoA LF tank +dps/non leather");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains("Templar", post.Classes);
        Assert.Contains("Mystic", post.Classes);
        Assert.DoesNotContain("Fury", post.Classes);
        Assert.DoesNotContain("Warden", post.Classes);

        var engine = new MatchEngine();
        GameCharacter Char(string cls) => new()
        {
            Account = "a",
            Server = "Wuoshi",
            Name = cls,
            Class = cls,
            Level = 68,
        };

        Assert.NotEmpty(engine.FindMatches(post, [Char("Templar")]));
        Assert.NotEmpty(engine.FindMatches(post, [Char("Guardian")]));
    }

    [Fact]
    public void Ill_abbreviation_resolves_to_illusionist()
    {
        var post = Analyze("60 ill lfg XP");

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Contains("Illusionist", post.Classes);
        Assert.Equal(60, post.StatedLevel);
    }

    [Fact]
    public void Shard_of_fear_is_a_known_zone()
    {
        var post = Analyze("fury lfg unrest or sof pst");
        Assert.Equal(PostKind.PlayerLfg, post.Kind);

        var ad = Analyze("SoF lfm need tank");
        Assert.Equal("Shard of Fear", ad.ZoneName);
    }

    [Fact]
    public void Zone_only_group_ad_matches_any_fitting_character()
    {
        var engine = new MatchEngine();
        var warden = new GameCharacter
        {
            Account = "a",
            Server = "Wuoshi",
            Name = "Bramwick",
            Class = "Warden",
            Level = 59,
        };

        // Mistmoore Catacombs 55-65, no role stated → any role welcome.
        var matches = engine.FindMatches(Analyze("LFM catacombs"), [warden]);

        var match = Assert.Single(matches);
        Assert.Contains("any role welcome", match.Reasons);
        Assert.Contains("Mistmoore Catacombs 55-65", match.Reasons);
    }

    [Fact]
    public void Seed_merge_adds_new_zones_but_keeps_user_edits()
    {
        var dir = Directory.CreateTempSubdirectory("eq2lfg-zonemerge").FullName;
        try
        {
            var path = Path.Combine(dir, "zones.json");
            var table = ZoneTable.LoadOrSeed(path);

            // User edits a band and saves.
            var edited = table.Entries
                .Select(e => e.Name == "Castle Mistmoore"
                    ? e with { MinLevel = 62, MaxLevel = 72 }
                    : e)
                .ToList();
            table.Replace(edited);
            table.Save(path);

            var reloaded = ZoneTable.LoadOrSeed(path);

            Assert.Equal(62, reloaded.Resolve("cmm")!.MinLevel);
            // Seed zones absent from the file (none here) would be merged; count unchanged.
            Assert.Equal(edited.Count, reloaded.Entries.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Install_locator_validates_directories()
    {
        var dir = Directory.CreateTempSubdirectory("eq2lfg-install").FullName;
        try
        {
            Assert.False(Eq2InstallLocator.IsValidEq2Directory(dir));
            Assert.False(Eq2InstallLocator.IsValidEq2Directory(null));
            Assert.False(Eq2InstallLocator.IsValidEq2Directory(Path.Combine(dir, "missing")));

            File.WriteAllText(Path.Combine(dir, "acct_characters.ini"), "[Characters]\n");
            Assert.True(Eq2InstallLocator.IsValidEq2Directory(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Install_locator_parses_steam_library_vdf()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path"		"D:\\SteamLibrary"
                }
            }
            """;

        var paths = Eq2InstallLocator.ParseSteamLibraryPaths(vdf);

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], paths);
        Assert.Equal(
            @"D:\SteamLibrary\steamapps\common\EverQuest II",
            Eq2InstallLocator.SteamAppPath(paths[1]));
    }

    [Fact]
    public void Install_locator_expands_common_paths_per_drive()
    {
        var candidates = Eq2InstallLocator.CommonCandidates([@"C:\", @"E:\"]).ToList();

        Assert.Contains(@"E:\games\eq2", candidates);
        Assert.Contains(@"C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest II", candidates);
    }
}

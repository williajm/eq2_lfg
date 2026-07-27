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
    public void Members_in_a_group_ad_is_not_recruitment()
    {
        var post = Analyze("Need 2 more members for CMM");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Equal("Castle Mistmoore", post.ZoneName);
    }

    [Fact]
    public void Offered_spot_keeps_the_requested_role()
    {
        var post = Analyze("CMM group has 1 spot for healer");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(Role.Healer, post.WantedRoles);
    }

    [Fact]
    public void Seeking_a_group_is_a_player_post()
    {
        Assert.Equal(PostKind.PlayerLfg, Analyze("52 wizard seeking group").Kind);
        Assert.Equal(PostKind.GroupAd, Analyze("Unrest seeking Tank / DPS / Util - PST").Kind);
    }

    [Fact]
    public void Mage_means_any_mage_subclass_not_scouts()
    {
        var post = Analyze("need mage for CT");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains("Wizard", post.Classes);
        Assert.Contains("Conjuror", post.Classes);
        Assert.Contains("Illusionist", post.Classes);
        Assert.DoesNotContain("Assassin", post.Classes);
        Assert.DoesNotContain(Role.Dps, post.WantedRoles);
    }

    [Fact]
    public void Any_for_needs_its_context_in_the_same_clause()
    {
        Assert.Equal(PostKind.NotLfg, Analyze("CMM was fun. Anyone up for crafting?").Kind);
        Assert.Equal(PostKind.GroupAd, Analyze("any heals/dps for WC? 10+").Kind);
    }

    [Fact]
    public void Multi_word_zone_self_offers_are_player_posts()
    {
        Assert.Equal(
            PostKind.PlayerLfg, Analyze("Any Fallen Gate groups need a healer?").Kind);
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
    public void Seed_merge_appends_new_abbreviations_to_existing_entries()
    {
        var dir = Directory.CreateTempSubdirectory("eq2lfg-aliasmerge").FullName;
        try
        {
            var path = Path.Combine(dir, "zones.json");

            // A file from an older app version: no "Cata" alias yet, plus one the
            // user added themselves.
            var old = ZoneTable.CreateSeeded().Entries
                .Select(e => e.Name == "Mistmoore Catacombs"
                    ? e with { Abbreviations = ["MMC", "mycata"] }
                    : e)
                .ToList();
            new ZoneTable(old).Save(path);

            var reloaded = ZoneTable.LoadOrSeed(path);

            Assert.Equal("Mistmoore Catacombs", reloaded.Resolve("Cata")!.Name);
            Assert.Equal("Mistmoore Catacombs", reloaded.Resolve("mycata")!.Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("CT LF 1")]
    [InlineData("RoV LF2 lv 25+")]
    [InlineData("1 slot open for CT")]
    [InlineData("GREED Unrest last spot anyone")]
    [InlineData("WC last spot 10-15ish. Anything")]
    [InlineData("Unrest DPS or UTil 5/6 PST")]
    [InlineData("WC 3/6 LF Heal +2")]
    [InlineData("QUICK SS RUNS NEED DEEPS 50+ PST 4/6")]
    [InlineData("any tanks wanna come blow shit up in Sol Eye?")]
    [InlineData("anybody want to start Bloodline Chronicles 50+ pst")]
    [InlineData("Forming group for Living Tombs 52+ pst")]
    public void Slots_seat_counts_and_forming_calls_are_group_ads(string text)
    {
        Assert.Equal(PostKind.GroupAd, Analyze(text).Kind);
    }

    [Theory]
    [InlineData("CT LF 1", 1)]
    [InlineData("RoV LF2 lv 25+", 2)]
    [InlineData("1 slot open for CT", 1)]
    [InlineData("GREED Unrest last spot anyone", 1)]
    [InlineData("Unrest DPS or UTil 5/6 PST", 1)]
    [InlineData("WC 3/6 LF Heal +2", 3)]
    [InlineData("SoF lfm need tank and dps 3/6 pst", 3)]
    public void Slot_and_seat_count_phrasings_extract_spots_left(string text, int expected)
    {
        Assert.Equal(expected, Analyze(text).SpotsLeft);
    }

    [Theory]
    [InlineData("any XP grps going on for 35+?")]
    [InlineData("any RE/CT xp groups going?")]
    public void Asking_whether_groups_are_going_is_a_player_post(string text)
    {
        Assert.Equal(PostKind.PlayerLfg, Analyze(text).Kind);
    }

    [Fact]
    public void Lf_with_multi_digit_level_stays_a_player_post()
    {
        Assert.Equal(PostKind.PlayerLfg, Analyze("50 zerk lf 50+ group").Kind);
    }

    [Fact]
    public void Deeps_and_util_extract_dps_and_support_roles()
    {
        var post = Analyze("Unrest DPS or UTil 5/6 PST");

        Assert.Contains(Role.Dps, post.WantedRoles);
        Assert.Contains(Role.Support, post.WantedRoles);
        Assert.Equal("The Estate of Unrest", post.ZoneName);
    }

    [Fact]
    public void Bare_heal_extracts_healer_role()
    {
        var post = Analyze("WC 3/6 LF Heal +2");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(Role.Healer, post.WantedRoles);
        Assert.Equal("Wailing Caves", post.ZoneName);
    }

    [Fact]
    public void Expansion_shorthand_resolves_when_no_real_zone_is_named()
    {
        Assert.Equal("Echoes of Faydwer (any)", Analyze("70 nec lfg EOF").ZoneName);
        // A real zone in the same message wins over the expansion catch-all.
        Assert.Equal(
            "The Estate of Unrest", Analyze("70 conji lfg eof/unrest/sof").ZoneName);
    }

    [Theory]
    [InlineData("nest of great egg can use any pst")]
    [InlineData("could use a healer and 1 more dps for CMM")]
    public void Zone_that_can_use_someone_is_a_group_ad(string text)
    {
        Assert.Equal(PostKind.GroupAd, Analyze(text).Kind);
    }

    [Theory]
    [InlineData("berz can use, i think, anything but wands?")]
    [InlineData("you can use the CMM portal at 60")]
    [InlineData("anyone have an empty halls of fate I could use please?")]
    public void Can_use_chatter_is_not_an_ad(string text)
    {
        Assert.Equal(PostKind.NotLfg, Analyze(text).Kind);
    }

    [Theory]
    [InlineData("61 inq looking for group.", "Inquisitor")]
    [InlineData("50 warlock looking for xp group got pot on so will log in when ready", "Warlock")]
    [InlineData("70 dirge lf DT claymore update pst", "Dirge")]
    [InlineData("single lvl 70 Conj in your area looking for fun runs", "Conjuror")]
    public void Player_naming_own_class_before_lf_is_a_player_post(string text, string cls)
    {
        var post = Analyze(text);

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Contains(cls, post.Classes);
    }

    [Theory]
    [InlineData("CMM group looking for more. Tank/heals/dps/util - pst!")]
    [InlineData("SoF looking for DPS utility 2 spots pst.")]
    [InlineData("FG group LF dps/utility pst!")]
    [InlineData("looking for tank and 3 dps cmm")]
    public void Group_looking_for_someone_is_still_a_group_ad(string text)
    {
        Assert.Equal(PostKind.GroupAd, Analyze(text).Kind);
    }

    [Theory]
    [InlineData("PST if you need a cleared Unrest........basement  key --- SoD statue")]
    [InlineData("for a choice to play, all healers are good. if you are looking for a raid spot "
        + "you need all healers, and there are always tons of Wardens")]
    [InlineData("pst if u need a cleared Unrest key")]
    [InlineData("you're looking for the basement key, not the statue")]
    public void Second_person_need_and_looking_for_are_not_ads(string text)
    {
        Assert.Equal(PostKind.NotLfg, Analyze(text).Kind);
    }

    // The self-identified-player heuristic must not swallow explicit member-count
    // asks — a leader naming their own class before "lf 2m" is still recruiting.
    [Theory]
    [InlineData("70 dirge lf 1 more for DT")]
    [InlineData("70 guard lf 2m CMM")]
    [InlineData("70 dirge looking for more pst DT")]
    public void Own_class_before_a_member_count_ask_is_still_a_group_ad(string text)
    {
        Assert.Equal(PostKind.GroupAd, Analyze(text).Kind);
    }

    [Fact]
    public void Clock_times_are_not_levels()
    {
        var post = Analyze("CMM group starting at 10pm need healer");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Empty(post.StatedLevels);
    }

    [Theory]
    [InlineData("LF Alchemist to make 60s pally skills")]
    [InlineData("Any alchemists lf to craft 60+pally spells")]
    [InlineData("lf alchemist to make bruiser spells , pst")]
    public void Tradeskill_requests_are_not_ads(string text)
    {
        Assert.Equal(PostKind.NotLfg, Analyze(text).Kind);
    }

    [Fact]
    public void Raid_team_callout_is_recruitment()
    {
        const string bmp = "BMP raid team looking for a Coercer.. raid days and times are "
            + "Friday 9pm - 11pm EST and Sunday 8pm - 11pm EST ... PST/DM Vnorg";
        Assert.Equal(PostKind.Recruitment, Analyze(bmp).Kind);
    }

    [Theory]
    [InlineData("70 troob lfg", "Troubador")]
    [InlineData("conju 70 lfg", "Conjuror")]
    [InlineData("33 coe lfg", "Coercer")]
    public void Newly_observed_class_shorthand_resolves(string text, string cls)
    {
        var post = Analyze(text);

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Contains(cls, post.Classes);
    }

    [Fact]
    public void Level_glued_to_class_still_extracts()
    {
        var post = Analyze("27guard lfg");

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Contains("Guardian", post.Classes);
        Assert.Equal(27, post.StatedLevel);
    }

    [Fact]
    public void Mentoring_members_level_is_not_a_level_requirement()
    {
        var post = Analyze("RoV LFM got 70 mentoring tank");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.True(post.WillMentor);
        Assert.Empty(post.StatedLevels);
        // "got ... tank" is what the group has, not what it wants.
        Assert.Empty(post.WantedRoles);
    }

    [Fact]
    public void Gimme_a_role_is_a_group_ad()
    {
        var post = Analyze("gimme a TANK for CM + AT PoF run! Who knows? Maybe you'll even "
            + "have the honor of rolling on the mythical mount!");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains(Role.Tank, post.WantedRoles);
    }

    [Fact]
    public void Thundering_steppes_is_a_known_zone()
    {
        var post = Analyze("LFM TS skellies, PST - tank + heals + any other (3 spots)");

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Equal("Thundering Steppes", post.ZoneName);
        Assert.Equal(3, post.SpotsLeft);
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

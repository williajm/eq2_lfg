using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Parsing;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Tests;

public class LfgMessageAnalyzerTests
{
    private readonly LfgMessageAnalyzer analyzer = new(ZoneTable.CreateSeeded());

    private static ChatMessage Msg(string text, string speaker = "Brakor") => new()
    {
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(1784976352),
        Speaker = speaker,
        Channel = "LFG",
        Text = text,
        Raw = text,
    };

    [Theory]
    [InlineData("NEED TANK 4 DPS CMM")]
    [InlineData("need tank cmm into hear of mistmoor quest")]
    [InlineData("LFM healer for HoF, have tank")]
    [InlineData("forming SoS group, need heals and dps")]
    public void Recognises_group_ads(string text)
    {
        Assert.Equal(PostKind.GroupAd, analyzer.Analyze(Msg(text)).Kind);
    }

    [Theory]
    [InlineData("38 warlock LFG")]
    [InlineData("52 Warlock LF exp group")]
    [InlineData("Lv45 Fury LF exp group")]
    [InlineData("mystic,fury,wiz,nec,lock,ilu,swash,sin, troub LFG")]
    public void Recognises_player_posts(string text)
    {
        Assert.Equal(PostKind.PlayerLfg, analyzer.Analyze(Msg(text)).Kind);
    }

    [Fact]
    public void Recognises_powerlevel_spam()
    {
        var post = analyzer.Analyze(Msg("WTS powerleveling 1-70PL 1-2hour Krono x1  pst"));
        Assert.Equal(PostKind.Spam, post.Kind);
    }

    [Theory]
    [InlineData("Thank you, South East Griffin Tower")]
    [InlineData("looking for my lost corpse lol")]
    [InlineData("anyone know where the mender is?")]
    public void Ignores_ordinary_chat(string text)
    {
        Assert.Equal(PostKind.NotLfg, analyzer.Analyze(Msg(text)).Kind);
    }

    [Fact]
    public void Extracts_roles_zone_from_group_ad()
    {
        var post = analyzer.Analyze(Msg("NEED TANK 4 DPS CMM"));

        Assert.Contains(Role.Tank, post.WantedRoles);
        Assert.Contains(Role.Dps, post.WantedRoles);
        Assert.Equal("Castle Mistmoore", post.ZoneName);
        Assert.Equal(60, post.ZoneMinLevel);
        Assert.Equal(70, post.ZoneMaxLevel);
        Assert.Null(post.StatedLevel);
    }

    [Fact]
    public void Extracts_class_and_level_from_player_post()
    {
        var post = analyzer.Analyze(Msg("52 Warlock LF exp group"));

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Contains("Warlock", post.Classes);
        Assert.Equal(52, post.StatedLevel);
    }

    [Fact]
    public void Extracts_many_classes_from_multibox_post()
    {
        var post = analyzer.Analyze(Msg("mystic,fury,wiz,nec,lock,ilu,swash,sin, troub LFG"));

        Assert.Equal(
            ["Mystic", "Fury", "Wizard", "Necromancer", "Warlock", "Illusionist", "Swashbuckler", "Assassin", "Troubador"],
            post.Classes);
    }

    [Theory]
    [InlineData("anyone need a 70 Inq or 70 Sk ?")]
    [InlineData("70 inqui lfg eof")]
    [InlineData("fury lfg unrest or sof pst")]
    public void Self_offers_are_player_posts_not_group_ads(string text)
    {
        Assert.Equal(PostKind.PlayerLfg, analyzer.Analyze(Msg(text)).Kind);
    }

    [Fact]
    public void Multibox_post_captures_all_levels()
    {
        var post = analyzer.Analyze(Msg("16 Dirge / 47 Conj / 70 Warden LFG PoA"));

        Assert.Equal(PostKind.PlayerLfg, post.Kind);
        Assert.Equal([16, 47, 70], post.StatedLevels);
        Assert.Equal(["Dirge", "Conjuror", "Warden"], post.Classes);
        Assert.Equal("Palace of the Awakened", post.ZoneName);
    }

    [Fact]
    public void Group_ad_wanting_specific_class_extracts_it()
    {
        var post = analyzer.Analyze(Msg("need warden or templar for Nest grp"));

        Assert.Equal(PostKind.GroupAd, post.Kind);
        Assert.Contains("Warden", post.Classes);
        Assert.Contains("Templar", post.Classes);
        Assert.Equal("The Nest of the Great Egg", post.ZoneName);
    }
}

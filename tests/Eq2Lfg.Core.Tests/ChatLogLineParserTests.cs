using Eq2Lfg.Core.Parsing;

namespace Eq2Lfg.Core.Tests;

public class ChatLogLineParserTests
{
    // Line shapes taken from logs/Wuoshi/eq2log_Bramwick.txt; player names are fictional.
    private const string LfgLine =
        @"(1784976352)[Sat Jul 25 11:45:52 2026] \aPC -1 Brakor:Brakor\/a tells LFG (3), ""NEED TANK 4 DPS CMM """;

    private const string GuildLine =
        @"(1784976375)[Sat Jul 25 11:46:15 2026] \aPC -1 Melia:Melia\/a says to the guild, ""Thank you, South East Griffin Tower""";

    private const string SystemLine =
        "(1784976350)[Sat Jul 25 11:45:50 2026] Logging to 'logs/Wuoshi/eq2log_Bramwick.txt' is now *ON*";

    [Fact]
    public void Parses_channel_tell_with_player_link_markup()
    {
        var msg = ChatLogLineParser.Parse(LfgLine);

        Assert.NotNull(msg);
        Assert.Equal("Brakor", msg.Speaker);
        Assert.Equal("LFG", msg.Channel);
        Assert.Equal("NEED TANK 4 DPS CMM", msg.Text);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1784976352), msg.Timestamp);
    }

    [Fact]
    public void Parses_guild_chat()
    {
        var msg = ChatLogLineParser.Parse(GuildLine);

        Assert.NotNull(msg);
        Assert.Equal("Melia", msg.Speaker);
        Assert.Equal("guild", msg.Channel);
        Assert.Equal("Thank you, South East Griffin Tower", msg.Text);
    }

    [Fact]
    public void Ignores_system_lines()
    {
        Assert.Null(ChatLogLineParser.Parse(SystemLine));
    }

    [Fact]
    public void Parses_ooc_and_shout_and_say()
    {
        var ooc = ChatLogLineParser.Parse(
            @"(1784976400)[Sat Jul 25 11:46:40 2026] \aPC -1 Zim:Zim\/a says out of character, ""anyone seen the griffon?""");
        Assert.NotNull(ooc);
        Assert.Equal("ooc", ooc.Channel);

        var shout = ChatLogLineParser.Parse(
            @"(1784976401)[Sat Jul 25 11:46:41 2026] \aPC -1 Zim:Zim\/a shouts, ""TRAIN to zone!""");
        Assert.NotNull(shout);
        Assert.Equal("shout", shout.Channel);

        var say = ChatLogLineParser.Parse(
            @"(1784976402)[Sat Jul 25 11:46:42 2026] \aPC -1 Zim:Zim\/a says, ""hi""");
        Assert.NotNull(say);
        Assert.Equal("say", say.Channel);
    }

    [Fact]
    public void Parses_incoming_tell()
    {
        var msg = ChatLogLineParser.Parse(
            @"(1784976403)[Sat Jul 25 11:46:43 2026] \aPC -1 Brakor:Brakor\/a tells you, ""got room for a warden?""");

        Assert.NotNull(msg);
        Assert.Equal("tell", msg.Channel);
        Assert.Equal("Brakor", msg.Speaker);
    }
}

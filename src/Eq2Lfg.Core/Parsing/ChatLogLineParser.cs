using System.Globalization;
using System.Text.RegularExpressions;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Parsing;

/// <summary>
/// Parses raw EQ2 log lines into <see cref="ChatMessage"/>s. Typical shapes:
/// <code>
/// (1784976352)[Sat Jul 25 11:45:52 2026] \aPC -1 Brakor:Brakor\/a tells LFG (3), "NEED TANK 4 DPS CMM "
/// (1784976375)[Sat Jul 25 11:46:15 2026] \aPC -1 Melia:Melia\/a says to the guild, "..."
/// (1784976500)[...] Sella tells you, "..."
/// </code>
/// Non-chat lines (combat, system) return null.
/// </summary>
public static partial class ChatLogLineParser
{
    [GeneratedRegex(@"^\((?<epoch>\d+)\)\[[^\]]+\]\s*(?<rest>.*)$")]
    private static partial Regex LinePrefixRegex();

    // \aPC -1 Brakor:Brakor\/a  (link markup around player names); display name is after ':'
    [GeneratedRegex(@"\\aPC\s+-?\d+\s+(?<id>[^:\\]+):(?<name>[^\\]+)\\/a")]
    private static partial Regex PlayerLinkRegex();

    // e.g.  tells LFG (3), "..."   |  tells General (2), "..."
    [GeneratedRegex(@"^(?<speaker>\S+)\s+tells\s+(?<channel>[^(,]+?)\s*(?:\(\d+\))?\s*,\s*""(?<text>.*)""\s*$")]
    private static partial Regex TellsChannelRegex();

    // e.g.  says to the guild, "..."  |  says out of character, "..."  |  says to the group, "..."
    [GeneratedRegex(@"^(?<speaker>\S+)\s+says?\s+(?:to\s+the\s+)?(?<channel>guild|group|raid|out\s+of\s+character)\s*,\s*""(?<text>.*)""\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SaysToRegex();

    // e.g.  says, "..."   (local /say)
    [GeneratedRegex(@"^(?<speaker>\S+)\s+says?\s*,\s*""(?<text>.*)""\s*$")]
    private static partial Regex SaysRegex();

    // e.g.  tells you, "..."  |  You tell Brakor, "..."
    [GeneratedRegex(@"^(?<speaker>\S+)\s+tells\s+you\s*,\s*""(?<text>.*)""\s*$")]
    private static partial Regex TellsYouRegex();

    // e.g.  shouts, "..."
    [GeneratedRegex(@"^(?<speaker>\S+)\s+shouts\s*,\s*""(?<text>.*)""\s*$")]
    private static partial Regex ShoutsRegex();

    public static ChatMessage? Parse(string line)
    {
        var prefix = LinePrefixRegex().Match(line);
        if (!prefix.Success)
        {
            return null;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(
            long.Parse(prefix.Groups["epoch"].Value, CultureInfo.InvariantCulture));

        // Replace player-link markup with the bare display name so the channel
        // patterns below see "Brakor tells LFG (3), ..." regardless of markup.
        var rest = PlayerLinkRegex().Replace(prefix.Groups["rest"].Value, m => m.Groups["name"].Value);

        foreach (var (regex, channelFromMatch) in Patterns)
        {
            var m = regex.Match(rest);
            if (!m.Success)
            {
                continue;
            }

            var channel = channelFromMatch ?? NormalizeChannel(m.Groups["channel"].Value);
            return new ChatMessage
            {
                Timestamp = timestamp,
                Speaker = m.Groups["speaker"].Value.Trim(),
                Channel = channel,
                Text = m.Groups["text"].Value.Trim(),
                Raw = line,
            };
        }

        return null;
    }

    private static readonly (Regex Regex, string? FixedChannel)[] Patterns =
    [
        (TellsYouRegex(), "tell"),
        (TellsChannelRegex(), null),
        (SaysToRegex(), null),
        (ShoutsRegex(), "shout"),
        (SaysRegex(), "say"),
    ];

    private static string NormalizeChannel(string channel)
    {
        var c = channel.Trim();
        return c.Equals("out of character", StringComparison.OrdinalIgnoreCase) ? "ooc" : c;
    }
}

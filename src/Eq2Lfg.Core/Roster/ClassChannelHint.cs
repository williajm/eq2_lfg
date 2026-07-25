using System.Text.RegularExpressions;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Roster;

/// <summary>
/// Fallback class detection: EQ2 auto-joins each character to a chat channel named after
/// its class, and joined channels are persisted in <c>&lt;Server&gt;_&lt;Name&gt;_eq2_uisettings.xml</c>.
/// </summary>
public static partial class ClassChannelHint
{
    [GeneratedRegex("""<Channel[^>]*\bname="([^"]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelRegex();

    /// <summary>Returns the canonical class name hinted by the character's uisettings file, or null.</summary>
    public static string? DetectClass(string eq2Directory, string server, string characterName)
    {
        var path = Path.Combine(eq2Directory, $"{server}_{characterName}_eq2_uisettings.xml");
        if (!File.Exists(path))
        {
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }

        foreach (Match m in ChannelRegex().Matches(content))
        {
            var channel = m.Groups[1].Value;
            var cls = ClassCatalog.ResolveClass(channel);
            // Only exact class names count — abbreviations aren't channel names,
            // and generic channels (LFG, General, ...) don't resolve at all.
            if (cls is not null && string.Equals(cls, channel, StringComparison.OrdinalIgnoreCase))
            {
                return cls;
            }
        }

        return null;
    }
}

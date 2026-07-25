using System.Text.RegularExpressions;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.Core.Parsing;

/// <summary>
/// Classifies chat messages into group ads / player-LFG posts / spam and extracts
/// the roles, classes, levels, and zones they mention.
/// </summary>
public sealed partial class LfgMessageAnalyzer(ZoneTable zoneTable)
{
    [GeneratedRegex(@"\b(wts|wtb|selling|buying|krono|plat\b|powerlevel\w*|\bPL\b|carry|carries)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SpamRegex();

    [GeneratedRegex(@"\b(need|needs|neeed|lfm|lf\s*\d+\s*m(?:ore)?|looking\s+for|forming|making\s+group|starting)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GroupAdRegex();

    [GeneratedRegex(@"\blfg\b|\blf\b.*\bgroup\b|\blfw\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLfgRegex();

    // "anyone need a 70 Inq?" is a player offering themselves, despite the word "need".
    [GeneratedRegex(@"\b(?:anyone|any1|any\s+(?:grp|group)s?)\s+(?:need|want)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SelfOfferRegex();

    [GeneratedRegex(@"\b(?:lvl?|level)?\s*(\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

    [GeneratedRegex(@"\b(tank|mt|ot)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TankRegex();

    [GeneratedRegex(@"\b(healer?s?|heals|healz)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HealerRegex();

    [GeneratedRegex(@"\b(dps|dd|damage)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DpsRegex();

    [GeneratedRegex(@"\b(support|utility|bard|chanter|enchanter|cc)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SupportRegex();

    [GeneratedRegex(@"[a-zA-Z']+", RegexOptions.IgnoreCase)]
    private static partial Regex WordRegex();

    public LfgPost Analyze(ChatMessage message)
    {
        var text = message.Text;

        if (SpamRegex().IsMatch(text))
        {
            return new LfgPost { Kind = PostKind.Spam, Message = message };
        }

        var roles = ExtractRoles(text);
        var classes = ExtractClasses(text);
        var zone = zoneTable.FindInText(text);
        var statedLevels = ExtractLevels(text);

        var looksLikeGroupAd = GroupAdRegex().IsMatch(text) && !SelfOfferRegex().IsMatch(text);
        var looksLikePlayerLfg = PlayerLfgRegex().IsMatch(text) || SelfOfferRegex().IsMatch(text);

        // "mystic,fury,wiz LFG" or "52 warlock LFG": the speaker is offering, not recruiting.
        // "need tank cmm" wins over "lf" phrasing when both appear ("need 2 more, we're LFG" is a group).
        PostKind kind;
        if (looksLikeGroupAd)
        {
            kind = PostKind.GroupAd;
        }
        else if (looksLikePlayerLfg)
        {
            kind = PostKind.PlayerLfg;
        }
        else
        {
            return new LfgPost { Kind = PostKind.NotLfg, Message = message };
        }

        // A "group ad" that names no role, class, or zone is noise ("looking for my corpse").
        if (kind == PostKind.GroupAd && roles.Count == 0 && classes.Count == 0 && zone is null)
        {
            return new LfgPost { Kind = PostKind.NotLfg, Message = message };
        }

        return new LfgPost
        {
            Kind = kind,
            Message = message,
            WantedRoles = roles,
            Classes = classes,
            ZoneName = zone?.Name,
            ZoneMinLevel = zone?.MinLevel,
            ZoneMaxLevel = zone?.MaxLevel,
            StatedLevels = statedLevels,
        };
    }

    private static List<Role> ExtractRoles(string text)
    {
        var roles = new List<Role>();
        if (TankRegex().IsMatch(text))
        {
            roles.Add(Role.Tank);
        }

        if (HealerRegex().IsMatch(text))
        {
            roles.Add(Role.Healer);
        }

        if (DpsRegex().IsMatch(text))
        {
            roles.Add(Role.Dps);
        }

        if (SupportRegex().IsMatch(text))
        {
            roles.Add(Role.Support);
        }

        return roles;
    }

    private static List<string> ExtractClasses(string text)
    {
        var classes = new List<string>();
        foreach (Match word in WordRegex().Matches(text))
        {
            var cls = ClassCatalog.ResolveClass(word.Value);
            if (cls is not null && !classes.Contains(cls))
            {
                classes.Add(cls);
            }
        }

        return classes;
    }

    private static List<int> ExtractLevels(string text)
    {
        var levels = new List<int>();
        foreach (Match m in LevelRegex().Matches(text))
        {
            var value = int.Parse(m.Groups[1].Value);
            // Group sizes ("need 2 dps") are small; plausible levels are 10-130.
            if (value is >= 10 and <= 130 && !levels.Contains(value))
            {
                levels.Add(value);
            }
        }

        return levels;
    }
}

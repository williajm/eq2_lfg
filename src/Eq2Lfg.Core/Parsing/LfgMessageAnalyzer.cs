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

    // "need tank", "LFM", "LF2M", "room for 3m", "one spot in CT", "forming group",
    // "seeking zerker/monk", "CMM 1 spot chanter/bard", "has 1 spot for anything".
    // "seeking (a) group" is excluded — that's a player looking, not a group asking.
    [GeneratedRegex(@"\b(need|needs|neeed|lfm|lf\s*\d+\s*m(?:ore)?|looking\s+for|seek(?:ing|s)?\b(?!\s+(?:an?\s+)?(?:exp\s+|xp\s+)?gr(?:ou)?p\b)|forming|making\s+group|starting|room\s+for|\d+\s*spots?|spots?\s+(?:in|open|left|for)|open\s+spots?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GroupAdRegex();

    // Bare "LF <role/class>" ("LF healer and tank MMC", "Klak LF chanter/bard") —
    // a group ad when combined with a role or class mention.
    [GeneratedRegex(@"\blf\b", RegexOptions.IgnoreCase)]
    private static partial Regex BareLfRegex();

    // "any heals/dps for WC?", "anyone for FG?", "Any bard reps for PoA?" —
    // a group ad when the same clause also names a role, class, or zone.
    [GeneratedRegex(@"(?<clause>\bany(?:one|1)?\b[^.!?;]*?\bfor\b[^.!?;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex AnyForRegex();

    // "50+ exp group LF3M", "Giants exp group LFM!" — an experience group is a
    // group by definition, even when no role, class, or known zone is named.
    [GeneratedRegex(@"\b(?:exp|xp)\s+gr(?:ou)?p\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExpGroupRegex();

    [GeneratedRegex(@"\blfg\b|\blf\b.*\bgroup\b|\blfw\b|\bseek(?:ing|s)?\s+(?:an?\s+)?(?:exp\s+|xp\s+)?gr(?:ou)?p\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLfgRegex();

    // "anyone need a 70 Inq?" and "Any RoV groups need a healer?" are players
    // offering themselves, despite the word "need". Zone names between "any" and
    // "groups" may run to a few words ("Any Fallen Gate groups need a healer?").
    [GeneratedRegex(@"\b(?:anyone|any1|any\s+(?:\S+\s+){0,3}(?:grp|group)s?)\s+(?:need|want)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SelfOfferRegex();

    // Guild recruitment: "<Lucid Dreams> Seeking sk/monk ... Full Time Position Raiders",
    // "Velocity is lookin for Dirge/Healers ... for raiding ... accept all casuals".
    // Deliberately narrow — a mere mention of "guild" must not disqualify a genuine
    // group ad like "guild group needs healer for CMM".
    [GeneratedRegex(@"^\s*<[^>]+>|\b(?:recruit\w*|raiders|raiding|casuals?|full\s*time|apply|looking\s+for\s+(?:more\s+)?members|level\s+\d+\s+guild)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RecruitmentRegex();

    // "have tank", "already got healer": roles/classes the group HAS, not ones it
    // wants. "has 1 spot for healer" offers a place, though — that clause stays.
    [GeneratedRegex(@"\b(?:already\s+)?(?:have|has|got|found)\b(?![^,;.!|]*\b(?:spots?|room)\b)[^,;.!|]*", RegexOptions.IgnoreCase)]
    private static partial Regex HaveClauseRegex();

    // "LF2M", "need 2 more", "room for 3m", "2 spots", "one spot left": how many
    // places the group has open. Single digits only — "12 spots" is not a group.
    [GeneratedRegex(@"\b(?:lf\s*(?<n>\d)\s*m(?:ore)?\b|need\w*\s+(?<n>\d|one|two|three|four|five)\s+more\b|room\s+for\s+(?<n>\d|one|two|three|four|five)\s*m?(?:ore)?\b|(?<n>\d|one|two|three|four|five)\s+(?:more\s+|open\s+)?spots?\b)", RegexOptions.IgnoreCase)]
    private static partial Regex SpotsLeftRegex();

    // "WILL MENTOR 40+" / "can mentor" / "mentor down"
    [GeneratedRegex(@"\bmentor\w*(?:\s+(?:down\s+)?(?:to\s+)?(\d{1,3})\s*\+?)?", RegexOptions.IgnoreCase)]
    private static partial Regex MentorRegex();

    [GeneratedRegex(@"\b(?:lvl?|level)?\s*(\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

    [GeneratedRegex(@"\b(tanks?|mt|ot|fighters?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TankRegex();

    [GeneratedRegex(@"\b(healer?s?|heals|healz|priests?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HealerRegex();

    [GeneratedRegex(@"\b(dps|dd|damage)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DpsRegex();

    // "LF tank +dps/non leather": a healer who isn't a druid, i.e. a cleric or shaman.
    [GeneratedRegex(@"\bnon[\s-]*leather\b", RegexOptions.IgnoreCase)]
    private static partial Regex NonLeatherRegex();

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

        if (RecruitmentRegex().IsMatch(text))
        {
            return new LfgPost { Kind = PostKind.Recruitment, Message = message };
        }

        // Roles/classes after "have"/"got" are what the group already has — drop
        // those clauses before working out what is wanted. Zone lookup still uses
        // the full text ("have tank for CMM" names the zone either way).
        var wantedText = HaveClauseRegex().Replace(text, " ");
        var roles = ExtractRoles(wantedText);
        var classes = ExtractClasses(wantedText);
        var zone = zoneTable.FindInText(text);
        var mentorMatch = MentorRegex().Match(text);
        var mentorFloor = mentorMatch.Success && mentorMatch.Groups[1].Success
            ? int.Parse(mentorMatch.Groups[1].Value)
            : (int?)null;
        var statedLevels = ExtractLevels(text, exceptValue: mentorFloor);

        var isExpGroup = ExpGroupRegex().IsMatch(text);
        var kind = Classify(text, roles, classes, IsAnyForAd(text), isExpGroup);

        // A "group ad" that names no role, class, or zone is noise ("looking for my
        // corpse") — unless it's an exp group, which is worth surfacing on its own.
        if (kind == PostKind.GroupAd && roles.Count == 0 && classes.Count == 0 && zone is null
            && !isExpGroup)
        {
            kind = PostKind.NotLfg;
        }

        if (kind == PostKind.NotLfg)
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
            SpotsLeft = kind == PostKind.GroupAd ? ParseSpotsLeft(text) : null,
            WillMentor = mentorMatch.Success,
            MentorFloor = mentorFloor,
        };
    }

    // "any heals/dps for WC?" is an ad only when the "any ... for ..." clause itself
    // names a role, class, or zone — context from other sentences doesn't count
    // ("CMM was fun. Anyone up for crafting?" is chatter).
    private bool IsAnyForAd(string text)
    {
        var match = AnyForRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var clause = match.Groups["clause"].Value;
        return ExtractRoles(clause).Count > 0
            || ExtractClasses(clause).Count > 0
            || zoneTable.FindInText(clause) is not null;
    }

    // "mystic,fury,wiz LFG" or "52 warlock LFG": the speaker is offering, not recruiting.
    // "need tank cmm" wins over "lf" phrasing when both appear ("need 2 more, we're LFG" is a group).
    private static PostKind Classify(
        string text, List<Role> roles, List<string> classes, bool isAnyForAd, bool isExpGroup)
    {
        var wantsSomeone = roles.Count > 0 || classes.Count > 0;
        var looksLikePlayerLfg = PlayerLfgRegex().IsMatch(text) || SelfOfferRegex().IsMatch(text);
        var looksLikeGroupAd = !SelfOfferRegex().IsMatch(text)
            && (GroupAdRegex().IsMatch(text)
                || (!looksLikePlayerLfg
                    && BareLfRegex().IsMatch(text)
                    && (wantsSomeone || isExpGroup))
                || (!looksLikePlayerLfg && isAnyForAd));

        if (looksLikeGroupAd)
        {
            return PostKind.GroupAd;
        }

        return looksLikePlayerLfg ? PostKind.PlayerLfg : PostKind.NotLfg;
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
        foreach (var token in WordRegex().Matches(text).Select(word => word.Value))
        {
            // "clerics" and "sorcs" read as the singular; short tokens stay as-is
            // so abbreviations like "dps" or "sos" are never mangled.
            var singular = token.Length > 3 && token.EndsWith('s') ? token[..^1] : token;

            var cls = ClassCatalog.ResolveClass(token) ?? ClassCatalog.ResolveClass(singular);
            if (cls is not null)
            {
                AddClass(classes, cls);
                continue;
            }

            // Archetype terms ("sorc", "cleric", "bard") mean any of their subclasses.
            var group = ClassCatalog.ResolveClassGroup(token) ?? ClassCatalog.ResolveClassGroup(singular);
            foreach (var member in group ?? [])
            {
                AddClass(classes, member);
            }
        }

        if (NonLeatherRegex().IsMatch(text))
        {
            foreach (var member in new[] { "Templar", "Inquisitor", "Mystic", "Defiler" })
            {
                AddClass(classes, member);
            }
        }

        return classes;
    }

    private static void AddClass(List<string> classes, string cls)
    {
        if (!classes.Contains(cls))
        {
            classes.Add(cls);
        }
    }

    private static int? ParseSpotsLeft(string text)
    {
        var match = SpotsLeftRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["n"].Value.ToLowerInvariant() switch
        {
            "one" => 1,
            "two" => 2,
            "three" => 3,
            "four" => 4,
            "five" => 5,
            var digit => int.Parse(digit),
        };

        // The advertiser already fills a slot, so a six-player group can have at
        // most five open; anything larger is a raid callout or a typo.
        return value is >= 1 and <= 5 ? value : null;
    }

    private static List<int> ExtractLevels(string text, int? exceptValue = null)
    {
        var levels = new List<int>();
        foreach (Match m in LevelRegex().Matches(text))
        {
            var value = int.Parse(m.Groups[1].Value);
            // Group sizes ("need 2 dps") are small; plausible levels are 10-130.
            // A mentor floor ("mentor 40+") is a limit, not a character level.
            if (value is >= 10 and <= 130 && value != exceptValue && !levels.Contains(value))
            {
                levels.Add(value);
            }
        }

        return levels;
    }
}

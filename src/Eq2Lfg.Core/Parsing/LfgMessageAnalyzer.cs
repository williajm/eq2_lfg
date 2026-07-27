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

    // "need tank", "LFM", "LF2M", "CT LF 1", "room for 3m", "space for 1", "one spot
    // in CT", "1 slot open", "last spot", "4/6 pst", "forming group", "seeking
    // zerker/monk", "CMM 1 spot chanter/bard", "has 1 spot for anything".
    // "seeking/looking for (a) group" is excluded — that's a player looking, not a
    // group asking — as is second-person "you/u need" / "you are/you're looking
    // for", which addresses the reader ("PST if you need a cleared Unrest").
    // "lf 1" allows a single digit only, so "lf 50+ group" stays a player post.
    [GeneratedRegex(@"\b((?<!\b(?:you|u)\s)nee+ds?|lfm|lf\s*\d(?![\d+])\s*(?:m(?:ore)?)?|(?<!\b(?:you|u)\s)(?<!\byou\s+are\s)(?<!\byou'?re\s)looking\s+for(?!\s+(?:an?\s+)?(?:exp\s+|xp\s+)?gr(?:ou)?ps?\b)|seek(?:ing|s)?\b(?!\s+(?:an?\s+)?(?:exp\s+|xp\s+)?gr(?:ou)?ps?\b)|gim(?:me|mie)|forming|making\s+group|starting|(?:wanna|want\s+to)\s+start|(?:room|space)\s+for|\d+\s*(?:spots?|slots?)|(?:spots?|slots?)\s+(?:in|open|left|for)|open\s+(?:spots?|slots?)|last\s+(?:spot|slot)|[0-5]\s*/\s*6)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GroupAdRegex();

    // "nest of great egg can use any pst": a group-ad verb only when the message
    // also names a zone (see Classify) — "berz can use anything but wands" is item
    // chatter, and "you/I can use" addresses a person, not a group's needs.
    [GeneratedRegex(@"(?<!\byou\s)(?<!\bu\s)(?<!\bi\s)\b(?:can|could)\s+use\b", RegexOptions.IgnoreCase)]
    private static partial Regex CanUseRegex();

    // Bare "LF <role/class>" ("LF healer and tank MMC", "Klak LF chanter/bard") —
    // a group ad when combined with a role or class mention.
    [GeneratedRegex(@"\blf\b", RegexOptions.IgnoreCase)]
    private static partial Regex BareLfRegex();

    // "any heals/dps for WC?", "anyone for FG?", "Any bard reps for PoA?",
    // "any tanks wanna come to Sol Eye?" — a group ad when the same clause also
    // names a role, class, or zone.
    [GeneratedRegex(@"(?<clause>\bany(?:one|1|body)?\b[^.!?;]*?\b(?:for|wanna\s+(?:come|join)|want\s+to\s+(?:come|join))\b[^.!?;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex AnyForRegex();

    // "50+ exp group LF3M", "Giants exp group LFM!" — an experience group is a
    // group by definition, even when no role, class, or known zone is named.
    [GeneratedRegex(@"\b(?:exp|xp)\s+gr(?:ou)?ps?\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExpGroupRegex();

    // "groups going" covers players asking around: "any RE/CT xp groups going?".
    // "looking for (xp) group" mirrors the seek-group exclusion in GroupAdRegex:
    // "61 inq looking for group" offers a player, "CMM group looking for more" asks.
    [GeneratedRegex(@"\blfg\b|\blf\b.*\bgr(?:ou)?p\b|\blfw\b|\b(?:seek(?:ing|s)?|looking\s+for)\s+(?:an?\s+)?(?:exp\s+|xp\s+)?gr(?:ou)?ps?\b|\bgr(?:ou)?ps?\s+going\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLfgRegex();

    // "70 dirge lf DT claymore update", "single lvl 70 Conj ... looking for fun
    // runs": a player naming their own level and class just before "lf"/"looking
    // for" is offering themselves — unless the message goes on to name a role or
    // some other class it wants (see Classify). A member count after the verb
    // ("70 guard lf 2m CMM", "70 dirge looking for more") is the group asking, so
    // those never read as self-offers.
    [GeneratedRegex(@"^\s*(?:[\w']+\s+){0,2}?(?:lvl?\s*)?\d{1,3}\s*(?<class>[a-zA-Z']+)\s+(?:[\w']+\s+){0,3}?(?:lf\b(?!\s*\d|\s+more\b)|looking\s+for\b(?!\s+more\b))", RegexOptions.IgnoreCase)]
    private static partial Regex SelfIdentifiedLfRegex();

    // "anyone need a 70 Inq?" and "Any RoV groups need a healer?" are players
    // offering themselves, despite the word "need". Zone names between "any" and
    // "groups" may run to a few words ("Any Fallen Gate groups need a healer?").
    // "want to" is excluded: "anybody want to start CT?" is forming, not offering.
    [GeneratedRegex(@"\b(?:anyone|any1|anybody|any\s+(?:\S+\s+){0,3}(?:grp|group)s?)\s+(?:need|want(?!s?\s+to\b))\b", RegexOptions.IgnoreCase)]
    private static partial Regex SelfOfferRegex();

    // Guild recruitment: "<Lucid Dreams> Seeking sk/monk ... Full Time Position Raiders",
    // "Velocity is lookin for Dirge/Healers ... for raiding ... accept all casuals".
    // Deliberately narrow — a mere mention of "guild" must not disqualify a genuine
    // group ad like "guild group needs healer for CMM".
    [GeneratedRegex(@"^\s*<[^>]+>|\b(?:recruit\w*|raiders|raiding|raid\s+(?:team|force)|casuals?|full\s*time|apply|looking\s+for\s+(?:more\s+)?members|level\s+\d+\s+guild)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RecruitmentRegex();

    // "LF Alchemist to make 60s pally skills": a tradeskill request — any adventure
    // class it names is the spells' owner, not someone a group wants.
    [GeneratedRegex(@"\b(?:alchemist|sage|jeweler|jeweller|provisioner|carpenter|woodworker|armou?rer|weaponsmith|tailor|tinkerer|transmuter)s?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TradeskillRegex();

    // "have tank", "already got healer": roles/classes the group HAS, not ones it
    // wants. "has 1 spot for healer" offers a place, though — that clause stays.
    [GeneratedRegex(@"\b(?:already\s+)?(?:have|has|got|found)\b(?![^,;.!|]*\b(?:spots?|room)\b)[^,;.!|]*", RegexOptions.IgnoreCase)]
    private static partial Regex HaveClauseRegex();

    // "LF2M", "LF 1", "need 2 more", "room for 3m", "2 spots", "one slot left",
    // "last spot", "4/6": how many places the group has open. Single digits only —
    // "12 spots" is not a group. "4/6" counts filled seats, so open = 6 - filled.
    [GeneratedRegex(@"\b(?:lf\s*(?<n>\d)(?![\d+])\s*(?:m(?:ore)?)?\b|need\w*\s+(?<n>\d|one|two|three|four|five)\s+more\b|(?:room|space)\s+for\s+(?<n>\d|one|two|three|four|five)\s*m?(?:ore)?\b|(?<n>\d|one|two|three|four|five)\s+(?:more\s+|open\s+)?(?:spots?|slots?)\b|(?<n>last)\s+(?:spot|slot)\b|(?<filled>[0-5])\s*/\s*6\b)", RegexOptions.IgnoreCase)]
    private static partial Regex SpotsLeftRegex();

    // "WILL MENTOR 40+" / "can mentor" / "mentor down"
    [GeneratedRegex(@"\bmentor\w*(?:\s+(?:down\s+)?(?:to\s+)?(\d{1,3})\s*\+?)?", RegexOptions.IgnoreCase)]
    private static partial Regex MentorRegex();

    // "got 70 mentoring tank": a number before "mentor" is the mentor's own level,
    // not a level the group is asking for.
    [GeneratedRegex(@"\b(\d{1,3})\s*\+?\s+mentor", RegexOptions.IgnoreCase)]
    private static partial Regex MentorLevelRegex();

    // The trailing lookahead lets a level run into its class ("27guard lfg")
    // while keeping clock times ("starting at 10pm") out.
    [GeneratedRegex(@"\b(?:lvl?|level)?\s*(\d{1,3})(?![ap]m\b)(?=[a-zA-Z]|\b)", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();

    [GeneratedRegex(@"\b(tanks?|mt|ot|fighters?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TankRegex();

    [GeneratedRegex(@"\b(heal(?:er|z)?s?|priests?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HealerRegex();

    [GeneratedRegex(@"\b(dps|dd|deeps|damage)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DpsRegex();

    // "LF tank +dps/non leather": a healer who isn't a druid, i.e. a cleric or shaman.
    [GeneratedRegex(@"\bnon[\s-]*leather\b", RegexOptions.IgnoreCase)]
    private static partial Regex NonLeatherRegex();

    [GeneratedRegex(@"\b(support|util(?:ity)?|bard|chanter|enchanter|cc)\b", RegexOptions.IgnoreCase)]
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

        if (TradeskillRegex().IsMatch(text))
        {
            return new LfgPost { Kind = PostKind.NotLfg, Message = message };
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
        var mentorLevelMatch = MentorLevelRegex().Match(text);
        var mentorLevel = mentorLevelMatch.Success
            ? int.Parse(mentorLevelMatch.Groups[1].Value)
            : (int?)null;
        var statedLevels = ExtractLevels(text, mentorFloor, mentorLevel);

        var isExpGroup = ExpGroupRegex().IsMatch(text);
        var kind = Classify(text, roles, classes, IsAnyForAd(text), isExpGroup, zone is not null);

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
        string text, List<Role> roles, List<string> classes, bool isAnyForAd, bool isExpGroup,
        bool hasZone)
    {
        var wantsSomeone = roles.Count > 0 || classes.Count > 0;
        var selfIdentified = IsSelfIdentifiedLf(text, roles, classes);
        var looksLikePlayerLfg = PlayerLfgRegex().IsMatch(text)
            || SelfOfferRegex().IsMatch(text)
            || selfIdentified;
        var looksLikeGroupAd = !SelfOfferRegex().IsMatch(text)
            && !selfIdentified
            && (GroupAdRegex().IsMatch(text)
                || (CanUseRegex().IsMatch(text) && hasZone)
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

    // "70 dirge lf DT claymore update" offers the dirge; "70 dirge lf healer for
    // DT" still recruits, so any wanted role or class beyond the speaker's own
    // disqualifies.
    private static bool IsSelfIdentifiedLf(string text, List<Role> roles, List<string> classes)
    {
        if (roles.Count > 0)
        {
            return false;
        }

        var match = SelfIdentifiedLfRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var token = match.Groups["class"].Value;
        var own = ClassCatalog.ResolveClass(token)
            ?? (token.Length > 3 && token.EndsWith('s') ? ClassCatalog.ResolveClass(token[..^1]) : null);
        return own is not null && classes.All(c => c == own);
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

        // "4/6" states filled seats; the rest state open ones directly.
        var value = match.Groups["filled"].Success
            ? 6 - int.Parse(match.Groups["filled"].Value)
            : match.Groups["n"].Value.ToLowerInvariant() switch
            {
                "one" or "last" => 1,
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

    private static List<int> ExtractLevels(string text, params int?[] exceptValues)
    {
        var levels = new List<int>();
        foreach (Match m in LevelRegex().Matches(text))
        {
            var value = int.Parse(m.Groups[1].Value);
            // Group sizes ("need 2 dps") are small; plausible levels are 10-130.
            // A mentor's own level or floor ("mentor 40+") is not a character level.
            if (value is >= 10 and <= 130 && !exceptValues.Contains(value) && !levels.Contains(value))
            {
                levels.Add(value);
            }
        }

        return levels;
    }
}

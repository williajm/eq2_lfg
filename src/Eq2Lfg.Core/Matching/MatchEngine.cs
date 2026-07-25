using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Matching;

public sealed record MatchOptions
{
    /// <summary>How far outside a stated level / zone band a character may be and still match.</summary>
    public int LevelTolerance { get; init; } = 5;

    /// <summary>
    /// Characters above the ad's level range still match — EQ2 mentoring lets a higher-level
    /// character lower their level to the group's.
    /// </summary>
    public bool AllowMentorDown { get; init; } = true;
}

/// <summary>
/// Decides which of the user's enabled characters fit a group ad, and why.
/// </summary>
public sealed class MatchEngine(MatchOptions options)
{
    public MatchEngine()
        : this(new MatchOptions())
    {
    }

    /// <summary>Returns one result per matching character; empty if the post isn't a group ad.</summary>
    public IReadOnlyList<MatchResult> FindMatches(
        LfgPost post, IEnumerable<GameCharacter> enabledCharacters)
    {
        if (post.Kind != PostKind.GroupAd)
        {
            return [];
        }

        return enabledCharacters
            .Select(character => TryMatch(post, character))
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();
    }

    private MatchResult? TryMatch(LfgPost post, GameCharacter character)
    {
        if (character.Class is null || character.Role is null)
        {
            return null;
        }

        var reasons = new List<string>();
        if (!RoleOrClassWanted(post, character, reasons) || !LevelFits(post, character, reasons))
        {
            return null;
        }

        return new MatchResult { Character = character, Post = post, Reasons = reasons };
    }

    private static bool RoleOrClassWanted(LfgPost post, GameCharacter character, List<string> reasons)
    {
        if (post.Classes.Contains(character.Class!, StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add($"{character.Class} wanted");
            return true;
        }

        if (post.WantedRoles.Contains(character.Role!.Value))
        {
            reasons.Add(character.Role.Value.ToString().ToLowerInvariant());
            return true;
        }

        // "LFM catacombs" names no role or class: anyone who fits the level is welcome.
        if (post.WantedRoles.Count == 0 && post.Classes.Count == 0)
        {
            reasons.Add("any role welcome");
            return true;
        }

        return false;
    }

    private bool LevelFits(LfgPost post, GameCharacter character, List<string> reasons)
    {
        if (character.Level is not { } level)
        {
            // Unknown level: match on role/class alone rather than stay silent.
            return true;
        }

        if (post.StatedLevels.Count > 0)
        {
            // "need healer 40-50" states a range; a single "for 55 grp" collapses to one value.
            var statedMin = post.StatedLevels.Min();
            var statedMax = post.StatedLevels.Max();
            var label = statedMin == statedMax
                ? $"level {statedMin}±{options.LevelTolerance}"
                : $"level {statedMin}-{statedMax}";
            return FitsRange(
                level, statedMin, statedMax, label, $"can mentor down to {statedMax}", reasons);
        }

        if (post is { ZoneMinLevel: { } min, ZoneMaxLevel: { } max })
        {
            return FitsRange(level, min, max, $"{post.ZoneName} {min}-{max}",
                $"can mentor down to {post.ZoneName} {min}-{max}", reasons);
        }

        // Ad carries no level information at all.
        return true;
    }

    private bool FitsRange(
        int level, int min, int max, string inRangeReason, string mentorReason, List<string> reasons)
    {
        if (level < min - options.LevelTolerance)
        {
            return false;
        }

        if (level > max + options.LevelTolerance)
        {
            if (!options.AllowMentorDown)
            {
                return false;
            }

            reasons.Add(mentorReason);
            return true;
        }

        reasons.Add(inRangeReason);
        return true;
    }
}

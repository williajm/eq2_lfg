using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Matching;

public sealed record MatchOptions
{
    /// <summary>How far outside a stated level / zone band a character may be and still match.</summary>
    public int LevelTolerance { get; init; } = 5;
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

        var results = new List<MatchResult>();
        foreach (var character in enabledCharacters)
        {
            if (character.Class is null || character.Role is null)
            {
                continue;
            }

            var reasons = new List<string>();

            var classWanted = post.Classes.Contains(character.Class, StringComparer.OrdinalIgnoreCase);
            var roleWanted = post.WantedRoles.Contains(character.Role.Value);
            if (classWanted)
            {
                reasons.Add($"{character.Class} wanted");
            }
            else if (roleWanted)
            {
                reasons.Add(character.Role.Value.ToString().ToLowerInvariant());
            }
            else
            {
                continue;
            }

            if (!LevelFits(post, character, reasons))
            {
                continue;
            }

            results.Add(new MatchResult { Character = character, Post = post, Reasons = reasons });
        }

        return results;
    }

    private bool LevelFits(LfgPost post, GameCharacter character, List<string> reasons)
    {
        if (character.Level is not { } level)
        {
            // Unknown level: match on role/class alone rather than stay silent.
            return true;
        }

        if (post.StatedLevel is { } stated)
        {
            if (Math.Abs(level - stated) > options.LevelTolerance)
            {
                return false;
            }

            reasons.Add($"level {stated}±{options.LevelTolerance}");
            return true;
        }

        if (post is { ZoneMinLevel: { } min, ZoneMaxLevel: { } max })
        {
            if (level < min - options.LevelTolerance || level > max + options.LevelTolerance)
            {
                return false;
            }

            reasons.Add($"{post.ZoneName} {min}-{max}");
            return true;
        }

        // Ad carries no level information at all.
        return true;
    }
}

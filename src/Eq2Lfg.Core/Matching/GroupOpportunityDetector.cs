using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Matching;

public sealed record GroupOpportunityOptions
{
    /// <summary>How long a player-LFG post stays relevant.</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Maximum level difference across a compatible cluster.</summary>
    public int LevelSpread { get; init; } = 10;

    /// <summary>Minimum people (posters + at most one of the user's characters).</summary>
    public int MinPlayers { get; init; } = 3;

    /// <summary>Minimum distinct archetypes (tank/healer/dps/support) covered.</summary>
    public int MinArchetypes { get; init; } = 2;

    /// <summary>Higher-level posters/characters count as compatible — they can mentor down.</summary>
    public bool AllowMentorDown { get; init; } = true;
}

/// <summary>A cluster of compatible player-LFG posts that could seed a new group.</summary>
public sealed record GroupOpportunity
{
    public required IReadOnlyList<LfgPost> Posts { get; init; }

    /// <summary>The user's enabled characters that fit the cluster's level range.</summary>
    public required IReadOnlyList<GameCharacter> OwnCandidates { get; init; }

    public int? MinLevel { get; init; }
    public int? MaxLevel { get; init; }
    public required IReadOnlySet<Role> Archetypes { get; init; }

    /// <summary>Stable identity for cooldown purposes: who is in the cluster.</summary>
    public string Signature =>
        string.Join(",", Posts.Select(p => p.Advertiser.ToLowerInvariant()).Order());
}

/// <summary>
/// Watches individual player-LFG posts and reports when enough compatible players
/// are looking (optionally completed by one of the user's own characters) to form a group.
/// </summary>
public sealed class GroupOpportunityDetector(GroupOpportunityOptions options)
{
    private readonly Dictionary<string, LfgPost> latestByAdvertiser =
        new(StringComparer.OrdinalIgnoreCase);

    public GroupOpportunityDetector()
        : this(new GroupOpportunityOptions())
    {
    }

    /// <summary>Feed every analyzed post; only PlayerLfg posts are retained.</summary>
    public void Observe(LfgPost post)
    {
        if (post.Kind == PostKind.PlayerLfg)
        {
            latestByAdvertiser[post.Advertiser] = post;
        }
    }

    public IReadOnlyList<LfgPost> ActivePosts(DateTimeOffset now)
    {
        Prune(now);
        return latestByAdvertiser.Values.OrderByDescending(p => p.Message.Timestamp).ToList();
    }

    /// <summary>
    /// Returns the best current opportunity, or null. <paramref name="enabledCharacters"/>
    /// may complete the cluster: at most one own character counts toward the player minimum,
    /// but all fitting characters contribute archetypes and are reported.
    /// </summary>
    public GroupOpportunity? Evaluate(DateTimeOffset now, IEnumerable<GameCharacter> enabledCharacters)
    {
        Prune(now);
        var posts = latestByAdvertiser.Values.ToList();
        if (posts.Count == 0)
        {
            return null;
        }

        var ownCharacters = enabledCharacters.Where(c => c.Class is not null).ToList();

        // Candidate level windows: one anchored at each stated level, plus a final
        // null anchor for the cluster of posts that state no level at all. Posts
        // without a stated level fit any window, and multiboxer posts advertising
        // several characters fit via any of their levels.
        var anchors = posts
            .SelectMany(p => p.StatedLevels)
            .Distinct()
            .OrderBy(l => l)
            .Select(l => (int?)l)
            .Append(null)
            .ToList();

        GroupOpportunity? best = null;
        foreach (var anchor in anchors)
        {
            var candidate = EvaluateWindow(anchor, posts, ownCharacters);
            if (candidate is not null && (best is null || IsBetter(candidate, best)))
            {
                best = candidate;
            }
        }

        return best;
    }

    private GroupOpportunity? EvaluateWindow(
        int? anchor, List<LfgPost> posts, List<GameCharacter> ownCharacters)
    {
        var members = posts.Where(p => FitsWindow(p, anchor)).ToList();
        if (members.Count == 0)
        {
            return null;
        }

        var levels = members
            .SelectMany(p => p.StatedLevels.Where(l => InWindow(l, anchor)))
            .ToList();
        int? min = levels.Count > 0 ? levels.Min() : null;
        int? max = levels.Count > 0 ? levels.Max() : null;

        var own = ownCharacters.Where(c => CharacterFits(c, min, max)).ToList();
        var archetypes = CollectArchetypes(members, own);

        var people = members.Count + Math.Min(1, own.Count);
        if (people < options.MinPlayers || archetypes.Count < options.MinArchetypes)
        {
            return null;
        }

        return new GroupOpportunity
        {
            Posts = members,
            OwnCandidates = own,
            MinLevel = min,
            MaxLevel = max,
            Archetypes = archetypes,
        };
    }

    private bool InWindow(int level, int? anchor) =>
        anchor is not null && level >= anchor && level <= anchor + options.LevelSpread;

    private bool FitsWindow(LfgPost post, int? anchor)
    {
        if (post.StatedLevels.Count == 0 || post.StatedLevels.Any(l => InWindow(l, anchor)))
        {
            return true;
        }

        // A poster offering to mentor can play any level between their mentor floor
        // and their actual level.
        if (options.AllowMentorDown && post.WillMentor && anchor is { } a)
        {
            var floor = post.MentorFloor ?? 1;
            return floor <= a + options.LevelSpread && post.StatedLevels.Max() >= a;
        }

        return false;
    }

    private bool CharacterFits(GameCharacter character, int? min, int? max) =>
        min is null
        || (character.Level is not null
            && character.Level >= min - options.LevelSpread
            && (character.Level <= max + options.LevelSpread || options.AllowMentorDown));

    private static HashSet<Role> CollectArchetypes(
        List<LfgPost> members, List<GameCharacter> own) =>
        members
            .SelectMany(p => p.Classes)
            .Select(cls => ClassCatalog.TryRoleOf(cls, out var r) ? (Role?)r : null)
            .Concat(own.Select(c => c.Role))
            .Where(role => role is not null)
            .Select(role => role!.Value)
            .ToHashSet();

    private static bool IsBetter(GroupOpportunity candidate, GroupOpportunity current)
    {
        var candidatePeople = candidate.Posts.Count + Math.Min(1, candidate.OwnCandidates.Count);
        var currentPeople = current.Posts.Count + Math.Min(1, current.OwnCandidates.Count);
        return candidatePeople > currentPeople
            || (candidatePeople == currentPeople
                && candidate.Archetypes.Count > current.Archetypes.Count);
    }

    public void Clear() => latestByAdvertiser.Clear();

    private void Prune(DateTimeOffset now)
    {
        foreach (var key in latestByAdvertiser
                     .Where(kv => now - kv.Value.Message.Timestamp > options.Window)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            latestByAdvertiser.Remove(key);
        }
    }
}

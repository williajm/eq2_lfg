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

        // Candidate level windows: one anchored at each stated level, plus the
        // "no level information" cluster. Posts without a stated level fit any window;
        // multiboxer posts ("16 Dirge / 47 Conj / 70 Warden LFG") fit via any of their levels.
        var anchors = posts
            .SelectMany(p => p.StatedLevels)
            .Distinct()
            .OrderBy(l => l)
            .Select(l => (int?)l)
            .Append(null)
            .ToList();

        GroupOpportunity? best = null;
        var bestScore = (People: 0, Archetypes: 0);

        foreach (var anchor in anchors)
        {
            bool InWindow(int level) =>
                anchor is not null && level >= anchor && level <= anchor + options.LevelSpread;

            bool Fits(LfgPost p)
            {
                if (p.StatedLevels.Count == 0 || p.StatedLevels.Any(InWindow))
                {
                    return true;
                }

                // "70 fury LFG WILL MENTOR 40+" can play any level between the
                // mentor floor and their actual level.
                if (options.AllowMentorDown && p.WillMentor && anchor is { } a)
                {
                    var floor = p.MentorFloor ?? 1;
                    return floor <= a + options.LevelSpread && p.StatedLevels.Max() >= a;
                }

                return false;
            }

            var members = posts.Where(Fits).ToList();
            if (members.Count == 0)
            {
                continue;
            }

            var levels = members.SelectMany(p => p.StatedLevels.Where(InWindow)).ToList();
            int? min = levels.Count > 0 ? levels.Min() : null;
            int? max = levels.Count > 0 ? levels.Max() : null;

            var own = ownCharacters
                .Where(c => min is null
                    || (c.Level is not null
                        && c.Level >= min - options.LevelSpread
                        && (c.Level <= max + options.LevelSpread || options.AllowMentorDown)))
                .ToList();

            var archetypes = new HashSet<Role>();
            foreach (var role in members
                         .SelectMany(p => p.Classes)
                         .Select(cls => ClassCatalog.TryRoleOf(cls, out var r) ? (Role?)r : null)
                         .Concat(own.Select(c => c.Role)))
            {
                if (role is not null)
                {
                    archetypes.Add(role.Value);
                }
            }

            var people = members.Count + Math.Min(1, own.Count);
            if (people < options.MinPlayers || archetypes.Count < options.MinArchetypes)
            {
                continue;
            }

            var score = (People: people, Archetypes: archetypes.Count);
            if (best is null || score.People > bestScore.People
                || (score.People == bestScore.People && score.Archetypes > bestScore.Archetypes))
            {
                best = new GroupOpportunity
                {
                    Posts = members,
                    OwnCandidates = own,
                    MinLevel = min,
                    MaxLevel = max,
                    Archetypes = archetypes,
                };
                bestScore = score;
            }
        }

        return best;
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

using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Matching;

/// <summary>
/// Suppresses repeat alerts: an advertiser who re-posts the same ad within the cooldown
/// window is not re-alerted unless the ad's content materially changed (different
/// roles/classes/zone — captured by <see cref="LfgPost.Signature"/>).
/// </summary>
public sealed class CooldownTracker(TimeSpan cooldown)
{
    private readonly Dictionary<string, (string Signature, DateTimeOffset LastAlert)> byAdvertiser =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns true if this post should raise an alert now (and records it).</summary>
    public bool ShouldAlert(LfgPost post) => ShouldAlert(post, post.Message.Timestamp);

    public bool ShouldAlert(LfgPost post, DateTimeOffset now)
    {
        if (byAdvertiser.TryGetValue(post.Advertiser, out var previous)
            && previous.Signature == post.Signature
            && now - previous.LastAlert < cooldown)
        {
            return false;
        }

        byAdvertiser[post.Advertiser] = (post.Signature, now);
        return true;
    }

    public void Clear() => byAdvertiser.Clear();
}

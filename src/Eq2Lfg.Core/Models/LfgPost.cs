namespace Eq2Lfg.Core.Models;

public enum PostKind
{
    /// <summary>A group advertising for members ("need tank 2 dps CMM").</summary>
    GroupAd,

    /// <summary>An individual advertising themselves ("52 Warlock LF exp group").</summary>
    PlayerLfg,

    /// <summary>Sales / powerlevel-service / other spam that mentions LFG-ish words.</summary>
    Spam,

    /// <summary>Guild recruitment ("&lt;Guild&gt; is recruiting healers for raiding") — excluded from matching.</summary>
    Recruitment,

    /// <summary>Chat that carries no LFG-relevant content.</summary>
    NotLfg,
}

/// <summary>The analyzed LFG-relevant content of a chat message.</summary>
public sealed record LfgPost
{
    public required PostKind Kind { get; init; }
    public required ChatMessage Message { get; init; }

    /// <summary>Roles the group asked for (group ads).</summary>
    public IReadOnlyList<Role> WantedRoles { get; init; } = [];

    /// <summary>Canonical class names explicitly asked for (group ads) or offered (player posts).</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>Zone the ad referenced, resolved via the zone table.</summary>
    public string? ZoneName { get; init; }
    public int? ZoneMinLevel { get; init; }
    public int? ZoneMaxLevel { get; init; }

    /// <summary>
    /// All levels stated in the message. Multiboxers advertise several characters at once
    /// ("16 Dirge / 47 Conj / 70 Warden LFG"), so a post can carry multiple levels.
    /// </summary>
    public IReadOnlyList<int> StatedLevels { get; init; } = [];

    /// <summary>The first level stated in the message, if any.</summary>
    public int? StatedLevel => StatedLevels.Count > 0 ? StatedLevels[0] : null;

    /// <summary>
    /// Open places the group advertised ("LF2M", "need 2 more", "1 spot left");
    /// null when the ad doesn't say. Group ads only.
    /// </summary>
    public int? SpotsLeft { get; init; }

    /// <summary>The poster offered to mentor down ("70 fury LFG WILL MENTOR 40+").</summary>
    public bool WillMentor { get; init; }

    /// <summary>Lowest level the poster will mentor down to, when stated ("mentor 40+").</summary>
    public int? MentorFloor { get; init; }

    public string Advertiser => Message.Speaker;

    /// <summary>Stable signature of what is being asked for; used for cooldown/material-change checks.</summary>
    public string Signature =>
        string.Join(
            ";",
            Advertiser.ToLowerInvariant(),
            Kind,
            string.Join(",", WantedRoles.Order()),
            string.Join(",", Classes.Order(StringComparer.OrdinalIgnoreCase)),
            ZoneName ?? "",
            StatedLevel?.ToString() ?? "");
}

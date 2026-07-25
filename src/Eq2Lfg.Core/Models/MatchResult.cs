namespace Eq2Lfg.Core.Models;

/// <summary>One of the user's characters matching a group ad, with human-readable reasons.</summary>
public sealed record MatchResult
{
    public required GameCharacter Character { get; init; }
    public required LfgPost Post { get; init; }

    /// <summary>Why this character matched, e.g. ["healer", "Castle Mistmoore 60-70"].</summary>
    public required IReadOnlyList<string> Reasons { get; init; }

    /// <summary>Display form, e.g. "Bramwick (lvl 59 Warden) matches Brakor: "need healer CMM" — healer, Castle Mistmoore 60-70".</summary>
    public override string ToString() =>
        $"{Character.Display} matches {Post.Advertiser}: \"{Post.Message.Text}\" — {string.Join(", ", Reasons)}";
}

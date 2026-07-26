using CommunityToolkit.Mvvm.ComponentModel;
using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.App.ViewModels;

/// <summary>Base for list rows that show a live "2 min ago" age.</summary>
public abstract partial class TimedRow : ObservableObject
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Marks the row as freshly re-seen (repeat ad within cooldown).</summary>
    public void Touch(DateTimeOffset seenAt)
    {
        Timestamp = seenAt;
        RefreshAge();
    }

    public string AgeText =>
        (DateTimeOffset.UtcNow - Timestamp) switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 60 } age => $"{(int)age.TotalMinutes} min ago",
            { TotalHours: < 24 } age => $"{(int)age.TotalHours} h ago",
            var age => $"{(int)age.TotalDays} d ago",
        };

    [ObservableProperty]
    private bool isNew;

    public void RefreshAge() => OnPropertyChanged(nameof(AgeText));
}

/// <summary>A group ad matched by one or more of the user's characters.</summary>
public sealed class MatchRow : TimedRow
{
    public required string CharacterDisplay { get; init; }
    public required Role CharacterRole { get; init; }
    public required string Advertiser { get; init; }
    public required string MessageText { get; init; }
    public required string ReasonsText { get; init; }
    public required string Channel { get; init; }

    /// <summary>Other own characters matching the same ad ("also Fellwick (lvl 44 Fury)").</summary>
    public string? AlsoMatchesText { get; init; }

    public static List<MatchRow> From(LfgPost post, IReadOnlyList<MatchResult> matches)
    {
        var first = matches[0];
        var also = matches.Count > 1
            ? "also " + string.Join(", ", matches.Skip(1).Select(m => m.Character.Display))
            : null;
        return
        [
            new MatchRow
            {
                Timestamp = post.Message.Timestamp,
                CharacterDisplay = first.Character.Display,
                CharacterRole = first.Character.Role ?? Role.Dps,
                Advertiser = post.Advertiser,
                MessageText = post.Message.Text,
                ReasonsText = string.Join(", ", first.Reasons),
                Channel = post.Message.Channel,
                AlsoMatchesText = also,
            },
        ];
    }
}

/// <summary>Any LFG-relevant chat shown in the Traffic view.</summary>
public sealed class TrafficRow : TimedRow
{
    public required PostKind Kind { get; init; }
    public required string Speaker { get; init; }
    public required string MessageText { get; init; }
    public required string Channel { get; init; }
    public required string DetailText { get; init; }

    public string KindText => Kind == PostKind.GroupAd ? "GROUP AD" : "PLAYER LFG";

    public static TrafficRow From(LfgPost post)
    {
        var details = new List<string>();
        if (post.Classes.Count > 0)
        {
            details.Add(string.Join("/", post.Classes));
        }

        if (post.StatedLevels.Count > 0)
        {
            details.Add("lvl " + string.Join("/", post.StatedLevels));
        }

        if (post.ZoneName is not null)
        {
            details.Add($"{post.ZoneName} {post.ZoneMinLevel}-{post.ZoneMaxLevel}");
        }

        if (post.SpotsLeft is { } spots)
        {
            details.Add(spots == 1 ? "1 spot left" : $"{spots} spots left");
        }

        return new TrafficRow
        {
            Timestamp = post.Message.Timestamp,
            Kind = post.Kind,
            Speaker = post.Advertiser,
            MessageText = post.Message.Text,
            Channel = post.Message.Channel,
            DetailText = string.Join(" · ", details),
        };
    }
}

/// <summary>A cluster of LFG players that could form a new group.</summary>
public sealed partial class OpportunityRow : TimedRow
{
    public required string TitleText { get; init; }
    public required string PlayersText { get; init; }
    public required string OwnText { get; init; }

    /// <summary>Ready-to-paste chat line inviting the players to form the group.</summary>
    public required string SuggestedMessage { get; init; }

    [ObservableProperty]
    private bool copied;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CopyMessage()
    {
        try
        {
            System.Windows.Clipboard.SetText(SuggestedMessage);
            Copied = true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Clipboard can be briefly locked by another process; ignore.
        }
    }

    public static OpportunityRow From(GroupOpportunity opportunity)
    {
        var range = opportunity.MinLevel is null
            ? "any level"
            : $"{opportunity.MinLevel}-{opportunity.MaxLevel}";
        var archetypes = string.Join(", ",
            opportunity.Archetypes.Select(a => a.ToString().ToLowerInvariant()));
        return new OpportunityRow
        {
            TitleText =
                $"Potential group — {opportunity.Posts.Count} players LFG ({range}), covers {archetypes}",
            PlayersText = string.Join("   ", opportunity.Posts.Select(p =>
                p.Classes.Count > 0
                    ? $"{p.Advertiser} ({string.Join("/", p.Classes)})"
                    : p.Advertiser)),
            OwnText = opportunity.OwnCandidates.Count > 0
                ? "You could bring: " + string.Join(", ", opportunity.OwnCandidates.Select(c => c.Display))
                : "",
            SuggestedMessage = OpportunityMessage.Compose(opportunity),
        };
    }
}

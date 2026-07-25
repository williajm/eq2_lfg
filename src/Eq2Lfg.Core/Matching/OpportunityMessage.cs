namespace Eq2Lfg.Core.Matching;

public static class OpportunityMessage
{
    /// <summary>
    /// A ready-to-paste chat line inviting the clustered LFG players to form a group, e.g.
    /// "Vex, Dorn — saw you all LFG. I can bring my 59 Warden (Bramwick). Want to form a group?"
    /// </summary>
    public static string Compose(GroupOpportunity opportunity)
    {
        var names = string.Join(", ", opportunity.Posts.Select(p => p.Advertiser));
        var mine = opportunity.OwnCandidates.FirstOrDefault();
        var bring = mine?.Class is null
            ? ""
            : mine.Level is null
                ? $" I can bring my {mine.Class} ({mine.Name})."
                : $" I can bring my {mine.Level} {mine.Class} ({mine.Name}).";
        return $"{names} — saw you all LFG.{bring} Want to form a group?";
    }
}

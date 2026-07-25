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
        var mine = opportunity.OwnCandidates.Count > 0 ? opportunity.OwnCandidates[0] : null;
        return $"{names} — saw you all LFG.{BringText(mine)} Want to form a group?";
    }

    private static string BringText(Models.GameCharacter? mine)
    {
        if (mine?.Class is null)
        {
            return "";
        }

        return mine.Level is null
            ? $" I can bring my {mine.Class} ({mine.Name})."
            : $" I can bring my {mine.Level} {mine.Class} ({mine.Name}).";
    }
}

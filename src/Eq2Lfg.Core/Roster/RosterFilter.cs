using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Roster;

public static class RosterFilter
{
    /// <summary>
    /// Characters eligible for matching: enabled in settings and — because chat channels
    /// are per-server — on the server the active log belongs to (when known).
    /// </summary>
    public static IEnumerable<GameCharacter> Eligible(
        IEnumerable<GameCharacter> roster, AppSettings settings, string? server) =>
        roster.Where(c => settings.IsEnabled(c)
            && (string.IsNullOrEmpty(server)
                || c.Server.Equals(server, StringComparison.OrdinalIgnoreCase)));
}

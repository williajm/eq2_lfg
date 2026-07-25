namespace Eq2Lfg.Core.Models;

/// <summary>One of the user's characters, assembled from the roster inis, Census, and local hints.</summary>
public sealed class GameCharacter
{
    public required string Account { get; init; }
    public required string Server { get; init; }
    public required string Name { get; init; }

    /// <summary>Canonical adventure class name, if known (Census or class-channel hint).</summary>
    public string? Class { get; set; }

    /// <summary>Adventure level, if known.</summary>
    public int? Level { get; set; }

    public string? TradeskillClass { get; set; }
    public int? TradeskillLevel { get; set; }

    /// <summary>Where the class/level came from: "census", "cache", "channel-hint", "log".</summary>
    public string? DataSource { get; set; }

    public DateTimeOffset? LastRefreshedUtc { get; set; }

    public Role? Role =>
        Class is not null && ClassCatalog.TryRoleOf(Class, out var role) ? role : null;

    public string Key => $"{Account}|{Server}|{Name}";

    /// <summary>Display form used in match rows, e.g. "Bramwick (lvl 59 Warden)".</summary>
    public string Display
    {
        get
        {
            if (Class is null)
            {
                return Name;
            }

            return Level is null ? $"{Name} ({Class})" : $"{Name} (lvl {Level} {Class})";
        }
    }
}

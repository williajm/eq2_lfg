using System.Text.Json;

namespace Eq2Lfg.Core.Zones;

/// <summary>A zone with the abbreviations players use in chat and its intended level band.</summary>
public sealed record ZoneEntry
{
    public required string Name { get; init; }
    public required int MinLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required List<string> Abbreviations { get; init; }
}

/// <summary>
/// Abbreviation → zone lookup, persisted as an editable JSON file. Ships seeded for the
/// current Wuoshi (TLE) era — classic through Echoes of Faydwer, level cap 70.
/// </summary>
public sealed class ZoneTable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<string, ZoneEntry> byToken = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ZoneEntry> Entries { get; private set; } = [];

    public ZoneTable(IEnumerable<ZoneEntry> entries)
    {
        Replace(entries);
    }

    public static ZoneTable CreateSeeded() => new(SeedEntries());

    /// <summary>
    /// Loads the table from disk, writing the seed file first if none exists. Seed zones
    /// added in newer app versions are merged in (by name) without touching user edits.
    /// </summary>
    public static ZoneTable LoadOrSeed(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var seeded = CreateSeeded();
            seeded.Save(filePath);
            return seeded;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<ZoneEntry>>(File.ReadAllText(filePath));
            if (entries is { Count: > 0 })
            {
                var known = entries.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var additions = SeedEntries().Where(s => !known.Contains(s.Name)).ToList();
                var table = new ZoneTable(entries.Concat(additions));
                if (additions.Count > 0)
                {
                    table.Save(filePath);
                }

                return table;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Fall through to seed.
        }

        return CreateSeeded();
    }

    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(Entries, JsonOptions));
    }

    public void Replace(IEnumerable<ZoneEntry> entries)
    {
        Entries = entries.ToList();
        byToken.Clear();
        foreach (var entry in Entries)
        {
            byToken[entry.Name] = entry;
            foreach (var abbreviation in entry.Abbreviations)
            {
                byToken[abbreviation] = entry;
            }
        }
    }

    /// <summary>Resolve a single chat token ("cmm", "stormhold") to a zone, or null.</summary>
    public ZoneEntry? Resolve(string token) =>
        byToken.TryGetValue(token.Trim(), out var entry) ? entry : null;

    /// <summary>Finds the first zone referenced anywhere in a message (multi-word names included).</summary>
    public ZoneEntry? FindInText(string text) =>
        Entries.FirstOrDefault(entry =>
            entry.Abbreviations.Append(entry.Name).Any(token => ContainsToken(text, token)));

    private static bool ContainsToken(string text, string token)
    {
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = index == 0 || !IsWordChar(text[index - 1]);
            var end = index + token.Length;
            var afterOk = end >= text.Length || !IsWordChar(text[end]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            index = end;
        }

        return false;
    }

    // Apostrophes are word-internal so "RE" doesn't match inside "We're".
    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'';

    private static List<ZoneEntry> SeedEntries() =>
    [
        new() { Name = "Stormhold", MinLevel = 15, MaxLevel = 30, Abbreviations = ["SH"] },
        new() { Name = "Fallen Gate", MinLevel = 20, MaxLevel = 30, Abbreviations = ["FG"] },
        new() { Name = "Ruins of Varsoon", MinLevel = 25, MaxLevel = 35, Abbreviations = ["RoV", "Varsoon"] },
        new() { Name = "Nektropos Castle", MinLevel = 30, MaxLevel = 40, Abbreviations = ["Nek Castle", "NC", "Nekcastle"] },
        new() { Name = "Runnyeye", MinLevel = 30, MaxLevel = 40, Abbreviations = ["RE", "Runny"] },
        new() { Name = "Deathfist Citadel", MinLevel = 35, MaxLevel = 45, Abbreviations = ["DFC", "Deathfist"] },
        new() { Name = "Crushbone Keep", MinLevel = 25, MaxLevel = 35, Abbreviations = ["CK", "Crushbone"] },
        new() { Name = "Kaladim", MinLevel = 40, MaxLevel = 50, Abbreviations = ["Kal"] },
        new() { Name = "Solusek's Eye", MinLevel = 40, MaxLevel = 50, Abbreviations = ["Sol Eye", "SolEye", "Sols"] },
        new() { Name = "Obelisk of Lost Souls", MinLevel = 40, MaxLevel = 50, Abbreviations = ["OLS", "Obelisk"] },
        new() { Name = "Permafrost", MinLevel = 45, MaxLevel = 55, Abbreviations = ["PF", "Perma"] },
        new() { Name = "Cazic-Thule", MinLevel = 45, MaxLevel = 55, Abbreviations = ["CT", "Cazic"] },
        new() { Name = "The Sanctum of the Scaleborn", MinLevel = 55, MaxLevel = 65, Abbreviations = ["SoS", "Sanctum"] },
        new() { Name = "The Nest of the Great Egg", MinLevel = 60, MaxLevel = 70, Abbreviations = ["Nest"] },
        new() { Name = "The Vaults of El'Arad", MinLevel = 60, MaxLevel = 70, Abbreviations = ["Vaults"] },
        new() { Name = "Den of the Devourer", MinLevel = 65, MaxLevel = 70, Abbreviations = ["DoD", "Den"] },
        new() { Name = "Palace of the Awakened", MinLevel = 65, MaxLevel = 70, Abbreviations = ["PoA", "Palace"] },
        new() { Name = "Halls of Fate", MinLevel = 65, MaxLevel = 70, Abbreviations = ["HoF"] },
        new() { Name = "Mistmoore Catacombs", MinLevel = 55, MaxLevel = 65, Abbreviations = ["MMC", "Catacombs", "Cata"] },
        new() { Name = "Castle Mistmoore", MinLevel = 60, MaxLevel = 70, Abbreviations = ["CMM", "Mistmoore", "Mistmoor"] },
        new() { Name = "The Estate of Unrest", MinLevel = 65, MaxLevel = 70, Abbreviations = ["Unrest"] },
        new() { Name = "Shard of Fear", MinLevel = 65, MaxLevel = 70, Abbreviations = ["SoF"] },
        new() { Name = "Crypt of Valdoon", MinLevel = 65, MaxLevel = 70, Abbreviations = ["Valdoon", "CoV"] },
        new() { Name = "Klak'Anon", MinLevel = 60, MaxLevel = 70, Abbreviations = ["Klak"] },
        new() { Name = "Obelisk of Blight", MinLevel = 60, MaxLevel = 70, Abbreviations = ["OoB", "Blight"] },
        new() { Name = "Wailing Caves", MinLevel = 10, MaxLevel = 20, Abbreviations = ["WC"] },
        new() { Name = "Pillars of Flame", MinLevel = 50, MaxLevel = 60, Abbreviations = ["PoF"] },
        // "Mayong" is the raid's boss (Mayong Mistmoore), used as shorthand for the zone.
        new() { Name = "Mistmoore's Inner Sanctum", MinLevel = 70, MaxLevel = 70, Abbreviations = ["MMIS", "Mayong"] },
        new() { Name = "Freethinker Hideout", MinLevel = 70, MaxLevel = 70, Abbreviations = ["FTH", "Freethinker"] },
    ];
}

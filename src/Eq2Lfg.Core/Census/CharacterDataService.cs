using System.Text.Json;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Roster;

namespace Eq2Lfg.Core.Census;

/// <summary>
/// Fills in class/level for roster characters: Census first, then a local JSON cache,
/// then the class-channel hint from uisettings.xml. Level-up messages seen in the log
/// update the store live via <see cref="ApplyLevelUp"/>.
/// </summary>
public sealed class CharacterDataService(CensusClient censusClient, string cacheFilePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed record CachedEntry(
        string? Class,
        int? Level,
        string? TradeskillClass,
        int? TradeskillLevel,
        DateTimeOffset RefreshedUtc);

    /// <summary>
    /// Populates <paramref name="characters"/> in place. Returns the number of characters
    /// successfully refreshed from Census (0 means fully offline / cache-only).
    /// </summary>
    public async Task<int> PopulateAsync(
        IReadOnlyList<GameCharacter> characters,
        string eq2Directory,
        CancellationToken cancellationToken = default)
    {
        var cache = LoadCache();
        var refreshed = 0;

        foreach (var character in characters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = await censusClient
                .GetCharacterAsync(character.Name, character.Server, cancellationToken)
                .ConfigureAwait(false);

            if (info?.Class is not null)
            {
                character.Class = ClassCatalog.ResolveClass(info.Class) ?? info.Class;
                character.Level = info.Level;
                character.TradeskillClass = info.TradeskillClass;
                character.TradeskillLevel = info.TradeskillLevel;
                character.DataSource = "census";
                character.LastRefreshedUtc = DateTimeOffset.UtcNow;
                cache[character.Key] = new CachedEntry(
                    character.Class, character.Level,
                    character.TradeskillClass, character.TradeskillLevel,
                    character.LastRefreshedUtc.Value);
                refreshed++;
                continue;
            }

            if (cache.TryGetValue(character.Key, out var cached))
            {
                character.Class = cached.Class;
                character.Level = cached.Level;
                character.TradeskillClass = cached.TradeskillClass;
                character.TradeskillLevel = cached.TradeskillLevel;
                character.DataSource = "cache";
                character.LastRefreshedUtc = cached.RefreshedUtc;
                continue;
            }

            var hinted = ClassChannelHint.DetectClass(eq2Directory, character.Server, character.Name);
            if (hinted is not null)
            {
                character.Class = hinted;
                character.DataSource = "channel-hint";
            }
        }

        SaveCache(cache);
        return refreshed;
    }

    /// <summary>Records a level-up observed in the chat log for the active character.</summary>
    public void ApplyLevelUp(GameCharacter character, int newLevel)
    {
        character.Level = newLevel;
        character.DataSource = "log";
        var cache = LoadCache();
        cache[character.Key] = new CachedEntry(
            character.Class, newLevel,
            character.TradeskillClass, character.TradeskillLevel,
            DateTimeOffset.UtcNow);
        SaveCache(cache);
    }

    private Dictionary<string, CachedEntry> LoadCache()
    {
        try
        {
            if (File.Exists(cacheFilePath))
            {
                var json = File.ReadAllText(cacheFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, CachedEntry>>(json) ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupt or unreadable cache: start fresh.
        }

        return [];
    }

    private void SaveCache(Dictionary<string, CachedEntry> cache)
    {
        try
        {
            var dir = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(cacheFilePath, JsonSerializer.Serialize(cache, JsonOptions));
        }
        catch (IOException)
        {
            // Cache persistence is best-effort.
        }
    }
}

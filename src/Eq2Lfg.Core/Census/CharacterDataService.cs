using System.Text.Json;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Roster;

namespace Eq2Lfg.Core.Census;

/// <summary>
/// Fills in class/level for roster characters: Census first, then a local JSON cache,
/// then the class-channel hint from uisettings.xml. Only stale cache entries are
/// re-queried, and characters Census doesn't know (deleted, EU) are remembered so they
/// aren't hammered every refresh. Level-ups seen in the log update the store live via
/// <see cref="ApplyLevelUp"/>.
/// </summary>
public sealed class CharacterDataService(ICensusClient censusClient, string cacheFilePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Pause between Census requests to stay under the anonymous rate limit.</summary>
    public TimeSpan RequestSpacing { get; init; } = TimeSpan.FromSeconds(1.5);

    /// <summary>Wait after a rate-limit error before continuing (the limit window is ~1 minute).</summary>
    public TimeSpan RateLimitBackoff { get; init; } = TimeSpan.FromSeconds(61);

    /// <summary>Known-missing characters are re-checked at most this often.</summary>
    public TimeSpan NotFoundMaxAge { get; init; } = TimeSpan.FromHours(24);

    private sealed record CachedEntry(
        string? Class,
        int? Level,
        string? TradeskillClass,
        int? TradeskillLevel,
        DateTimeOffset RefreshedUtc);

    /// <summary>
    /// Populates <paramref name="characters"/> in place, querying Census only for entries
    /// whose cache is older than <paramref name="maxAge"/>. Returns the number of
    /// characters refreshed from Census this pass.
    /// </summary>
    public async Task<int> PopulateAsync(
        IReadOnlyList<GameCharacter> characters,
        string eq2Directory,
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        var cache = LoadCache();
        var refreshed = 0;
        var rateLimitHits = 0;
        var queried = false;

        foreach (var character in characters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            cache.TryGetValue(character.Key, out var cached);
            if (IsFresh(cached, maxAge) || rateLimitHits >= 2)
            {
                ApplyFallback(character, cached, eq2Directory);
                continue;
            }

            if (queried)
            {
                await Task.Delay(RequestSpacing, cancellationToken).ConfigureAwait(false);
            }

            queried = true;
            var lookup = await censusClient
                .LookupAsync(character.Name, character.Server, cancellationToken)
                .ConfigureAwait(false);

            if (lookup.Status == CensusLookupStatus.Error)
            {
                rateLimitHits++;
                await Task.Delay(RateLimitBackoff, cancellationToken).ConfigureAwait(false);
                lookup = await censusClient
                    .LookupAsync(character.Name, character.Server, cancellationToken)
                    .ConfigureAwait(false);
            }

            switch (lookup)
            {
                case { Status: CensusLookupStatus.Found, Info: { Class: not null } info }:
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
                    break;

                case { Status: CensusLookupStatus.NotFound }:
                    cache[character.Key] = new CachedEntry(
                        null, null, null, null, DateTimeOffset.UtcNow);
                    ApplyFallback(character, cache[character.Key], eq2Directory);
                    break;

                default:
                    rateLimitHits++;
                    ApplyFallback(character, cached, eq2Directory);
                    break;
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

    private bool IsFresh(CachedEntry? entry, TimeSpan maxAge)
    {
        if (entry is null)
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - entry.RefreshedUtc;
        var limit = entry.Class is null
            ? NotFoundMaxAge > maxAge ? NotFoundMaxAge : maxAge
            : maxAge;
        return age < limit;
    }

    private static void ApplyFallback(GameCharacter character, CachedEntry? cached, string eq2Directory)
    {
        if (cached?.Class is not null)
        {
            character.Class = cached.Class;
            character.Level = cached.Level;
            character.TradeskillClass = cached.TradeskillClass;
            character.TradeskillLevel = cached.TradeskillLevel;
            character.DataSource = "cache";
            character.LastRefreshedUtc = cached.RefreshedUtc;
            return;
        }

        if (character.Class is null)
        {
            var hinted = ClassChannelHint.DetectClass(eq2Directory, character.Server, character.Name);
            if (hinted is not null)
            {
                character.Class = hinted;
                character.DataSource = "channel-hint";
            }
        }
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

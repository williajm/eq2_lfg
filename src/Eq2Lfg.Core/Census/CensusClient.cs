using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eq2Lfg.Core.Census;

public sealed record CensusCharacterInfo(
    string Name,
    string World,
    string? Class,
    int? Level,
    string? TradeskillClass,
    int? TradeskillLevel);

/// <summary>
/// Minimal client for the Daybreak Census API (no key required for EQ2 character lookups).
/// </summary>
public sealed class CensusClient(HttpClient httpClient)
{
    private const string BaseUrl = "https://census.daybreakgames.com/json/get/eq2/character/";

    public async Task<CensusCharacterInfo?> GetCharacterAsync(
        string name, string world, CancellationToken cancellationToken = default)
    {
        var url =
            $"{BaseUrl}?name.first_lower={Uri.EscapeDataString(name.ToLowerInvariant())}" +
            $"&locationdata.world={Uri.EscapeDataString(world)}" +
            "&c:show=name,type,locationdata&c:limit=5";

        CensusResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<CensusResponse>(url, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }

        var character = response?.CharacterList?.FirstOrDefault();
        if (character?.Name?.First is null)
        {
            return null;
        }

        return new CensusCharacterInfo(
            character.Name.First,
            character.LocationData?.World ?? world,
            character.Type?.Class,
            character.Type?.Level,
            character.Type?.TsClass,
            character.Type?.TsLevel);
    }

    private sealed class CensusResponse
    {
        [JsonPropertyName("character_list")]
        public List<CensusCharacter>? CharacterList { get; set; }
    }

    private sealed class CensusCharacter
    {
        [JsonPropertyName("name")]
        public CensusName? Name { get; set; }

        [JsonPropertyName("type")]
        public CensusType? Type { get; set; }

        [JsonPropertyName("locationdata")]
        public CensusLocation? LocationData { get; set; }
    }

    private sealed class CensusName
    {
        [JsonPropertyName("first")]
        public string? First { get; set; }
    }

    private sealed class CensusType
    {
        [JsonPropertyName("class")]
        public string? Class { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        [JsonPropertyName("ts_class")]
        public string? TsClass { get; set; }

        [JsonPropertyName("ts_level")]
        public int? TsLevel { get; set; }
    }

    private sealed class CensusLocation
    {
        [JsonPropertyName("world")]
        public string? World { get; set; }
    }
}

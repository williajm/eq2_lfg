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

public enum CensusLookupStatus
{
    /// <summary>Character found; Info is populated.</summary>
    Found,

    /// <summary>Census answered but has no such character on that world.</summary>
    NotFound,

    /// <summary>Rate limited, offline, or malformed response — worth retrying later.</summary>
    Error,
}

public sealed record CensusLookup(CensusLookupStatus Status, CensusCharacterInfo? Info)
{
    public static readonly CensusLookup NotFound = new(CensusLookupStatus.NotFound, null);
    public static readonly CensusLookup Error = new(CensusLookupStatus.Error, null);
}

public interface ICensusClient
{
    Task<CensusLookup> LookupAsync(string name, string world, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal client for the Daybreak Census API. Anonymous access allows roughly 10
/// requests per minute before returning a "Missing Service ID" error; registering a
/// free service ID (https://census.daybreakgames.com) lifts the limit.
/// </summary>
public sealed class CensusClient(HttpClient httpClient, string? serviceId = null) : ICensusClient
{
    private string BaseUrl =>
        string.IsNullOrWhiteSpace(serviceId)
            ? "https://census.daybreakgames.com/json/get/eq2/character/"
            : $"https://census.daybreakgames.com/s:{serviceId.Trim()}/json/get/eq2/character/";

    public async Task<CensusLookup> LookupAsync(
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
            return CensusLookup.Error;
        }

        if (response is null || response.Error is not null)
        {
            return CensusLookup.Error;
        }

        var character = response.CharacterList?.FirstOrDefault();
        if (character?.Name?.First is null)
        {
            return response.CharacterList is null ? CensusLookup.Error : CensusLookup.NotFound;
        }

        return new CensusLookup(
            CensusLookupStatus.Found,
            new CensusCharacterInfo(
                character.Name.First,
                character.LocationData?.World ?? world,
                character.Type?.Class,
                character.Type?.Level,
                character.Type?.TsClass,
                character.Type?.TsLevel));
    }

    private sealed class CensusResponse
    {
        [JsonPropertyName("character_list")]
        public List<CensusCharacter>? CharacterList { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
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

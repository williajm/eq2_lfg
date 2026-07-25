using System.Text.Json;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Config;

/// <summary>
/// User settings persisted as JSON in %LOCALAPPDATA%\Eq2Lfg\settings.json.
/// Availability is a three-level tree (account → server → character); the disabled
/// sets store exceptions so re-enabling a branch restores prior child choices.
/// </summary>
public sealed class AppSettings
{
    public string Eq2Directory { get; set; } = @"E:\games\eq2";

    public bool InAppAlerts { get; set; } = true;
    public bool ToastAlerts { get; set; } = true;
    public bool SoundAlerts { get; set; } = true;

    public int CooldownMinutes { get; set; } = 15;
    public int LevelTolerance { get; set; } = 5;
    public int CensusRefreshMinutes { get; set; } = 60;

    /// <summary>Accounts entirely disabled, e.g. "williajm-eu".</summary>
    public HashSet<string> DisabledAccounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Disabled servers as "account|server".</summary>
    public HashSet<string> DisabledServers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Disabled characters as "account|server|name".</summary>
    public HashSet<string> DisabledCharacters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(GameCharacter character) =>
        !DisabledAccounts.Contains(character.Account)
        && !DisabledServers.Contains($"{character.Account}|{character.Server}")
        && !DisabledCharacters.Contains(character.Key);

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eq2Lfg");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");
    public static string DefaultZonesPath => Path.Combine(DefaultDirectory, "zones.json");
    public static string DefaultCachePath => Path.Combine(DefaultDirectory, "characters.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupt settings: fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}

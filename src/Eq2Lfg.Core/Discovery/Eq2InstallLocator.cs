using System.Text.RegularExpressions;

namespace Eq2Lfg.Core.Discovery;

/// <summary>
/// Pure helpers for finding an EQ2 install; the platform-specific probing
/// (registry, processes, Steam root) lives in the app layer.
/// </summary>
public static partial class Eq2InstallLocator
{
    [GeneratedRegex("""^\s*"path"\s+"(?<path>[^"]+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex SteamLibraryPathRegex();

    /// <summary>A directory is an EQ2 install if the client or its account/roster files are there.</summary>
    public static bool IsValidEq2Directory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            return File.Exists(Path.Combine(path, "EverQuest2.exe"))
                || Directory.EnumerateFiles(path, "*_characters*.ini").Any();
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static string? FirstValid(IEnumerable<string?> candidates) =>
        candidates.FirstOrDefault(IsValidEq2Directory);

    /// <summary>Extracts library root paths from Steam's libraryfolders.vdf.</summary>
    public static IReadOnlyList<string> ParseSteamLibraryPaths(string vdfContent) =>
        SteamLibraryPathRegex().Matches(vdfContent)
            .Select(m => m.Groups["path"].Value.Replace(@"\\", @"\"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Steam installs EQ2 under &lt;library&gt;\steamapps\common\EverQuest II.</summary>
    public static string SteamAppPath(string libraryRoot) =>
        Path.Combine(libraryRoot, "steamapps", "common", "EverQuest II");

    /// <summary>Well-known install locations, expanded per available drive root ("C:\", "E:\", ...).</summary>
    public static IEnumerable<string> CommonCandidates(IEnumerable<string> driveRoots)
    {
        string[] patterns =
        [
            @"Users\Public\Daybreak Game Company\Installed Games\EverQuest II",
            @"Daybreak Game Company\Installed Games\EverQuest II",
            @"Program Files (x86)\Daybreak Game Company\Installed Games\EverQuest II",
            @"Program Files (x86)\Sony\EverQuest II",
            @"Program Files\Sony\EverQuest II",
            @"games\eq2",
            @"games\EverQuest II",
            @"EverQuest II",
        ];
        return driveRoots.SelectMany(root => patterns.Select(p => Path.Combine(root, p)));
    }
}

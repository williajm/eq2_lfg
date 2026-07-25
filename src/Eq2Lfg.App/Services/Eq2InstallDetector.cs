using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Eq2Lfg.Core.Discovery;
using Microsoft.Win32;

namespace Eq2Lfg.App.Services;

/// <summary>
/// Finds the EQ2 install directory by probing, in order: the running game process,
/// Windows uninstall registry entries, Steam libraries, and well-known paths.
/// </summary>
public static class Eq2InstallDetector
{
    public static string? Detect() =>
        Eq2InstallLocator.FirstValid(
            FromRunningProcess()
                .Concat(FromRegistry())
                .Concat(FromSteam())
                .Concat(FromCommonPaths()));

    private static IEnumerable<string?> FromRunningProcess()
    {
        foreach (var process in Process.GetProcessesByName("EverQuest2"))
        {
            string? dir = null;
            try
            {
                dir = Path.GetDirectoryName(process.MainModule?.FileName);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // Access denied or process exited; try the next strategy.
            }

            if (dir is not null)
            {
                yield return dir;
            }
        }
    }

    private static IEnumerable<string?> FromRegistry()
    {
        string[] uninstallRoots =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

        foreach (var root in uninstallRoots)
        {
            using var key = Registry.LocalMachine.OpenSubKey(root);
            if (key is null)
            {
                continue;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey is null)
                {
                    continue;
                }

                var displayName = subKey.GetValue("DisplayName") as string;
                if (displayName?.Contains("EverQuest II", StringComparison.OrdinalIgnoreCase) == true)
                {
                    yield return subKey.GetValue("InstallLocation") as string;
                }
            }
        }
    }

    private static IEnumerable<string?> FromSteam()
    {
        using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (steamKey?.GetValue("SteamPath") is not string steamRoot || steamRoot.Length == 0)
        {
            yield break;
        }

        var vdf = Path.Combine(steamRoot.Replace('/', '\\'), "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
        {
            yield break;
        }

        string content;
        try
        {
            content = File.ReadAllText(vdf);
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var library in Eq2InstallLocator.ParseSteamLibraryPaths(content))
        {
            yield return Eq2InstallLocator.SteamAppPath(library);
        }
    }

    private static IEnumerable<string?> FromCommonPaths()
    {
        var roots = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.RootDirectory.FullName);
        return Eq2InstallLocator.CommonCandidates(roots);
    }
}

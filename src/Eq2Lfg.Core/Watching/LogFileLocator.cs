namespace Eq2Lfg.Core.Watching;

public sealed record ActiveLog(string FilePath, string Server, string CharacterName);

/// <summary>
/// Finds the chat log currently being written. Logs live at
/// <c>&lt;eq2&gt;\logs\&lt;Server&gt;\eq2log_&lt;Name&gt;.txt</c>; the most recently
/// modified one identifies both the file to tail and the character being played.
/// </summary>
public static class LogFileLocator
{
    public static ActiveLog? FindMostRecent(string eq2Directory)
    {
        var logsRoot = Path.Combine(eq2Directory, "logs");
        if (!Directory.Exists(logsRoot))
        {
            return null;
        }

        FileInfo? newest = null;
        foreach (var path in Directory.EnumerateFiles(logsRoot, "eq2log_*.txt", SearchOption.AllDirectories))
        {
            var info = new FileInfo(path);
            if (newest is null || info.LastWriteTimeUtc > newest.LastWriteTimeUtc)
            {
                newest = info;
            }
        }

        if (newest is null)
        {
            return null;
        }

        var character = Path.GetFileNameWithoutExtension(newest.Name)["eq2log_".Length..];
        var server = newest.Directory?.Name ?? "";
        return new ActiveLog(newest.FullName, server, character);
    }
}

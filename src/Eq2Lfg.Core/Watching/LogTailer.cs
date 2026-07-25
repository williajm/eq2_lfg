using System.Text;

namespace Eq2Lfg.Core.Watching;

/// <summary>
/// Polling tail of a live log file. Opens with permissive sharing so EQ2 keeps writing,
/// survives the file being truncated or recreated, and starts at the current end so old
/// history isn't replayed.
/// </summary>
public sealed class LogTailer(string filePath)
{
    private long position = -1;

    public string FilePath { get; } = filePath;

    /// <summary>Reads any complete new lines appended since the last call.</summary>
    public IReadOnlyList<string> ReadNewLines()
    {
        var lines = new List<string>();

        FileStream stream;
        try
        {
            stream = new FileStream(
                FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException)
        {
            return lines;
        }
        catch (UnauthorizedAccessException)
        {
            return lines;
        }

        using (stream)
        {
            if (position < 0 || position > stream.Length)
            {
                // First read, or the file was truncated/rotated: start from the end.
                position = stream.Length;
                return lines;
            }

            stream.Seek(position, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            position = stream.Length;
        }

        return lines;
    }
}

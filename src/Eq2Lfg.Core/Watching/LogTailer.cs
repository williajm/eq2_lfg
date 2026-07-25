using System.Text;

namespace Eq2Lfg.Core.Watching;

/// <summary>
/// Polling tail of a live log file. Opens with permissive sharing so EQ2 keeps writing,
/// survives the file being truncated or recreated, and starts at the current end so old
/// history isn't replayed. Only complete (newline-terminated) lines are consumed: a line
/// split across two writes stays in the file until its terminator arrives, so no ad is
/// ever delivered as unusable fragments.
/// </summary>
public sealed class LogTailer(string filePath)
{
    private long position = -1;

    public string FilePath { get; } = filePath;

    /// <summary>Reads any complete new lines appended since the last call.</summary>
    public IReadOnlyList<string> ReadNewLines()
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        using (stream)
        {
            if (position < 0 || position > stream.Length)
            {
                // First read, or the file was truncated/rotated: start from the end.
                position = stream.Length;
                return [];
            }

            var available = stream.Length - position;
            if (available == 0)
            {
                return [];
            }

            stream.Seek(position, SeekOrigin.Begin);
            var buffer = new byte[available];
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = stream.Read(buffer, read, buffer.Length - read);
                if (chunk == 0)
                {
                    break;
                }

                read += chunk;
            }

            // Consume only up to the last newline; a trailing partial line is left
            // in place for the next poll. Splitting on the byte value of '\n' is
            // safe in UTF-8 (it never appears inside a multi-byte sequence).
            var lastNewline = Array.LastIndexOf(buffer, (byte)'\n', read - 1);
            if (lastNewline < 0)
            {
                return [];
            }

            position += lastNewline + 1;
            var text = Encoding.UTF8.GetString(buffer, 0, lastNewline + 1);
            return text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Length > 0)
                .ToList();
        }
    }
}

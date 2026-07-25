using Eq2Lfg.Core.Models;

namespace Eq2Lfg.Core.Roster;

/// <summary>
/// Discovers the user's characters from the <c>*_characters*.ini</c> files in the EQ2 directory.
/// File name pattern: <c>&lt;account&gt;_characters.ini</c> or <c>&lt;account&gt;_characters-eu.ini</c>;
/// each line: <c>CharacterN=Name,Server</c>.
/// </summary>
public static class RosterLoader
{
    public static IReadOnlyList<GameCharacter> Load(string eq2Directory)
    {
        var characters = new List<GameCharacter>();
        if (!Directory.Exists(eq2Directory))
        {
            return characters;
        }

        foreach (var file in Directory.EnumerateFiles(eq2Directory, "*_characters*.ini"))
        {
            var account = AccountFromFileName(Path.GetFileName(file));
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("Character", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var eq = trimmed.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }

                var parts = trimmed[(eq + 1)..].Split(',', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var name = parts[0].Trim();
                var server = parts[1].Trim();
                if (name.Length == 0 || server.Length == 0)
                {
                    continue;
                }

                characters.Add(new GameCharacter { Account = account, Server = server, Name = name });
            }
        }

        return characters;
    }

    /// <summary>"williajm2_characters.ini" → "williajm2"; "williajm_characters-eu.ini" → "williajm-eu".</summary>
    public static string AccountFromFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var marker = stem.IndexOf("_characters", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return stem;
        }

        var account = stem[..marker];
        var suffix = stem[(marker + "_characters".Length)..];
        return account + suffix;
    }
}

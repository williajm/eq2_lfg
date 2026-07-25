using System.Text.RegularExpressions;

namespace Eq2Lfg.Core.Parsing;

/// <summary>
/// Spots the active character levelling up in raw log lines, e.g.
/// "You have gained a level! You are now level 60!" / "You are now an Artisan!" variants.
/// </summary>
public static partial class LevelUpDetector
{
    [GeneratedRegex(@"You are now level (\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LevelUpRegex();

    public static int? DetectNewLevel(string rawLine)
    {
        var m = LevelUpRegex().Match(rawLine);
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }
}

using System.Reflection;

namespace Eq2Lfg.App;

/// <summary>
/// Human-readable build identity, e.g. "v1.2.0 (abc1234)" — version from the git
/// tag via MinVer, short commit SHA from AssemblyInformationalVersion.
/// </summary>
public static class AppVersion
{
    public static string Display { get; } = Build();

    private static string Build()
    {
        var info = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrEmpty(info))
        {
            return "";
        }

        var plus = info.IndexOf('+');
        if (plus < 0)
        {
            return $"v{info}";
        }

        var sha = info[(plus + 1)..];
        return $"v{info[..plus]} ({sha[..Math.Min(7, sha.Length)]})";
    }
}

using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Roster;

namespace Eq2Lfg.Core.Tests;

public class RosterAndSettingsTests : IDisposable
{
    private readonly string tempDir =
        Directory.CreateTempSubdirectory("eq2lfg-tests").FullName;

    public void Dispose() => Directory.Delete(tempDir, recursive: true);

    [Fact]
    public void Loads_characters_from_all_account_files()
    {
        File.WriteAllText(
            Path.Combine(tempDir, "testacct_characters.ini"),
            "[Characters]\nCharacter0=Fellwick,Wuoshi\nCharacter1=Jacobus,Antonia Bayle\n");
        File.WriteAllText(
            Path.Combine(tempDir, "testacct_characters-eu.ini"),
            "[Characters]\nCharacter0=Lode,Thurgadin\n");

        var roster = RosterLoader.Load(tempDir);

        Assert.Equal(3, roster.Count);
        Assert.Contains(roster, c =>
            c is { Name: "Fellwick", Server: "Wuoshi", Account: "testacct" });
        Assert.Contains(roster, c =>
            c is { Name: "Jacobus", Server: "Antonia Bayle", Account: "testacct" });
        Assert.Contains(roster, c =>
            c is { Name: "Lode", Server: "Thurgadin", Account: "testacct-eu" });
    }

    [Theory]
    [InlineData("williajm_characters.ini", "williajm")]
    [InlineData("williajm2_characters.ini", "williajm2")]
    [InlineData("williajm_characters-eu.ini", "williajm-eu")]
    public void Account_name_derives_from_file_name(string fileName, string expected)
    {
        Assert.Equal(expected, RosterLoader.AccountFromFileName(fileName));
    }

    [Fact]
    public void Class_channel_hint_reads_uisettings()
    {
        File.WriteAllText(
            Path.Combine(tempDir, "Wuoshi_Bramwick_eq2_uisettings.xml"),
            """
            <UISettings>
              <Channel index="2" name="General" />
              <Channel index="3" name="LFG" />
              <Channel index="4" name="Warden" />
            </UISettings>
            """);

        Assert.Equal("Warden", ClassChannelHint.DetectClass(tempDir, "Wuoshi", "Bramwick"));
        Assert.Null(ClassChannelHint.DetectClass(tempDir, "Wuoshi", "Nobody"));
    }

    [Fact]
    public void Availability_tree_disables_by_account_server_and_character()
    {
        var settings = new AppSettings();
        var character = new GameCharacter
        {
            Account = "testacct-eu",
            Server = "Thurgadin",
            Name = "Lode",
        };

        Assert.True(settings.IsEnabled(character));

        settings.DisabledAccounts.Add("testacct-eu");
        Assert.False(settings.IsEnabled(character));

        settings.DisabledAccounts.Clear();
        settings.DisabledServers.Add("testacct-eu|Thurgadin");
        Assert.False(settings.IsEnabled(character));

        settings.DisabledServers.Clear();
        settings.DisabledCharacters.Add("testacct-eu|Thurgadin|Lode");
        Assert.False(settings.IsEnabled(character));
    }

    [Fact]
    public void Settings_round_trip_to_disk()
    {
        var path = Path.Combine(tempDir, "settings.json");
        var settings = new AppSettings { CooldownMinutes = 20, SoundAlerts = false };
        settings.DisabledAccounts.Add("testacct-eu");
        settings.Save(path);

        var loaded = AppSettings.Load(path);

        Assert.Equal(20, loaded.CooldownMinutes);
        Assert.False(loaded.SoundAlerts);
        Assert.Contains("testacct-eu", loaded.DisabledAccounts);
    }
}

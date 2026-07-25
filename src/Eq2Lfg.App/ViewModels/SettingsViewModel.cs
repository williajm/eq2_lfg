using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eq2Lfg.App.Services;
using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Models;
using Eq2Lfg.Core.Zones;

namespace Eq2Lfg.App.ViewModels;

public sealed partial class ZoneRow : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string abbreviations = "";

    [ObservableProperty]
    private int minLevel;

    [ObservableProperty]
    private int maxLevel;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings settings;
    private readonly MonitorService monitor;

    public ObservableCollection<AvailabilityNode> AccountNodes { get; } = [];
    public ObservableCollection<ZoneRow> ZoneRows { get; } = [];

    public SettingsViewModel(AppSettings settings, MonitorService monitor)
    {
        this.settings = settings;
        this.monitor = monitor;

        toastAlerts = settings.ToastAlerts;
        soundAlerts = settings.SoundAlerts;
        inAppAlerts = settings.InAppAlerts;
        cooldownMinutes = settings.CooldownMinutes;
        levelTolerance = settings.LevelTolerance;
        censusRefreshMinutes = settings.CensusRefreshMinutes;
        eq2Directory = settings.Eq2Directory;

        BuildAvailabilityTree();
        BuildZoneRows();
    }

    [ObservableProperty]
    private bool toastAlerts;

    [ObservableProperty]
    private bool soundAlerts;

    [ObservableProperty]
    private bool inAppAlerts;

    [ObservableProperty]
    private int cooldownMinutes;

    [ObservableProperty]
    private int levelTolerance;

    [ObservableProperty]
    private int censusRefreshMinutes;

    [ObservableProperty]
    private string eq2Directory = "";

    partial void OnToastAlertsChanged(bool value)
    {
        settings.ToastAlerts = value;
        Persist();
    }

    partial void OnSoundAlertsChanged(bool value)
    {
        settings.SoundAlerts = value;
        Persist();
    }

    partial void OnInAppAlertsChanged(bool value)
    {
        settings.InAppAlerts = value;
        Persist();
    }

    partial void OnCooldownMinutesChanged(int value)
    {
        settings.CooldownMinutes = Math.Clamp(value, 1, 240);
        Persist();
    }

    partial void OnLevelToleranceChanged(int value)
    {
        settings.LevelTolerance = Math.Clamp(value, 0, 30);
        Persist();
    }

    partial void OnCensusRefreshMinutesChanged(int value)
    {
        settings.CensusRefreshMinutes = Math.Clamp(value, 5, 24 * 60);
        Persist();
    }

    partial void OnEq2DirectoryChanged(string value)
    {
        settings.Eq2Directory = value;
        Persist();
    }

    private void Persist()
    {
        settings.Save(AppSettings.DefaultPath);
        monitor.ApplySettings();
    }

    private void BuildAvailabilityTree()
    {
        AccountNodes.Clear();
        foreach (var accountGroup in monitor.Roster
                     .GroupBy(c => c.Account, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var accountNode = new AvailabilityNode { Label = accountGroup.Key, Depth = 0 };
            foreach (var serverGroup in accountGroup
                         .GroupBy(c => c.Server, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var serverNode = new AvailabilityNode
                {
                    Label = serverGroup.Key,
                    Depth = 1,
                    Parent = accountNode,
                };
                foreach (var character in serverGroup.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var label = character.Class is null
                        ? character.Name
                        : character.Level is null
                            ? $"{character.Name} — {character.Class}"
                            : $"{character.Name} — lvl {character.Level} {character.Class}";
                    var characterNode = new AvailabilityNode
                    {
                        Label = label,
                        Depth = 2,
                        Parent = serverNode,
                    };
                    characterNode.Initialize(settings.IsEnabled(character));
                    var captured = character;
                    characterNode.StateChanged = (_, enabled) => SetCharacterEnabled(captured, enabled);
                    serverNode.Children.Add(characterNode);
                }

                var capturedServer = (accountGroup.Key, serverGroup.Key);
                serverNode.StateChanged = (_, enabled) =>
                    SetServerEnabled(capturedServer.Item1, capturedServer.Item2, enabled);
                accountNode.Children.Add(serverNode);
            }

            var capturedAccount = accountGroup.Key;
            accountNode.StateChanged = (_, enabled) => SetAccountEnabled(capturedAccount, enabled);
            accountNode.RecomputeAfterInitialize();
            AccountNodes.Add(accountNode);
        }
    }

    private void SetCharacterEnabled(GameCharacter character, bool enabled)
    {
        if (enabled)
        {
            settings.DisabledCharacters.Remove(character.Key);
        }
        else
        {
            settings.DisabledCharacters.Add(character.Key);
        }

        Persist();
    }

    private void SetServerEnabled(string account, string server, bool enabled)
    {
        var key = $"{account}|{server}";
        if (enabled)
        {
            settings.DisabledServers.Remove(key);
        }
        else
        {
            settings.DisabledServers.Add(key);
        }

        Persist();
    }

    private void SetAccountEnabled(string account, bool enabled)
    {
        if (enabled)
        {
            settings.DisabledAccounts.Remove(account);
        }
        else
        {
            settings.DisabledAccounts.Add(account);
        }

        Persist();
    }

    private void BuildZoneRows()
    {
        ZoneRows.Clear();
        foreach (var zone in monitor.Zones.Entries)
        {
            ZoneRows.Add(new ZoneRow
            {
                Name = zone.Name,
                Abbreviations = string.Join(", ", zone.Abbreviations),
                MinLevel = zone.MinLevel,
                MaxLevel = zone.MaxLevel,
            });
        }
    }

    [RelayCommand]
    private void AddZone() =>
        ZoneRows.Add(new ZoneRow { Name = "New Zone", Abbreviations = "NZ", MinLevel = 1, MaxLevel = 70 });

    [RelayCommand]
    private void RemoveZone(ZoneRow row) => ZoneRows.Remove(row);

    [RelayCommand]
    private void SaveZones()
    {
        var entries = ZoneRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new ZoneEntry
            {
                Name = r.Name.Trim(),
                MinLevel = Math.Min(r.MinLevel, r.MaxLevel),
                MaxLevel = Math.Max(r.MinLevel, r.MaxLevel),
                Abbreviations = r.Abbreviations
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
            })
            .ToList();
        monitor.Zones.Replace(entries);
        monitor.ApplyZoneChanges();
    }

    /// <summary>Rebuild the tree after roster/census data arrives.</summary>
    public void RefreshCharacters() => BuildAvailabilityTree();
}

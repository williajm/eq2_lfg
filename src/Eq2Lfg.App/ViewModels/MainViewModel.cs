using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Eq2Lfg.App.Services;
using Eq2Lfg.Core.Config;
using Eq2Lfg.Core.Matching;
using Eq2Lfg.Core.Models;

namespace Eq2Lfg.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxMatches = 100;
    private const int MaxTraffic = 250;

    private readonly AppSettings settings;
    private readonly AlertService alerts;
    private readonly DispatcherTimer ageTimer;

    public MonitorService Monitor { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<MatchRow> Matches { get; } = [];
    public ObservableCollection<OpportunityRow> Opportunities { get; } = [];
    public ObservableCollection<TrafficRow> Traffic { get; } = [];

    [ObservableProperty]
    private int selectedTab;

    [ObservableProperty]
    private string statusText = "Starting…";

    [ObservableProperty]
    private string censusText = "Census: not refreshed yet";

    public bool HasNoMatches => Matches.Count == 0 && Opportunities.Count == 0;
    public bool HasNoTraffic => Traffic.Count == 0;

    public MainViewModel(AppSettings settings)
    {
        this.settings = settings;
        Monitor = new MonitorService(settings);
        alerts = new AlertService(settings);
        Settings = new SettingsViewModel(settings, Monitor);

        Monitor.TrafficSeen += OnTraffic;
        Monitor.MatchFound += OnMatch;
        Monitor.OpportunityFound += OnOpportunity;
        Monitor.StatusChanged += OnStatus;

        Matches.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoMatches));
        Opportunities.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoMatches));
        Traffic.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoTraffic));

        ageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        ageTimer.Tick += (_, _) => RefreshAges();
        ageTimer.Start();

        Monitor.Start();
    }

    private void OnTraffic(LfgPost post)
    {
        // Replace an existing row from the same advertiser so the list shows "still active".
        var existing = Traffic.FirstOrDefault(t =>
            t.Speaker.Equals(post.Advertiser, StringComparison.OrdinalIgnoreCase)
            && t.MessageText == post.Message.Text);
        if (existing is not null)
        {
            Traffic.Remove(existing);
        }

        Traffic.Insert(0, TrafficRow.From(post));
        TrimTo(Traffic, MaxTraffic);
    }

    private void OnMatch(LfgPost post, IReadOnlyList<MatchResult> matches, bool shouldAlert)
    {
        if (!shouldAlert)
        {
            var existing = Matches.FirstOrDefault(m =>
                m.Advertiser.Equals(post.Advertiser, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.RefreshAge();
                return;
            }
        }

        foreach (var row in MatchRow.From(post, matches))
        {
            Matches.Insert(0, row);
        }

        TrimTo(Matches, MaxMatches);

        if (shouldAlert)
        {
            alerts.AlertMatches(post, matches);
        }
    }

    private void OnOpportunity(GroupOpportunity opportunity)
    {
        Opportunities.Insert(0, OpportunityRow.From(opportunity));
        while (Opportunities.Count > 5)
        {
            Opportunities.RemoveAt(Opportunities.Count - 1);
        }

        alerts.AlertOpportunity(opportunity);
    }

    private void OnStatus(MonitorStatus status)
    {
        StatusText = status.LogFile is null
            ? $"No active log found under {settings.Eq2Directory}\\logs — waiting for EQ2 (is chat logging on?)"
            : $"Watching {status.LogFile} — playing {status.ActiveCharacter}";
        CensusText = status.LastCensusRefresh is null
            ? $"Roster: {status.RosterCount} characters — Census: waiting"
            : $"Roster: {status.RosterCount} characters — Census: {status.CensusRefreshed} refreshed {status.LastCensusRefresh.Value.ToLocalTime():HH:mm}";
        Settings.RefreshCharacters();
    }

    private void RefreshAges()
    {
        foreach (var row in Matches)
        {
            row.RefreshAge();
            row.IsNew = false;
        }

        foreach (var row in Traffic)
        {
            row.RefreshAge();
        }

        foreach (var row in Opportunities)
        {
            row.RefreshAge();
            row.IsNew = false;
        }
    }

    private static void TrimTo<T>(ObservableCollection<T> list, int max)
    {
        while (list.Count > max)
        {
            list.RemoveAt(list.Count - 1);
        }
    }
}

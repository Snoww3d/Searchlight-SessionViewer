using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Searchlight.Abstractions;
using Searchlight.Diagnostics;
using Searchlight.Models;
using Searchlight.Services;

namespace Searchlight.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the recent-sessions collection,
/// the current selection, the details pane, a text filter, and live refresh via
/// <see cref="ISessionWatcher"/>. Heavy loads run on the thread-pool and marshal
/// back to the UI thread through the injected <see cref="IUiDispatcher"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISessionDataSource _dataSource;
    private readonly ISessionWatcher _watcher;
    private readonly IUiDispatcher _dispatcher;

    private readonly List<SessionInfo> _all = [];
    private bool _watcherHooked;
    private bool _suppressSelectionSideEffects;

    /// <summary>Creates the main view-model with its services and UI dispatcher.</summary>
    public MainViewModel(
        ISessionDataSource dataSource,
        ISessionWatcher watcher,
        DetailsViewModel details,
        SettingsService settings,
        IUiDispatcher dispatcher)
    {
        _dataSource = dataSource;
        _watcher = watcher;
        _dispatcher = dispatcher;
        Details = details;
        Settings = settings;
    }

    /// <summary>The details pane view-model (empty until a row is selected).</summary>
    public DetailsViewModel Details { get; }

    /// <summary>App settings, bound by the Settings flyout and used by resume.</summary>
    public SettingsService Settings { get; }

    /// <summary>Filtered sessions grouped into recency buckets, bound to the list.</summary>
    public ObservableCollection<SessionGroup> SessionGroups { get; } = [];

    /// <summary>The row currently selected in the list.</summary>
    [ObservableProperty]
    private SessionInfo? _selectedSession;

    /// <summary>Free-text filter over name, id, cwd, and branch.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>True while a full reload is in flight.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Count of sessions after filtering, for the status line.</summary>
    [ObservableProperty]
    private int _visibleCount;

    /// <summary>Total sessions discovered, for the status line.</summary>
    [ObservableProperty]
    private int _totalCount;

    partial void OnSelectedSessionChanged(SessionInfo? value)
    {
        // During a list rebuild the ListView transiently clears its SelectedItem
        // (Sessions.Clear -> SelectedItem=null) before we restore it. Ignore that
        // churn so the details pane doesn't flicker or clear on every refresh.
        if (_suppressSelectionSideEffects)
        {
            return;
        }

        Details.Load(value);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>
    /// Loads all sessions (thread-pool) then publishes them on the UI thread.
    /// Also wires the live-refresh watcher on first call.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        CoreLog.Write("LoadAsync: start");
        IsLoading = true;
        try
        {
            IReadOnlyList<SessionInfo> loaded =
                await Task.Run(() => _dataSource.LoadAll()).ConfigureAwait(true);

            CoreLog.Write($"LoadAsync: data source returned {loaded.Count} sessions");

            _all.Clear();
            _all.AddRange(loaded);
            TotalCount = _all.Count;
            ApplyFilter();
            CoreLog.Write($"LoadAsync: published {VisibleCount} rows in {SessionGroups.Count} groups (total {TotalCount})");
        }
        catch (Exception ex)
        {
            CoreLog.Write($"LoadAsync: EXCEPTION {ex}");
        }
        finally
        {
            IsLoading = false;
        }

        HookWatcher();
    }

    /// <summary>Re-runs <see cref="LoadAsync"/> to pick up on-disk changes.</summary>
    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private void HookWatcher()
    {
        if (_watcherHooked)
        {
            return;
        }

        _watcher.Changed += OnWatcherChanged;
        _watcher.Start();
        _watcherHooked = true;
    }

    private void OnWatcherChanged(object? sender, EventArgs e)
    {
        // FileSystemWatcher/timer fire on thread-pool threads; marshal to UI.
        _dispatcher.Post(() =>
        {
            if (!RefreshCommand.IsRunning)
            {
                RefreshCommand.Execute(null);
            }
        });
    }

    private void ApplyFilter()
    {
        string query = SearchText?.Trim() ?? string.Empty;

        IEnumerable<SessionInfo> filtered = _all;
        if (query.Length > 0)
        {
            filtered = _all.Where(s => Matches(s, query));
        }

        // Explicit newest-first ordering so buckets stay contiguous even when
        // workspace updated_at diverges from the folder last-write sort key.
        List<SessionInfo> ordered = [.. filtered.OrderByDescending(s => s.UpdatedAt)];

        string? keepId = SelectedSession?.Id;

        // Suppress the details-pane reload while the ListView churns its
        // SelectedItem through null during the group Clear/re-add rebuild.
        _suppressSelectionSideEffects = true;
        try
        {
            SessionGroups.Clear();

            DateTimeOffset now = DateTimeOffset.Now;
            SessionGroup? current = null;
            string? currentKey = null;

            foreach (SessionInfo session in ordered)
            {
                (string key, string shortKey) = GroupLabelsFor(session.UpdatedAt, now);
                if (current is null || !string.Equals(key, currentKey, StringComparison.Ordinal))
                {
                    current = new SessionGroup(key, shortKey);
                    SessionGroups.Add(current);
                    currentKey = key;
                }

                current.Add(session);
            }

            VisibleCount = ordered.Count;

            // Preserve selection across a filter/refresh when the row survives.
            SessionInfo? match = keepId is null
                ? null
                : ordered.FirstOrDefault(s => s.Id == keepId);
            SelectedSession = match;
        }
        finally
        {
            _suppressSelectionSideEffects = false;
        }

        // Reload the details pane exactly once, reflecting the final selection.
        Details.Load(SelectedSession);
    }

    /// <summary>
    /// Maps a session's last-update time to a group header. Recent sessions fall
    /// into doubling relative windows ("Last 2 hours" … "Last 32 hours"). Older
    /// sessions coarsen over time: 32h–14d are grouped by calendar day, 14d–30d
    /// by calendar week ("Week of …"), and anything ≥30d by calendar month.
    /// Each session lands in the tightest matching window.
    /// </summary>
    internal static string GroupKeyFor(DateTimeOffset updatedAt, DateTimeOffset now)
        => GroupLabelsFor(updatedAt, now).Key;

    /// <summary>
    /// Short label for a session's group, used by the compact tick rail that lets
    /// the user jump straight to a group. Mirrors <see cref="GroupKeyFor"/>'s tiers.
    /// </summary>
    internal static string ShortKeyFor(DateTimeOffset updatedAt, DateTimeOffset now)
        => GroupLabelsFor(updatedAt, now).ShortKey;

    /// <summary>
    /// Single source of truth for the grouping ladder. Returns both the full
    /// header <c>Key</c> and the compact tick-rail <c>ShortKey</c> for a session's
    /// last-update time relative to <paramref name="now"/>.
    /// </summary>
    private static (string Key, string ShortKey) GroupLabelsFor(DateTimeOffset updatedAt, DateTimeOffset now)
    {
        TimeSpan age = now - updatedAt;

        // Hour tiers keep their relative header ("Last N hours") but the compact tick label
        // also carries the weekday letter of the session's last update (e.g. "2h (T)"), matching
        // the day/week ticks. Grouping is keyed off the header, so a varying tick letter never
        // splits an hour bucket; the group shows its most-recent session's weekday.
        string hourDayLetter = " (" + DayLetter(updatedAt.ToLocalTime().DayOfWeek) + ")";

        if (age < TimeSpan.FromHours(2))
        {
            return ("Last 2 hours", "2h" + hourDayLetter);
        }

        if (age < TimeSpan.FromHours(4))
        {
            return ("Last 4 hours", "4h" + hourDayLetter);
        }

        if (age < TimeSpan.FromHours(8))
        {
            return ("Last 8 hours", "8h" + hourDayLetter);
        }

        if (age < TimeSpan.FromHours(16))
        {
            return ("Last 16 hours", "16h" + hourDayLetter);
        }

        if (age < TimeSpan.FromHours(32))
        {
            return ("Last 32 hours", "32h" + hourDayLetter);
        }

        if (age < TimeSpan.FromHours(64))
        {
            return ("Last 64 hours", "64h" + hourDayLetter);
        }

        DateTime local = updatedAt.ToLocalTime().Date;

        // 64h – <14d: group by the session's own calendar day. The tick label restores
        // the abbreviated month + day-number, e.g. "Jul (3) (T)", combined with the
        // single-letter weekday (M T W T F S S); the full date lives in the tooltip.
        if (age < TimeSpan.FromDays(14))
        {
            return (local.ToString("dddd, MMMM d, yyyy"), local.ToString("MMM d") + " (" + DayLetter(local.DayOfWeek) + ")");
        }

        // 14d – <30d: group by calendar week (that week's Monday).
        if (age < TimeSpan.FromDays(30))
        {
            int sinceMonday = ((int)local.DayOfWeek + 6) % 7;
            DateTime weekStart = local.AddDays(-sinceMonday);
            return ("Week of " + weekStart.ToString("MMM d, yyyy"), "Wk " + weekStart.ToString("MMM d"));
        }

        // ≥30d: group by calendar month.
        return (local.ToString("MMMM yyyy"), local.ToString("MMM yyyy"));
    }

    // Single-letter weekday for the compact tick rail: M T W T F S S.
    // There is no .NET format specifier for a one-letter weekday, so map it directly.
    private static string DayLetter(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => "M",
        DayOfWeek.Tuesday => "T",
        DayOfWeek.Wednesday => "W",
        DayOfWeek.Thursday => "T",
        DayOfWeek.Friday => "F",
        DayOfWeek.Saturday => "S",
        DayOfWeek.Sunday => "S",
        _ => "?",
    };

    private static bool Matches(SessionInfo session, string query)
    {
        return Contains(session.DisplayName, query)
            || Contains(session.Id, query)
            || Contains(session.Cwd, query)
            || Contains(session.Branch, query)
            || Contains(session.FirstPromptPreview, query);
    }

    private static bool Contains(string? value, string query) =>
        value is not null && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>Detaches the watcher event.</summary>
    public void Dispose()
    {
        if (_watcherHooked)
        {
            _watcher.Changed -= OnWatcherChanged;
        }
    }
}

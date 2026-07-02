using System.Collections.ObjectModel;

namespace Searchlight.Models;

/// <summary>
/// A titled bucket of sessions for the grouped left-pane list. The
/// <see cref="Key"/> is the header text shown above the group — a relative
/// window ("Last 2 hours" … "Last 32 hours") for recent sessions, or an
/// absolute calendar-day date for sessions older than 32 hours. Sessions
/// remain newest-first within each group, and groups themselves are emitted
/// newest-first by <see cref="ViewModels.MainViewModel"/>.
/// </summary>
public sealed class SessionGroup : ObservableCollection<SessionInfo>
{
    /// <summary>Creates a group with the given header text.</summary>
    public SessionGroup(string key) => Key = key;

    /// <summary>Header text displayed above the group in the list.</summary>
    public string Key { get; }
}

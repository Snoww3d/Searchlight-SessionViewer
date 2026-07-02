using Searchlight.Models;

namespace Searchlight.Services;

/// <summary>
/// Merges every data source into <see cref="SessionInfo"/> records keyed by
/// session UUID. The <see cref="LoadAll"/> pass is cheap: folder scan +
/// <c>workspace.yaml</c> + bulk snapshot-index and journal enrichment. The
/// per-session heavy work (head-parsing <c>events.jsonl</c>) is deferred to
/// <see cref="EnrichWithEvents"/> so a 500-folder scan stays responsive.
/// All access is read-only.
/// </summary>
public sealed class SessionAggregator
{
    private readonly SessionStateScanner _scanner;
    private readonly EventsJsonlReader _eventsReader;
    private readonly SnapshotIndexReader _snapshotReader;
    private readonly JournalReader _journalReader;

    /// <summary>Creates an aggregator over the given readers.</summary>
    public SessionAggregator(
        SessionStateScanner scanner,
        EventsJsonlReader eventsReader,
        SnapshotIndexReader snapshotReader,
        JournalReader journalReader)
    {
        _scanner = scanner;
        _eventsReader = eventsReader;
        _snapshotReader = snapshotReader;
        _journalReader = journalReader;
    }

    /// <summary>
    /// Produces the full recent-sessions list, newest first, with bulk
    /// enrichment (branch, journal activity, snapshot count) applied. Events
    /// head-parsing is NOT performed here — call <see cref="EnrichWithEvents"/>
    /// lazily per selected/visible session.
    /// </summary>
    public IReadOnlyList<SessionInfo> LoadAll()
    {
        IReadOnlyList<SessionInfo> baseList = _scanner.Scan();
        if (baseList.Count == 0)
        {
            return baseList;
        }

        IReadOnlyDictionary<string, SnapshotSummary> snapshots = _snapshotReader.LoadSummaries();
        IReadOnlyDictionary<string, JournalEntry> journal = _journalReader.LoadLatestBySession();

        var enriched = new List<SessionInfo>(baseList.Count);
        foreach (SessionInfo session in baseList)
        {
            enriched.Add(ApplyBulkEnrichment(session, snapshots, journal));
        }

        return enriched;
    }

    /// <summary>
    /// Returns a copy of <paramref name="session"/> with its <c>events.jsonl</c>
    /// head parsed into <see cref="SessionInfo.Start"/>. If already parsed or the
    /// file is absent, the original (or a null-Start copy) is returned unchanged.
    /// </summary>
    public SessionInfo EnrichWithEvents(SessionInfo session)
    {
        if (session.Start is not null)
        {
            return session;
        }

        SessionStartInfo? start = _eventsReader.Read(session.FolderPath);
        return start is null ? session : session with { Start = start };
    }

    private static SessionInfo ApplyBulkEnrichment(
        SessionInfo session,
        IReadOnlyDictionary<string, SnapshotSummary> snapshots,
        IReadOnlyDictionary<string, JournalEntry> journal)
    {
        string? branch = session.Branch;
        int snapshotCount = session.SnapshotCount;
        if (snapshots.TryGetValue(session.Id, out SnapshotSummary? summary))
        {
            branch ??= summary.LatestBranch;
            snapshotCount = summary.Count;
        }

        string? journalActivity = session.JournalActivity;
        if (journal.TryGetValue(session.Id, out JournalEntry? entry))
        {
            journalActivity ??= entry.Activity;
            branch ??= entry.Branch;
        }

        return session with
        {
            Branch = branch,
            SnapshotCount = snapshotCount,
            JournalActivity = journalActivity,
        };
    }
}

namespace Searchlight.Models;

/// <summary>
/// One row from the cross-session status-snapshot index at
/// <c>~/.copilot/status-snapshots/index.db</c> (table <c>snapshots</c>).
/// Used as best-effort enrichment (branch + latest decision point) — only
/// sessions that emitted status blocks appear here.
/// </summary>
public sealed record SnapshotInfo
{
    /// <summary>Auto-increment primary key from the index.</summary>
    public long SnapshotId { get; init; }

    /// <summary>Owning session UUID.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>ISO 8601 timestamp string as stored in the index.</summary>
    public string? TimestampIso { get; init; }

    /// <summary>Parsed form of <see cref="TimestampIso"/>, when valid.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Working directory recorded with the snapshot.</summary>
    public string? Cwd { get; init; }

    /// <summary>Git branch recorded with the snapshot.</summary>
    public string? Branch { get; init; }

    /// <summary>Path to the markdown file holding the snapshot body.</summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Trigger that produced the snapshot, e.g. <c>ask_user</c>, <c>handoff</c>,
    /// <c>task_complete</c>, <c>long_turn</c>, <c>on_demand</c>, <c>checkpoint</c>.
    /// </summary>
    public string? SourceTrigger { get; init; }
}

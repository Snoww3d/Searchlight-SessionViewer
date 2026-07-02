using Searchlight.Models;

namespace Searchlight.Services;

/// <summary>
/// Read-only façade over every per-session data source the UI needs. The live
/// implementation reads the user's real <c>~/.copilot</c> tree; a mock
/// implementation supplies synthetic data for demos, screenshots, and unit
/// tests. This single seam is what decouples the view-models from the
/// filesystem — swap the implementation to swap the entire data backing.
/// </summary>
public interface ISessionDataSource
{
    /// <summary>Full recent-sessions list, bulk-enriched, newest-first not guaranteed (caller sorts).</summary>
    IReadOnlyList<SessionInfo> LoadAll();

    /// <summary>Returns the session with its per-session events head parsed (or unchanged).</summary>
    SessionInfo EnrichWithEvents(SessionInfo session);

    /// <summary>Checkpoints for the given session (newest first).</summary>
    IReadOnlyList<CheckpointInfo> ReadCheckpoints(SessionInfo session);

    /// <summary>Recent status snapshots for the given session id (newest first).</summary>
    IReadOnlyList<SnapshotInfo> LoadSnapshots(string sessionId);

    /// <summary>Todos read from the given session's store.</summary>
    IReadOnlyList<SessionTodo> ReadTodos(SessionInfo session);
}

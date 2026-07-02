using Searchlight.Models;
using Microsoft.Data.Sqlite;

namespace Searchlight.Services;

/// <summary>
/// Reads the cross-session status-snapshot index at
/// <c>~/.copilot/status-snapshots/index.db</c> (table <c>snapshots</c>) to
/// provide best-effort per-session enrichment: the most recent branch and a
/// snapshot count. Opens the database strictly read-only and never writes.
/// </summary>
public sealed class SnapshotIndexReader
{
    /// <summary>
    /// Loads the latest snapshot (by <c>timestamp_iso</c>) and total count for
    /// every session present in the index, keyed by session UUID. Returns an
    /// empty map when the index is missing or unreadable.
    /// </summary>
    public IReadOnlyDictionary<string, SnapshotSummary> LoadSummaries()
    {
        var result = new Dictionary<string, SnapshotSummary>(StringComparer.OrdinalIgnoreCase);
        string dbPath = CopilotPaths.SnapshotIndexDb;
        if (!File.Exists(dbPath))
        {
            return result;
        }

        try
        {
            using var connection = OpenReadOnly(dbPath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT session_id, branch, timestamp_iso, cwd, source_trigger, cnt
                FROM (
                    SELECT
                        session_id, branch, timestamp_iso, cwd, source_trigger,
                        COUNT(*) OVER (PARTITION BY session_id) AS cnt,
                        ROW_NUMBER() OVER (
                            PARTITION BY session_id
                            ORDER BY timestamp_iso DESC
                        ) AS rn
                    FROM snapshots
                )
                WHERE rn = 1;
                """;

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string sessionId = reader.GetString(0);
                if (string.IsNullOrEmpty(sessionId))
                {
                    continue;
                }

                string? timestampIso = reader.IsDBNull(2) ? null : reader.GetString(2);
                DateTimeOffset? timestamp =
                    DateTimeOffset.TryParse(timestampIso, out DateTimeOffset ts) ? ts : null;

                result[sessionId] = new SnapshotSummary
                {
                    SessionId = sessionId,
                    LatestBranch = reader.IsDBNull(1) ? null : reader.GetString(1),
                    LatestTimestampIso = timestampIso,
                    LatestTimestamp = timestamp,
                    LatestCwd = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LatestSourceTrigger = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Count = reader.GetInt32(5),
                };
            }
        }
        catch (SqliteException)
        {
            return result;
        }

        return result;
    }

    /// <summary>
    /// Loads the most recent snapshots for a single session (newest first),
    /// for the details pane. Returns an empty list when the index is missing,
    /// unreadable, or has no rows for the session.
    /// </summary>
    /// <param name="sessionId">Session UUID to query.</param>
    /// <param name="limit">Maximum rows to return.</param>
    public IReadOnlyList<SnapshotInfo> LoadForSession(string sessionId, int limit = 25)
    {
        var result = new List<SnapshotInfo>();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return result;
        }

        string dbPath = CopilotPaths.SnapshotIndexDb;
        if (!File.Exists(dbPath))
        {
            return result;
        }

        try
        {
            using var connection = OpenReadOnly(dbPath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT snapshot_id, session_id, timestamp_iso, cwd, branch,
                       file_path, source_trigger
                FROM snapshots
                WHERE session_id = $id
                ORDER BY timestamp_iso DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$id", sessionId);
            command.Parameters.AddWithValue("$limit", limit);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? timestampIso = reader.IsDBNull(2) ? null : reader.GetString(2);
                DateTimeOffset? timestamp =
                    DateTimeOffset.TryParse(timestampIso, out DateTimeOffset ts) ? ts : null;

                result.Add(new SnapshotInfo
                {
                    SnapshotId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    SessionId = reader.IsDBNull(1) ? sessionId : reader.GetString(1),
                    TimestampIso = timestampIso,
                    Timestamp = timestamp,
                    Cwd = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Branch = reader.IsDBNull(4) ? null : reader.GetString(4),
                    FilePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SourceTrigger = reader.IsDBNull(6) ? null : reader.GetString(6),
                });
            }
        }
        catch (SqliteException)
        {
            return result;
        }

        return result;
    }

    private static SqliteConnection OpenReadOnly(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
        };
        return new SqliteConnection(builder.ConnectionString);
    }
}

/// <summary>Aggregated snapshot enrichment for one session.</summary>
public sealed record SnapshotSummary
{
    /// <summary>Owning session UUID.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Branch on the most recent snapshot, if recorded.</summary>
    public string? LatestBranch { get; init; }

    /// <summary>Raw ISO timestamp of the most recent snapshot.</summary>
    public string? LatestTimestampIso { get; init; }

    /// <summary>Parsed timestamp of the most recent snapshot.</summary>
    public DateTimeOffset? LatestTimestamp { get; init; }

    /// <summary>Working directory on the most recent snapshot.</summary>
    public string? LatestCwd { get; init; }

    /// <summary>Trigger of the most recent snapshot.</summary>
    public string? LatestSourceTrigger { get; init; }

    /// <summary>Total snapshots recorded for the session.</summary>
    public int Count { get; init; }
}

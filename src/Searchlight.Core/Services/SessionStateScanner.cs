using Searchlight.Models;

namespace Searchlight.Services;

/// <summary>
/// Enumerates the per-session folders under <c>~/.copilot/session-state</c> and
/// produces base <see cref="SessionInfo"/> records: folder facts,
/// <c>workspace.yaml</c>, and cheap file-presence flags. Heavy per-session
/// parsing (events.jsonl, SQLite) is deferred to the aggregator so the initial
/// scan of ~500 folders stays fast. Read-only throughout.
/// </summary>
public sealed class SessionStateScanner
{
    private const string ChatPrefix = "optimistic-chat-";

    private readonly WorkspaceYamlReader _workspaceReader;

    /// <summary>Creates a scanner using the given workspace.yaml reader.</summary>
    public SessionStateScanner(WorkspaceYamlReader workspaceReader) =>
        _workspaceReader = workspaceReader;

    /// <summary>
    /// Scans every session folder, newest first. Missing root yields an empty
    /// sequence; individual folder failures are skipped rather than fatal.
    /// </summary>
    public IReadOnlyList<SessionInfo> Scan()
    {
        if (!Directory.Exists(CopilotPaths.SessionState))
        {
            return [];
        }

        var results = new List<SessionInfo>();
        foreach (string folderPath in Directory.EnumerateDirectories(CopilotPaths.SessionState))
        {
            SessionInfo? info = ScanFolder(folderPath);
            if (info is not null)
            {
                results.Add(info);
            }
        }

        results.Sort(static (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
        return results;
    }

    /// <summary>
    /// Builds a base <see cref="SessionInfo"/> for a single folder, or returns
    /// <c>null</c> if the folder is unreadable. Handles empty/fileless
    /// <c>optimistic-chat-*</c> folders gracefully.
    /// </summary>
    public SessionInfo? ScanFolder(string folderPath)
    {
        try
        {
            string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
            bool isChat = folderName.StartsWith(ChatPrefix, StringComparison.OrdinalIgnoreCase);
            string id = isChat ? folderName[ChatPrefix.Length..] : folderName;

            var dirInfo = new DirectoryInfo(folderPath);
            DateTimeOffset lastWrite = dirInfo.LastWriteTimeUtc;

            WorkspaceMetadata? workspace = _workspaceReader.Read(folderPath);

            return new SessionInfo
            {
                Id = id,
                FolderName = folderName,
                FolderPath = folderPath,
                Kind = isChat ? SessionKind.Chat : SessionKind.Project,
                LastWriteTime = lastWrite,
                Workspace = workspace,
                IsInUse = HasLockFile(folderPath),
                HasPlan = HasPlanFile(folderPath),
                HasSessionDb = File.Exists(CopilotPaths.SessionDb(folderPath)),
                HasCheckpoints = HasCheckpointContent(folderPath),
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool HasLockFile(string folderPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(folderPath, "inuse.*.lock", SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool HasPlanFile(string folderPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(folderPath, "plan*.md", SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool HasCheckpointContent(string folderPath)
    {
        string dir = CopilotPaths.CheckpointsDir(folderPath);
        try
        {
            return Directory.Exists(dir) &&
                Directory.EnumerateFileSystemEntries(dir).Any();
        }
        catch (Exception)
        {
            return false;
        }
    }
}

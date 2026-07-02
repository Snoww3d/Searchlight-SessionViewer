using Searchlight.Abstractions;

namespace Searchlight.Services;

/// <summary>
/// Watches <c>~/.copilot/session-state</c> for folder-level changes and raises a
/// single debounced <see cref="Changed"/> event, so the UI can refresh the list
/// without reacting to every individual filesystem notification. The watcher is
/// read-only; it only observes. Debounce coalesces bursts (a session writing many
/// files) into one refresh.
/// </summary>
public sealed class SessionWatcher : ISessionWatcher
{
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly object _gate = new();

    /// <summary>Raised (debounced) when session-state content changes.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Creates a watcher over the session-state root. If the root does not exist,
    /// the watcher stays inert and never raises events.
    /// </summary>
    /// <param name="debounceMilliseconds">Quiet period before a change is reported.</param>
    public SessionWatcher(int debounceMilliseconds = 2000)
    {
        _debounce = new System.Timers.Timer(debounceMilliseconds)
        {
            AutoReset = false,
        };
        _debounce.Elapsed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

        if (!Directory.Exists(CopilotPaths.SessionState))
        {
            return;
        }

        _watcher = new FileSystemWatcher(CopilotPaths.SessionState)
        {
            IncludeSubdirectories = true,
            // Deliberately exclude LastWrite: active sessions append to events.jsonl
            // and .log files constantly, which would flood a full reload every couple
            // seconds and churn the list selection. We only care about structural
            // signals — a session folder appearing/disappearing/renamed, and the
            // inuse.<PID>.lock file being created/removed (the "In use" badge).
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.CreationTime,
        };

        _watcher.Created += OnFsEvent;
        _watcher.Deleted += OnFsEvent;
        _watcher.Renamed += OnFsEvent;
    }

    /// <summary>Begins raising change notifications.</summary>
    public void Start()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (IsStructural(e.FullPath))
        {
            Kick();
        }
    }

    /// <summary>
    /// True only for changes worth a full list refresh: a session folder
    /// appearing / disappearing / being renamed directly under session-state
    /// (depth 0), or an <c>inuse.&lt;PID&gt;.lock</c> file (drives the "In use"
    /// badge). Everything deeper — SQLite <c>-wal</c>/<c>-shm</c>/journal files,
    /// checkpoints, events.jsonl, logs — is transient churn that active sessions
    /// rewrite constantly and must NOT trigger a re-scan of all session folders.
    /// </summary>
    private static bool IsStructural(string fullPath)
    {
        string root = CopilotPaths.SessionState;
        string rel = Path.GetRelativePath(root, fullPath);
        if (string.IsNullOrEmpty(rel) || rel == ".")
        {
            return false;
        }

        int sep = 0;
        foreach (char ch in rel)
        {
            if (ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar)
            {
                sep++;
            }
        }

        // Depth 0 => a direct child of session-state, i.e. a session folder itself.
        if (sep == 0)
        {
            return true;
        }

        // Otherwise only the in-use lock file matters for a live badge update.
        string name = Path.GetFileName(fullPath);
        return name.StartsWith("inuse.", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
    }

    private void Kick()
    {
        lock (_gate)
        {
            _debounce.Stop();
            _debounce.Start();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}

namespace Searchlight.Abstractions;

/// <summary>
/// Watches the Copilot session store and raises <see cref="Changed"/> (debounced) when
/// sessions are added, removed, or updated. Abstracted so the core view-models can be
/// driven by a live <c>FileSystemWatcher</c>, a null watcher (mock/demo), or a test
/// double. Implementations must be safe to <see cref="IDisposable.Dispose"/> once.
/// </summary>
public interface ISessionWatcher : IDisposable
{
    /// <summary>Raised (debounced, off the UI thread) when the session store changes.</summary>
    event EventHandler? Changed;

    /// <summary>Begins watching. No-op if already started.</summary>
    void Start();
}

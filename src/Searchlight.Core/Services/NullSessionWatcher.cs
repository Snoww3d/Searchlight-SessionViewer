using Searchlight.Abstractions;

namespace Searchlight.Services;

/// <summary>
/// Inert <see cref="ISessionWatcher"/> used when live filesystem watching is not
/// wanted (mock/demo mode, headless hosts, tests). Never raises <see cref="Changed"/>.
/// </summary>
public sealed class NullSessionWatcher : ISessionWatcher
{
    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { /* never raised */ }
        remove { /* never raised */ }
    }

    /// <inheritdoc />
    public void Start()
    {
        // Nothing to watch.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to dispose.
    }
}

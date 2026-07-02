using Searchlight.Abstractions;
using Microsoft.UI.Dispatching;

namespace Searchlight.Services;

/// <summary>
/// Windows/WinUI adapter for <see cref="IUiDispatcher"/>. Marshals an <see cref="Action"/>
/// onto the UI thread via the WinUI <see cref="DispatcherQueue"/>, so the platform-neutral
/// core can post work back to the UI without referencing WinUI.
/// </summary>
public sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public DispatcherQueueUiDispatcher(DispatcherQueue queue) => _queue = queue;

    /// <inheritdoc />
    public void Post(Action action) => _queue.TryEnqueue(() => action());
}

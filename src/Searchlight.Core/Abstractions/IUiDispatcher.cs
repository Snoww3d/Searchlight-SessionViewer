namespace Searchlight.Abstractions;

/// <summary>
/// Minimal UI-thread marshaller. The core view-models depend on this instead of a
/// concrete WinUI <c>DispatcherQueue</c> so they stay platform-neutral. The Windows
/// front-end supplies an adapter over <c>DispatcherQueue.TryEnqueue</c>; a headless
/// or test host can supply an inline/no-op implementation.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Marshals <paramref name="action"/> onto the UI thread (fire-and-forget).</summary>
    void Post(Action action);
}

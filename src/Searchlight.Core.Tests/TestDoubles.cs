using Searchlight.Abstractions;

namespace Searchlight.Core.Tests;

/// <summary>
/// Runs posted callbacks inline on the calling thread — a headless stand-in for the
/// WinUI <c>DispatcherQueue</c> adapter so view-model tests need no UI thread.
/// </summary>
internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

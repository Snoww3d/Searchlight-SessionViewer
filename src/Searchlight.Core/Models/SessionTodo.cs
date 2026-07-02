namespace Searchlight.Models;

/// <summary>A single todo row from a session's <c>session.db</c>.</summary>
public sealed record SessionTodo
{
    /// <summary>Stable todo id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Lifecycle status (pending/in_progress/done/blocked).</summary>
    public string Status { get; init; } = string.Empty;
}

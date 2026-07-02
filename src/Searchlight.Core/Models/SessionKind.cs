namespace Searchlight.Models;

/// <summary>
/// Distinguishes the kind of Copilot session represented by a
/// <c>~/.copilot/session-state/&lt;id&gt;</c> folder.
/// </summary>
public enum SessionKind
{
    /// <summary>A project/agent session (folder named with a bare UUID).</summary>
    Project,

    /// <summary>
    /// A lightweight chat session (folder prefixed <c>optimistic-chat-</c>).
    /// </summary>
    Chat,
}

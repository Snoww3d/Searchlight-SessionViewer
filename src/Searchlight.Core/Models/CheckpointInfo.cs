namespace Searchlight.Models;

/// <summary>
/// A checkpoint recorded under
/// <c>~/.copilot/session-state/&lt;id&gt;/checkpoints/</c>. Each checkpoint is a
/// markdown file (<c>NNN-title.md</c>); an <c>index.md</c> may summarize them.
/// </summary>
public sealed record CheckpointInfo
{
    /// <summary>Sequential checkpoint number parsed from the file name.</summary>
    public int Number { get; init; }

    /// <summary>Checkpoint title (from the file name or its heading).</summary>
    public string? Title { get; init; }

    /// <summary>Absolute path to the checkpoint markdown file.</summary>
    public string? FilePath { get; init; }

    /// <summary>Last-write timestamp of the checkpoint file.</summary>
    public DateTimeOffset? Timestamp { get; init; }
}

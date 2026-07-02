namespace Searchlight.Models;

/// <summary>
/// One parsed row from a monthly journal file
/// <c>~/.copilot/journal/&lt;YYYY-MM&gt;.md</c>. Journal rows are a pipe-delimited
/// markdown table: <c>| time | session_id | branch | cwd | activity |</c>.
/// </summary>
public sealed record JournalEntry
{
    /// <summary>Raw time cell (e.g. <c>10:00</c> or a full date row).</summary>
    public string? Time { get; init; }

    /// <summary>Owning session UUID.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Git branch cell, if any.</summary>
    public string? Branch { get; init; }

    /// <summary>Working directory cell.</summary>
    public string? Cwd { get; init; }

    /// <summary>Free-text activity synopsis (may carry markers like <c>[start]</c>).</summary>
    public string? Activity { get; init; }
}

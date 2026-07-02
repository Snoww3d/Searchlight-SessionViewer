using Searchlight.Models;

namespace Searchlight.Services;

/// <summary>
/// Parses the monthly journal files at <c>~/.copilot/journal/&lt;YYYY-MM&gt;.md</c>.
/// Each data row is a pipe-delimited markdown table cell set
/// <c>| time | session_id | branch | cwd | activity |</c>. Header, separator,
/// and date-divider rows are skipped. Rows are append-only and chronological,
/// so the last row seen for a session wins.
/// </summary>
public sealed class JournalReader
{
    /// <summary>
    /// Scans all monthly journal files and returns the most recent journal
    /// entry per session UUID. Returns an empty map when the journal directory
    /// is absent. Best-effort: unreadable files are skipped.
    /// </summary>
    public IReadOnlyDictionary<string, JournalEntry> LoadLatestBySession()
    {
        var latest = new Dictionary<string, JournalEntry>(StringComparer.OrdinalIgnoreCase);
        string journalDir = CopilotPaths.Journal;
        if (!Directory.Exists(journalDir))
        {
            return latest;
        }

        // Sort ascending so later months overwrite earlier ones; within a file
        // rows are already chronological.
        IEnumerable<string> files;
        try
        {
            files = Directory
                .EnumerateFiles(journalDir, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return latest;
        }
        catch (UnauthorizedAccessException)
        {
            return latest;
        }

        foreach (string file in files)
        {
            ParseFile(file, latest);
        }

        return latest;
    }

    private static void ParseFile(string file, Dictionary<string, JournalEntry> latest)
    {
        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(file);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (string line in lines)
        {
            if (line.Length == 0 || line[0] != '|')
            {
                continue;
            }

            JournalEntry? entry = ParseRow(line);
            if (entry is not null)
            {
                latest[entry.SessionId] = entry;
            }
        }
    }

    /// <summary>
    /// Parses one pipe-delimited row. Returns null for header, separator, and
    /// date-divider rows, or any row lacking a real session id.
    /// </summary>
    private static JournalEntry? ParseRow(string line)
    {
        // "| a | b | c | d | e |" → split yields ["", " a ", " b ", " c ", " d ", " e ", ""].
        string[] parts = line.Split('|');
        if (parts.Length < 7)
        {
            return null;
        }

        string time = parts[1].Trim();
        string sessionId = parts[2].Trim();
        string branch = parts[3].Trim();
        string cwd = parts[4].Trim();
        string activity = parts[5].Trim();

        // Skip header row and markdown separator/divider rows.
        if (sessionId.Length == 0 ||
            sessionId is "session_id" or "---" ||
            sessionId.StartsWith("---", StringComparison.Ordinal))
        {
            return null;
        }

        return new JournalEntry
        {
            Time = time.Length == 0 ? null : time,
            SessionId = sessionId,
            Branch = branch.Length == 0 ? null : branch,
            Cwd = cwd.Length == 0 ? null : cwd,
            Activity = activity.Length == 0 ? null : activity,
        };
    }
}

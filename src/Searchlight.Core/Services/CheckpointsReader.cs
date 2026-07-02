using System.Text.RegularExpressions;
using Searchlight.Models;

namespace Searchlight.Services;

/// <summary>
/// Enumerates the checkpoint markdown files under a session's
/// <c>checkpoints</c> folder (<c>NNN-title.md</c>). Read-only; missing or
/// unreadable folders degrade to an empty list.
/// </summary>
public sealed partial class CheckpointsReader
{
    /// <summary>
    /// Lists checkpoints for the session rooted at <paramref name="folderPath"/>,
    /// ordered by their leading number (descending — newest first). The
    /// <c>index.md</c> summary file is skipped.
    /// </summary>
    public IReadOnlyList<CheckpointInfo> Read(string folderPath)
    {
        string dir = CopilotPaths.CheckpointsDir(folderPath);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        // Full checkpoint titles live in the index.md table; the per-file names are
        // truncated (~30 chars) by the checkpoint writer. Prefer the index title so
        // the UI shows the complete description.
        Dictionary<int, string> indexTitles = ReadIndexTitles(dir);

        var checkpoints = new List<CheckpointInfo>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*.md"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(name, "index", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Match match = CheckpointName().Match(name);
                int number = match.Success ? int.Parse(match.Groups["num"].Value) : 0;
                string? title = match.Success
                    ? match.Groups["title"].Value.Replace('-', ' ').Trim()
                    : name;

                // Use the fuller index.md title when available (falls back to the
                // truncated filename-derived title otherwise).
                if (indexTitles.TryGetValue(number, out string? fullTitle)
                    && !string.IsNullOrWhiteSpace(fullTitle))
                {
                    title = fullTitle;
                }

                DateTimeOffset? timestamp = null;
                try
                {
                    timestamp = File.GetLastWriteTime(file);
                }
                catch (IOException)
                {
                    // Ignore; leave timestamp null.
                }

                checkpoints.Add(new CheckpointInfo
                {
                    Number = number,
                    Title = string.IsNullOrWhiteSpace(title) ? name : title,
                    FilePath = file,
                    Timestamp = timestamp,
                });
            }
        }
        catch (IOException)
        {
            return checkpoints;
        }
        catch (UnauthorizedAccessException)
        {
            return checkpoints;
        }

        checkpoints.Sort((a, b) => b.Number.CompareTo(a.Number));
        return checkpoints;
    }

    /// <summary>
    /// Parses <c>checkpoints/index.md</c>'s markdown table into a map of checkpoint
    /// number to full (untruncated) title. Missing/unreadable index → empty map.
    /// </summary>
    private static Dictionary<int, string> ReadIndexTitles(string dir)
    {
        var titles = new Dictionary<int, string>();
        string indexPath = Path.Combine(dir, "index.md");
        if (!File.Exists(indexPath))
        {
            return titles;
        }

        try
        {
            foreach (string line in File.ReadLines(indexPath))
            {
                Match row = IndexRow().Match(line);
                if (row.Success && int.TryParse(row.Groups["num"].Value, out int number))
                {
                    titles[number] = row.Groups["title"].Value.Trim();
                }
            }
        }
        catch (IOException)
        {
            // Best-effort; return whatever parsed so far.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort; return whatever parsed so far.
        }

        return titles;
    }

    [GeneratedRegex(@"^(?<num>\d+)[-_]?(?<title>.*)$")]
    private static partial Regex CheckpointName();

    // Matches an index.md table row: "| 12 | Some full title | 012-some-full-title.md |"
    // (skips the header "| # | Title | File |" and the "|---|---|---|" separator).
    [GeneratedRegex(@"^\s*\|\s*(?<num>\d+)\s*\|\s*(?<title>.*?)\s*\|")]
    private static partial Regex IndexRow();
}

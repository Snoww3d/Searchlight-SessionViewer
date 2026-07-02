using System.Text.Json;
using Searchlight.Models;

namespace Searchlight.Services;

/// <summary>
/// Head-parses a session's <c>events.jsonl</c> into a <see cref="SessionStartInfo"/>.
/// Reads at most <see cref="MaxLines"/> leading lines so the full multi-hundred-KB
/// log is never materialized. Extracts the <c>session.start</c> baseline, tracks the
/// latest <c>session.model_change</c> seen in-window, and captures the first
/// <c>user.message</c> content as a prompt preview. Read-only and null-safe.
/// </summary>
public sealed class EventsJsonlReader
{
    /// <summary>Upper bound on lines scanned per session (keeps the scan bounded).</summary>
    public const int MaxLines = 2000;

    private const int PreviewLength = 2000;

    /// <summary>
    /// Parses the head of <c>events.jsonl</c> in <paramref name="folderPath"/>, or
    /// returns <c>null</c> when the file is absent or has no <c>session.start</c>.
    /// </summary>
    public SessionStartInfo? Read(string folderPath)
    {
        string path = CopilotPaths.EventsJsonl(folderPath);
        if (!File.Exists(path))
        {
            return null;
        }

        string? copilotVersion = null;
        string? contextTier = null;
        string? producer = null;
        DateTimeOffset? startTime = null;
        string? cwd = null;
        bool alreadyInUse = false;
        string? model = null;
        string? reasoningEffort = null;
        string? firstPrompt = null;
        bool haveStart = false;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string? line;
            int count = 0;
            while ((line = reader.ReadLine()) is not null && count < MaxLines)
            {
                count++;
                if (line.Length == 0)
                {
                    continue;
                }

                JsonElement root;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    root = doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    continue;
                }

                if (!root.TryGetProperty("type", out JsonElement typeEl) ||
                    typeEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                switch (typeEl.GetString())
                {
                    case "session.start":
                        ParseStart(
                            root, ref copilotVersion, ref contextTier, ref producer,
                            ref startTime, ref cwd, ref alreadyInUse, ref model,
                            ref reasoningEffort);
                        haveStart = true;
                        break;

                    case "session.model_change":
                        ParseModelChange(root, ref model, ref reasoningEffort);
                        break;

                    case "user.message" when firstPrompt is null:
                        firstPrompt = ExtractFirstPrompt(root);
                        break;
                }
            }
        }
        catch (IOException)
        {
            return haveStart ? Build() : null;
        }

        return haveStart ? Build() : null;

        SessionStartInfo Build() => new()
        {
            CopilotVersion = copilotVersion,
            ContextTier = contextTier,
            Producer = producer,
            StartTime = startTime,
            Cwd = cwd,
            AlreadyInUse = alreadyInUse,
            Model = model,
            ReasoningEffort = reasoningEffort,
            FirstUserPrompt = firstPrompt,
        };
    }

    private static void ParseStart(
        JsonElement root, ref string? copilotVersion, ref string? contextTier,
        ref string? producer, ref DateTimeOffset? startTime, ref string? cwd,
        ref bool alreadyInUse, ref string? model, ref string? reasoningEffort)
    {
        // session.start places its fields directly under "data".
        JsonElement data = root.TryGetProperty("data", out JsonElement d) ? d : root;

        copilotVersion = GetString(data, "copilotVersion") ?? copilotVersion;
        contextTier = GetString(data, "contextTier") ?? contextTier;
        producer = GetString(data, "producer") ?? producer;
        cwd = GetContextCwd(data) ?? cwd;
        model = GetString(data, "selectedModel") ?? model;
        reasoningEffort = GetString(data, "reasoningEffort") ?? reasoningEffort;

        if (data.TryGetProperty("startTime", out JsonElement st) &&
            st.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(st.GetString(), out DateTimeOffset parsed))
        {
            startTime = parsed;
        }

        if (data.TryGetProperty("alreadyInUse", out JsonElement au) &&
            (au.ValueKind == JsonValueKind.True || au.ValueKind == JsonValueKind.False))
        {
            alreadyInUse = au.GetBoolean();
        }
    }

    private static void ParseModelChange(
        JsonElement root, ref string? model, ref string? reasoningEffort)
    {
        JsonElement data = root.TryGetProperty("data", out JsonElement d) ? d : root;
        model = GetString(data, "newModel") ?? model;
        reasoningEffort = GetString(data, "reasoningEffort") ?? reasoningEffort;
    }

    private static string? ExtractFirstPrompt(JsonElement root)
    {
        if (!root.TryGetProperty("data", out JsonElement data) ||
            !data.TryGetProperty("content", out JsonElement content) ||
            content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = content.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > PreviewLength ? text[..PreviewLength] + "\u2026" : text;
    }

    private static string? GetContextCwd(JsonElement data)
    {
        if (data.TryGetProperty("context", out JsonElement ctx) &&
            ctx.ValueKind == JsonValueKind.Object)
        {
            return GetString(ctx, "cwd");
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}

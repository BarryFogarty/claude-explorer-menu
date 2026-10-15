using System.Text.Json;

namespace ClaudeMenu;

internal sealed record ModelEntry(string Label, string Flag);

internal sealed class MenuConfig
{
    public List<ModelEntry> Models { get; init; } = [new("Opus", "opus"), new("Sonnet", "sonnet")];
    public int RecentCount { get; init; } = 8;
    public string Shell { get; init; } = "pwsh";
    public string Terminal { get; init; } = "wt";
    public List<string> ExtraArgs { get; init; } = [];

    /// <summary>"native" (real separator item) or "line" (disabled item drawn with box characters).</summary>
    public string SeparatorStyle { get; init; } = "native";

    /// <summary>
    /// The repository's config.json, found by walking up from the DLL (native\dist -> native -> repo root),
    /// so a fresh clone works without copying anything. Null when not found (defaults are used).
    /// </summary>
    public static string? Path
    {
        get
        {
            string? dir = ModulePath.Directory;
            for (int i = 0; i < 5 && dir is not null; i++, dir = System.IO.Path.GetDirectoryName(dir))
            {
                string candidate = System.IO.Path.Combine(dir, "config.json");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }

    /// <summary>Loads ~/.claude-menu.json; any missing or malformed piece falls back to its default.</summary>
    public static MenuConfig Load()
    {
        var defaults = new MenuConfig();
        try
        {
            string? path = Path;
            if (path is null)
                return defaults;

            using var doc = JsonDocument.Parse(File.ReadAllBytes(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            JsonElement root = doc.RootElement;

            var models = new List<ModelEntry>();
            if (root.TryGetProperty("models", out var m) && m.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in m.EnumerateArray())
                {
                    string? label = e.TryGetProperty("label", out var l) ? l.GetString() : null;
                    string? flag = e.TryGetProperty("flag", out var f) ? f.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(flag))
                        models.Add(new ModelEntry(label, flag));
                }
            }

            var extra = new List<string>();
            if (root.TryGetProperty("extraArgs", out var x) && x.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in x.EnumerateArray())
                {
                    if (e.GetString() is { Length: > 0 } s)
                        extra.Add(s);
                }
            }

            int recent = root.TryGetProperty("recentCount", out var r) && r.TryGetInt32(out int n) ? Math.Clamp(n, 0, 30) : defaults.RecentCount;

            return new MenuConfig
            {
                Models = root.TryGetProperty("models", out _) ? models : defaults.Models,
                RecentCount = recent,
                Shell = root.TryGetProperty("shell", out var sh) && sh.GetString() is { Length: > 0 } shell ? shell : defaults.Shell,
                Terminal = root.TryGetProperty("terminal", out var t) && t.GetString() is { Length: > 0 } term ? term : defaults.Terminal,
                ExtraArgs = extra,
                SeparatorStyle = root.TryGetProperty("separatorStyle", out var ss) && ss.GetString() == "line" ? "line" : "native",
            };
        }
        catch (Exception ex)
        {
            Log.Write($"Config load failed, using defaults: {ex.Message}");
            return defaults;
        }
    }
}

/// <summary>Opt-in diagnostics: writes only while %LOCALAPPDATA%\ClaudeMenu\debug exists.</summary>
internal static class Log
{
    private static readonly string Dir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeMenu");

    public static void Write(string message)
    {
        try
        {
            if (!File.Exists(System.IO.Path.Combine(Dir, "debug")))
                return;
            File.AppendAllText(System.IO.Path.Combine(Dir, "log.txt"), $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // diagnostics must never affect the menu
        }
    }

    public static string CacheDir => Dir;
}

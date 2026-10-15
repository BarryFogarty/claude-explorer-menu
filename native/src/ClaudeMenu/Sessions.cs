using System.Text;
using System.Text.Json;

namespace ClaudeMenu;

internal readonly record struct SessionInfo(string Id, string Title, DateTime LastWriteUtc, string Path);

/// <summary>Chats to list, plus the ids of this folder's chats the user has hidden.</summary>
internal sealed record RecentChats(List<SessionInfo> Chats, List<string> HiddenIds);

/// <summary>
/// Finds the newest Claude Code sessions for a folder and labels them.
/// Label = last custom-title (/rename), else last ai-title, else first user prompt.
/// </summary>
internal static class Sessions
{
    private const int TailBytes = 64 * 1024;
    private const int HeadBytes = 32 * 1024;
    private const int MaxScanned = 200;

    private static string ProjectsRoot
    {
        get
        {
            string? custom = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            string root = string.IsNullOrWhiteSpace(custom)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
                : custom;
            return Path.Combine(root, "projects");
        }
    }

    public static RecentChats Recent(string folder, int count)
    {
        var result = new RecentChats([], []);
        if (count <= 0)
            return result;

        try
        {
            var files = new List<FileInfo>();
            foreach (string dir in ProjectDirs(folder))
                files.AddRange(new DirectoryInfo(dir).EnumerateFiles("*.jsonl"));

            files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

            HashSet<string> hidden = HiddenChats.Load();
            result.HiddenIds.AddRange(files.Select(f => System.IO.Path.GetFileNameWithoutExtension(f.Name)).Where(hidden.Contains));

            // Non-interactive runs and sessions with no title/prompt are skipped, so look well past the first N.
            // Skipped files are cached too, so the extra scanning is only paid once per file change.
            int scanned = 0;
            foreach (FileInfo file in files)
            {
                if (result.Chats.Count >= count || scanned++ >= MaxScanned)
                    break;

                string id = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                if (hidden.Contains(id))
                    continue;

                string? title = TitleCache.GetOrRead(file, ReadTitle);
                if (title is null)
                    continue;

                result.Chats.Add(new SessionInfo(id, title, file.LastWriteTimeUtc, file.FullName));
            }

            TitleCache.Save();
        }
        catch (Exception ex)
        {
            Log.Write($"Recent failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Claude Code names a project directory after the folder path. Current versions replace every
    /// non-alphanumeric char with '-'; older ones kept '_' - accept either if the directory exists.
    /// </summary>
    private static IEnumerable<string> ProjectDirs(string folder)
    {
        string trimmed = folder.Length > 3 ? folder.TrimEnd('\\', '/') : folder;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Sanitize(trimmed, keepUnderscore: false),
            Sanitize(trimmed, keepUnderscore: true),
        };

        foreach (string name in names)
        {
            string dir = Path.Combine(ProjectsRoot, name);
            if (Directory.Exists(dir))
                yield return dir;
        }
    }

    private static string Sanitize(string path, bool keepUnderscore)
    {
        var sb = new StringBuilder(path.Length);
        foreach (char c in path)
            sb.Append(char.IsAsciiLetterOrDigit(c) || (keepUnderscore && c == '_') ? c : '-');
        return sb.ToString();
    }

    /// <summary>Label for an interactive session, or null when it should not be listed (non-interactive or empty).</summary>
    private static string? ReadTitle(FileInfo file)
    {
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            // `claude --print` / SDK runs record entrypoint "sdk-*"; the CLI's own resume list omits them.
            string head = ReadHead(stream);
            if (IsNonInteractive(head))
                return null;

            return TitleFromTail(stream) ?? FirstPrompt(head);
        }
        catch (Exception ex)
        {
            Log.Write($"ReadTitle failed for {file.Name}: {ex.Message}");
            return null;
        }
    }

    private static string? TitleFromTail(FileStream stream)
    {
        int length = (int)Math.Min(stream.Length, TailBytes);
        stream.Seek(-length, SeekOrigin.End);
        string text = ReadText(stream, length);

        string? ai = null;
        string[] lines = text.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i];
            if (line.Length < 20 || line[0] != '{')
                continue;

            if (line.Contains("\"type\":\"custom-title\"", StringComparison.Ordinal))
            {
                if (JsonString(line, "customTitle") is { } custom)
                    return custom;
            }
            else if (ai is null && line.Contains("\"type\":\"ai-title\"", StringComparison.Ordinal))
            {
                ai = JsonString(line, "aiTitle");
            }
        }

        return ai;
    }

    private static string ReadHead(FileStream stream)
    {
        int length = (int)Math.Min(stream.Length, HeadBytes);
        stream.Seek(0, SeekOrigin.Begin);
        return ReadText(stream, length);
    }

    private static bool IsNonInteractive(string head)
    {
        const string key = "\"entrypoint\":\"";
        int start = head.IndexOf(key, StringComparison.Ordinal);
        return start >= 0 && head.AsSpan(start + key.Length).StartsWith("sdk", StringComparison.Ordinal);
    }

    private static string? FirstPrompt(string head)
    {
        foreach (string line in head.Split('\n'))
        {
            if (line.Length < 20 || line[0] != '{' || !line.Contains("\"type\":\"user\"", StringComparison.Ordinal))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("isMeta", out var meta) && meta.ValueKind == JsonValueKind.True)
                    continue;
                if (!doc.RootElement.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
                    continue;

                string? prompt = content.ValueKind == JsonValueKind.String ? content.GetString() : FirstText(content);
                if (!string.IsNullOrWhiteSpace(prompt) && prompt[0] != '<')
                    return prompt;
            }
            catch (JsonException)
            {
                // truncated line at the window edge
            }
        }

        return null;
    }

    private static string? FirstText(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var type) && type.GetString() == "text" && part.TryGetProperty("text", out var text))
                return text.GetString();
        }

        return null;
    }

    private static string? JsonString(string line, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty(property, out var p) ? p.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadText(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        int read = 0;
        while (read < length)
        {
            int n = stream.Read(buffer, read, length - read);
            if (n <= 0)
                break;
            read += n;
        }

        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}

/// <summary>
/// Title cache persisted to %LOCALAPPDATA%\ClaudeMenu\titles.tsv. An entry is valid only while the
/// file's last-write time and length are unchanged, so renames and new messages invalidate it.
/// </summary>
internal static class TitleCache
{
    // Bump the version in the file name whenever ReadTitle's rules change, so stale labels are not reused.
    private static readonly string CacheFile = Path.Combine(Log.CacheDir, "titles.v2.tsv");
    private static Dictionary<string, (long Ticks, long Length, string? Title)>? _entries;
    private static bool _dirty;

    public static string? GetOrRead(FileInfo file, Func<FileInfo, string?> read)
    {
        _entries ??= Load();

        string key = file.FullName;
        if (_entries.TryGetValue(key, out var hit) && hit.Ticks == file.LastWriteTimeUtc.Ticks && hit.Length == file.Length)
            return hit.Title;

        string? title = read(file);
        _entries[key] = (file.LastWriteTimeUtc.Ticks, file.Length, title);
        _dirty = true;
        return title;
    }

    public static void Save()
    {
        if (!_dirty || _entries is null)
            return;

        try
        {
            Directory.CreateDirectory(Log.CacheDir);

            // Drop entries for files that no longer exist so the cache cannot grow without bound.
            var sb = new StringBuilder();
            foreach (var (path, e) in _entries)
            {
                if (!File.Exists(path))
                    continue;
                sb.Append(path).Append('\t').Append(e.Ticks).Append('\t').Append(e.Length).Append('\t')
                  .Append(e.Title is null ? "\\0" : Escape(e.Title)).Append('\n');
            }

            string temp = CacheFile + "." + Environment.ProcessId + ".tmp";
            File.WriteAllText(temp, sb.ToString());
            File.Move(temp, CacheFile, overwrite: true);
            _dirty = false;
        }
        catch (Exception ex)
        {
            Log.Write($"Cache save failed: {ex.Message}");
        }
    }

    private static Dictionary<string, (long, long, string?)> Load()
    {
        var map = new Dictionary<string, (long, long, string?)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(CacheFile))
                return map;

            foreach (string line in File.ReadLines(CacheFile))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 4 && long.TryParse(parts[1], out long ticks) && long.TryParse(parts[2], out long length))
                    map[parts[0]] = (ticks, length, parts[3] == "\\0" ? null : Unescape(parts[3]));
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Cache load failed: {ex.Message}");
            map.Clear();
        }

        return map;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "");

    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char next = s[++i];
                sb.Append(next == 't' ? '\t' : next == 'n' ? '\n' : next);
            }
            else
            {
                sb.Append(s[i]);
            }
        }

        return sb.ToString();
    }
}

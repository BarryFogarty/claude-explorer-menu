using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ClaudeMenu;

/// <summary>The top-level "Claude" entry; builds its submenu on demand for the right-clicked folder.</summary>
[GeneratedComClass]
internal sealed partial class RootCommand : IExplorerCommand, IObjectWithSite
{
    private const int MaxTitle = 60;

    private nint _site;
    private string? _folder;

    public int GetTitle(nint items, out nint name)
    {
        name = Native.AllocString("Claude");
        return Hr.Ok;
    }

    public int GetIcon(nint items, out nint icon)
    {
        string path = Path.Combine(ModulePath.Directory, "claude-glyph.ico");
        icon = File.Exists(path) ? Native.AllocString(path) : 0;
        return icon == 0 ? Hr.NotImpl : Hr.Ok;
    }

    public int GetToolTip(nint items, out nint tip)
    {
        tip = 0;
        return Hr.NotImpl;
    }

    public int GetCanonicalName(out Guid guid)
    {
        guid = Ids.Clsid;
        return Hr.Ok;
    }

    public int GetState(nint items, int okToBeSlow, out int state)
    {
        _folder = Folder.FromItems(items) ?? _folder;
        state = 0; // ECS_ENABLED
        return Hr.Ok;
    }

    public int Invoke(nint items, nint bindCtx) => Hr.Ok;

    public int GetFlags(out int flags)
    {
        flags = ExplorerFlags.HasSubCommands;
        return Hr.Ok;
    }

    public int EnumSubCommands(out nint enumerator)
    {
        enumerator = 0;
        try
        {
            string? folder = _folder ?? Folder.FromSite(_site);
            Log.Write($"EnumSubCommands folder={folder ?? "(none)"}");
            if (folder is null)
                return Hr.Fail;

            var config = MenuConfig.Load();
            var children = new List<ChildCommand>();

            foreach (var model in config.Models)
                children.Add(new ChildCommand(model.Label, () => Launcher.Start(folder, $"--model {model.Flag}", config)));

            children.Add(new ChildCommand("Resume...", () => Launcher.Start(folder, "--resume", config)));

            var recent = Sessions.Recent(folder, config.RecentCount);
            if (recent.Chats.Count > 0)
            {
                children.Add(ChildCommand.Separator(config));
                foreach (var session in recent.Chats)
                {
                    children.Add(new ChildCommand(
                        ChatLabel(session),
                        () => ChatActions.Click(session, folder, config),
                        tooltip: "Click to resume. Shift+click hides it from this list. Ctrl+click moves it to the Recycle Bin."));
                }
            }

            if (recent.HiddenIds.Count > 0)
            {
                string label = $"Show {recent.HiddenIds.Count} hidden chat{(recent.HiddenIds.Count == 1 ? "" : "s")}";
                children.Add(new ChildCommand(label, () => HiddenChats.Remove(recent.HiddenIds)));
            }

            enumerator = Native.GetInterface(new CommandEnumerator(children), Ids.IEnumExplorerCommand);
            return enumerator == 0 ? Hr.Fail : Hr.Ok;
        }
        catch (Exception ex)
        {
            Log.Write($"EnumSubCommands failed: {ex}");
            return Hr.Fail;
        }
    }

    public int SetSite(nint site)
    {
        if (_site != 0)
            Marshal.Release(_site);

        _site = site;
        if (_site != 0)
            Marshal.AddRef(_site);

        return Hr.Ok;
    }

    public int GetSite(in Guid riid, out nint site)
    {
        site = 0;
        if (_site == 0)
            return Hr.NoInterface;

        Guid iid = riid;
        return Marshal.QueryInterface(_site, in iid, out site);
    }

    private static string ChatLabel(SessionInfo session)
    {
        string title = session.Title.ReplaceLineEndings(" ").Trim();
        if (title.Length > MaxTitle)
            title = title[..(MaxTitle - 1)].TrimEnd() + "…";

        // '&' marks an accelerator in menu text; double it to show a literal ampersand.
        return $"{title.Replace("&", "&&")}  ({Age(DateTime.UtcNow - session.LastWriteUtc)})";
    }

    private static string Age(TimeSpan age) => age.TotalMinutes < 1 ? "now"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m"
        : age.TotalDays < 1 ? $"{(int)age.TotalHours}h"
        : age.TotalDays < 14 ? $"{(int)age.TotalDays}d"
        : $"{(int)(age.TotalDays / 7)}w";
}

[GeneratedComClass]
internal sealed partial class ChildCommand(string title, Action? action = null, string? tooltip = null, bool isSeparator = false, bool disabled = false) : IExplorerCommand
{
    private const int StateDisabled = 1;

    /// <summary>
    /// A menu divider. "native" is a real separator item (ECF_ISSEPARATOR); "line" is a disabled item drawn
    /// with box characters, for Windows builds whose flyout renders native separators as a blank gap.
    /// </summary>
    public static ChildCommand Separator(MenuConfig config) =>
        config.SeparatorStyle == "line"
            ? new(new string('─', 30), disabled: true)
            : new("", isSeparator: true);

    public int GetTitle(nint items, out nint name)
    {
        if (isSeparator)
        {
            name = 0;
            return Hr.NotImpl;
        }

        name = Native.AllocString(title);
        return Hr.Ok;
    }

    public int GetIcon(nint items, out nint icon)
    {
        icon = 0;
        return Hr.NotImpl;
    }

    public int GetToolTip(nint items, out nint tip)
    {
        tip = tooltip is null ? 0 : Native.AllocString(tooltip);
        return tooltip is null ? Hr.NotImpl : Hr.Ok;
    }

    public int GetCanonicalName(out Guid guid)
    {
        guid = Guid.Empty;
        return Hr.NotImpl;
    }

    public int GetState(nint items, int okToBeSlow, out int state)
    {
        state = disabled ? StateDisabled : 0;
        return Hr.Ok;
    }

    public int Invoke(nint items, nint bindCtx)
    {
        if (isSeparator || disabled || action is null)
            return Hr.Ok;

        try
        {
            action();
            return Hr.Ok;
        }
        catch (Exception ex)
        {
            Log.Write($"Invoke failed: {ex}");
            return Hr.Fail;
        }
    }

    public int GetFlags(out int flags)
    {
        flags = isSeparator ? ExplorerFlags.IsSeparator : 0;
        return Hr.Ok;
    }

    public int EnumSubCommands(out nint enumerator)
    {
        enumerator = 0;
        return Hr.NotImpl;
    }
}

[GeneratedComClass]
internal sealed unsafe partial class CommandEnumerator(List<ChildCommand> commands) : IEnumExplorerCommand
{
    private int _index;

    public int Next(uint count, nint* items, uint* fetched)
    {
        uint done = 0;
        while (done < count && _index < commands.Count)
        {
            items[done] = Native.GetInterface(commands[_index], Ids.IExplorerCommand);
            _index++;
            done++;
        }

        if (fetched != null)
            *fetched = done;

        return done == count ? Hr.Ok : Hr.False;
    }

    public int Skip(uint count)
    {
        _index = (int)Math.Min((long)_index + count, commands.Count);
        return _index < commands.Count ? Hr.Ok : Hr.False;
    }

    public int Reset()
    {
        _index = 0;
        return Hr.Ok;
    }

    public int Clone(out nint clone)
    {
        clone = 0;
        return Hr.NotImpl;
    }
}

internal static unsafe class ModulePath
{
    private static string? _directory;

    /// <summary>Directory containing ClaudeMenu.dll (where claude-glyph.ico is shipped).</summary>
    public static string Directory => _directory ??= Resolve();

    private static string Resolve()
    {
        const uint FromAddress = 0x4, UnchangedRefCount = 0x2;
        nint address = (nint)(delegate* unmanaged<Guid*, Guid*, nint*, int>)&Exports.DllGetClassObject;

        if (!Native.GetModuleHandleEx(FromAddress | UnchangedRefCount, address, out nint module))
            return AppContext.BaseDirectory;

        char* buffer = stackalloc char[1024];
        uint length = Native.GetModuleFileName(module, buffer, 1024);
        return length == 0 ? AppContext.BaseDirectory : Path.GetDirectoryName(new string(buffer, 0, (int)length)) ?? AppContext.BaseDirectory;
    }
}

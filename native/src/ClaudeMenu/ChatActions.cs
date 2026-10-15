using System.Runtime.InteropServices;

namespace ClaudeMenu;

/// <summary>Chat ids the user has hidden from the menu, persisted in %LOCALAPPDATA%\ClaudeMenu\hidden.txt (one id per line).</summary>
internal static class HiddenChats
{
    private static readonly string File = Path.Combine(Log.CacheDir, "hidden.txt");

    public static HashSet<string> Load()
    {
        try
        {
            return System.IO.File.Exists(File)
                ? System.IO.File.ReadLines(File).Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Write($"Hidden load failed: {ex.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Add(string id) => Update(set => set.Add(id));

    public static void Remove(IEnumerable<string> ids) => Update(set => set.ExceptWith(ids));

    private static void Update(Action<HashSet<string>> change)
    {
        try
        {
            HashSet<string> set = Load();
            change(set);

            Directory.CreateDirectory(Log.CacheDir);
            string temp = File + "." + Environment.ProcessId + ".tmp";
            System.IO.File.WriteAllLines(temp, set.Order());
            System.IO.File.Move(temp, File, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Write($"Hidden update failed: {ex.Message}");
        }
    }
}

/// <summary>What clicking a chat does: resume it, or with a modifier key hide it / send it to the Recycle Bin.</summary>
internal static class ChatActions
{
    private const int VkShift = 0x10, VkControl = 0x11;

    public static bool ShiftHeld => IsDown(VkShift);

    public static bool ControlHeld => IsDown(VkControl);

    private static bool IsDown(int key) => (Native.GetAsyncKeyState(key) & 0x8000) != 0;

    public static void Click(SessionInfo session, string folder, MenuConfig config)
    {
        // Ctrl wins over Shift: the destructive gesture must be the deliberate one.
        if (ControlHeld)
        {
            Log.Write($"Recycle {session.Path}");
            Recycle(session.Path);
        }
        else if (ShiftHeld)
        {
            Log.Write($"Hide {session.Id}");
            HiddenChats.Add(session.Id);
        }
        else
        {
            Launcher.Start(folder, $"--resume {session.Id}", config);
        }
    }

    /// <summary>Moves a file to the Recycle Bin (restorable from there), without prompting.</summary>
    private static void Recycle(string path)
    {
        const uint Delete = 0x3, AllowUndo = 0x40, NoConfirmation = 0x10, Silent = 0x4, NoErrorUi = 0x400;

        // pFrom is a double-null-terminated list.
        nint from = Marshal.StringToHGlobalUni(path + "\0\0");
        try
        {
            var op = new Native.ShFileOpStruct
            {
                wFunc = Delete,
                pFrom = from,
                fFlags = (ushort)(AllowUndo | NoConfirmation | Silent | NoErrorUi),
            };

            int result = Native.SHFileOperation(ref op);
            if (result != 0)
                Log.Write($"SHFileOperation failed: 0x{result:X}");
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
    }
}

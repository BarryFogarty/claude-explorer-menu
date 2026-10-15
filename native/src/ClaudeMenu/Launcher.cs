using System.Runtime.InteropServices;

namespace ClaudeMenu;

internal static class Launcher
{
    /// <summary>Opens the configured terminal in <paramref name="folder"/> running `claude <paramref name="claudeArgs"/>`.</summary>
    public static void Start(string folder, string claudeArgs, MenuConfig config)
    {
        // A trailing backslash (drive root) would escape the closing quote in -d "...".
        string workDir = folder.EndsWith('\\') ? folder + "." : folder;

        string extra = config.ExtraArgs.Count > 0 ? " " + string.Join(' ', config.ExtraArgs) : "";
        string claude = ($"claude {claudeArgs}{extra}").Trim();
        string commandLine = $"\"{ResolveTerminal(config.Terminal)}\" -d \"{workDir}\" {config.Shell} -NoExit -Command {claude}";

        Log.Write($"Launch: {commandLine} (cwd {folder})");

        var startup = new Native.StartupInfo { cb = (uint)Marshal.SizeOf<Native.StartupInfo>() };
        if (!Native.CreateProcess(null, commandLine, 0, 0, false, 0, 0, folder, ref startup, out var info))
        {
            Log.Write($"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            return;
        }

        Native.CloseHandle(info.hThread);
        Native.CloseHandle(info.hProcess);
    }

    private static string ResolveTerminal(string terminal)
    {
        if (terminal.Contains('\\'))
            return terminal;

        string alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", terminal + ".exe");
        return File.Exists(alias) ? alias : terminal + ".exe";
    }
}

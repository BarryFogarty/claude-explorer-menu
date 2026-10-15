using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ClaudeMenu;

internal static class Ids
{
    public static readonly Guid Clsid = new("6b4c1f6e-8f6d-4a51-9a52-3c1d2e7b9a10");

    public static readonly Guid IExplorerCommand = new("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9");
    public static readonly Guid IEnumExplorerCommand = new("a88826f8-186f-4987-aade-ea0cef8fbfe8");
    public static readonly Guid IObjectWithSite = new("fc4801a3-2ba9-11cf-a229-00aa003d7352");
    public static readonly Guid IServiceProvider = new("6d5140c1-7436-11ce-8034-00aa006009fa");
    public static readonly Guid IShellBrowser = new("000214e2-0000-0000-c000-000000000046");
    public static readonly Guid IFolderView = new("cde725b0-ccc9-4519-917e-325d72fab4ce");
    public static readonly Guid IPersistFolder2 = new("1ac3d9f0-175c-11d1-95be-00609797ea4f");
    public static readonly Guid SidTopLevelBrowser = new("4c96be40-915c-11cf-99d3-00aa004ae837");
}

internal static class Hr
{
    public const int Ok = 0;
    public const int False = 1;
    public const int NotImpl = unchecked((int)0x80004001);
    public const int NoInterface = unchecked((int)0x80004002);
    public const int Fail = unchecked((int)0x80004005);
    public const int ClassNotAvailable = unchecked((int)0x80040111);
    public const int NoAggregation = unchecked((int)0x80040110);
}

internal static class ExplorerFlags
{
    public const int HasSubCommands = 0x1;
    public const int IsSeparator = 0x8;
}

[GeneratedComInterface]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
internal partial interface IExplorerCommand
{
    [PreserveSig] int GetTitle(nint items, out nint name);
    [PreserveSig] int GetIcon(nint items, out nint icon);
    [PreserveSig] int GetToolTip(nint items, out nint tip);
    [PreserveSig] int GetCanonicalName(out Guid guid);
    [PreserveSig] int GetState(nint items, int okToBeSlow, out int state);
    [PreserveSig] int Invoke(nint items, nint bindCtx);
    [PreserveSig] int GetFlags(out int flags);
    [PreserveSig] int EnumSubCommands(out nint enumerator);
}

[GeneratedComInterface]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
internal unsafe partial interface IEnumExplorerCommand
{
    [PreserveSig] int Next(uint count, nint* commands, uint* fetched);
    [PreserveSig] int Skip(uint count);
    [PreserveSig] int Reset();
    [PreserveSig] int Clone(out nint clone);
}

[GeneratedComInterface]
[Guid("fc4801a3-2ba9-11cf-a229-00aa003d7352")]
internal partial interface IObjectWithSite
{
    [PreserveSig] int SetSite(nint site);
    [PreserveSig] int GetSite(in Guid riid, out nint site);
}

[GeneratedComInterface]
[Guid("00000001-0000-0000-c000-000000000046")]
internal partial interface IClassFactory
{
    [PreserveSig] int CreateInstance(nint outer, in Guid riid, out nint instance);
    [PreserveSig] int LockServer(int lockServer);
}

internal static partial class Native
{
    public static readonly StrategyBasedComWrappers Wrappers = new();

    /// <summary>Returns an AddRef'd interface pointer for a managed COM object.</summary>
    public static nint GetInterface(object obj, Guid iid)
    {
        nint unknown = Wrappers.GetOrCreateComInterfaceForObject(obj, CreateComInterfaceFlags.None);
        try
        {
            return Marshal.QueryInterface(unknown, in iid, out nint ptr) == 0 ? ptr : 0;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    public static nint AllocString(string value) => Marshal.StringToCoTaskMemUni(value);

    [LibraryImport("shell32.dll", EntryPoint = "SHGetPathFromIDListEx", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool SHGetPathFromIDListEx(nint pidl, char* path, uint pathLen, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetModuleHandleEx(uint flags, nint address, out nint module);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial uint GetModuleFileName(nint module, char* fileName, uint size);

    [StructLayout(LayoutKind.Sequential)]
    public struct StartupInfo
    {
        public uint cb;
        public nint lpReserved, lpDesktop, lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public nint hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateProcess(
        string? applicationName, [MarshalAs(UnmanagedType.LPWStr)] string commandLine,
        nint processAttributes, nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags,
        nint environment, string? currentDirectory,
        ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    public struct ShFileOpStruct
    {
        public nint hwnd;
        public uint wFunc;
        public nint pFrom, pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public nint hNameMappings, lpszProgressTitle;
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHFileOperationW")]
    public static partial int SHFileOperation(ref ShFileOpStruct operation);
}

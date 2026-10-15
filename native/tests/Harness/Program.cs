// Loads ClaudeMenu.dll the way Explorer would (DllGetClassObject -> IClassFactory -> IExplorerCommand)
// and prints the submenu it builds for a folder. Usage: Harness <dll> <folder>
using System.Diagnostics;
using System.Runtime.InteropServices;

unsafe
{
    if (args.Length < 1)
    {
        Console.WriteLine("Usage: Harness <path-to-ClaudeMenu.dll> [folder]");
        return;
    }

    string dll = Path.GetFullPath(args[0]);
    string folder = args.Length > 1 ? args[1] : Environment.CurrentDirectory;

    nint module = NativeLibrary.Load(dll);
    var getClassObject = (delegate* unmanaged<Guid*, Guid*, nint*, int>)NativeLibrary.GetExport(module, "DllGetClassObject");

    Guid clsid = new("6b4c1f6e-8f6d-4a51-9a52-3c1d2e7b9a10");
    Guid iidFactory = new("00000001-0000-0000-c000-000000000046");
    Guid iidCommand = new("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9");
    Guid iidShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    Guid iidShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");

    for (int run = 1; run <= 3; run++)
    {
        var sw = Stopwatch.StartNew();

        nint factory = 0;
        Check(getClassObject(&clsid, &iidFactory, &factory), "DllGetClassObject");
        nint command = 0;
        var createInstance = (delegate* unmanaged<nint, nint, Guid*, nint*, int>)(*(nint**)factory)[3];
        Check(createInstance(factory, 0, &iidCommand, &command), "CreateInstance");

        nint item = 0, array = 0;
        Check(SHCreateItemFromParsingName(folder, 0, &iidShellItem, &item), "SHCreateItemFromParsingName");
        Check(SHCreateShellItemArrayFromShellItem(item, &iidShellItemArray, &array), "SHCreateShellItemArrayFromShellItem");

        nint* vt = *(nint**)command;
        int state;
        Check(((delegate* unmanaged<nint, nint, int, int*, int>)vt[7])(command, array, 0, &state), "GetState");

        nint enumerator = 0;
        Check(((delegate* unmanaged<nint, nint*, int>)vt[10])(command, &enumerator), "EnumSubCommands");

        var lines = new List<string>();
        nint* evt = *(nint**)enumerator;
        nint* children = stackalloc nint[64];
        uint fetched = 0;
        ((delegate* unmanaged<nint, uint, nint*, uint*, int>)evt[3])(enumerator, 64, children, &fetched);
        for (int i = 0; i < fetched; i++)
        {
            nint* cvt = *(nint**)children[i];
            nint title = 0;
            int flags = 0;
            ((delegate* unmanaged<nint, nint, nint*, int>)cvt[3])(children[i], 0, &title);
            ((delegate* unmanaged<nint, int*, int>)cvt[9])(children[i], &flags);
            lines.Add($"{((flags & 0x20) != 0 ? "---\n" : "")}{Marshal.PtrToStringUni(title)}");
            Marshal.FreeCoTaskMem(title);
            Marshal.Release(children[i]);
        }

        sw.Stop();
        Console.WriteLine($"run {run}: {sw.Elapsed.TotalMilliseconds:F1} ms, {fetched} entries");
        if (run == 1)
            lines.ForEach(Console.WriteLine);

        Marshal.Release(enumerator);
        Marshal.Release(array);
        Marshal.Release(item);
        Marshal.Release(command);
        Marshal.Release(factory);
    }
}

static void Check(int hr, string what)
{
    if (hr < 0)
        throw new InvalidOperationException($"{what} failed: 0x{hr:X8}");
}

[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
static extern unsafe int SHCreateItemFromParsingName(string path, nint bindContext, Guid* riid, nint* item);

[DllImport("shell32.dll")]
static extern unsafe int SHCreateShellItemArrayFromShellItem(nint item, Guid* riid, nint* array);

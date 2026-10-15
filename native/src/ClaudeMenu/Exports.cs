using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ClaudeMenu;

[GeneratedComClass]
internal sealed partial class ClassFactory : IClassFactory
{
    public int CreateInstance(nint outer, in Guid riid, out nint instance)
    {
        instance = 0;
        if (outer != 0)
            return Hr.NoAggregation;

        object command = new RootCommand();
        nint unknown = Native.Wrappers.GetOrCreateComInterfaceForObject(command, CreateComInterfaceFlags.None);
        try
        {
            Guid iid = riid;
            return Marshal.QueryInterface(unknown, in iid, out instance);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    public int LockServer(int lockServer) => Hr.Ok;
}

public static unsafe class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static int DllGetClassObject(Guid* clsid, Guid* riid, nint* ppv)
    {
        if (ppv == null)
            return Hr.Fail;

        *ppv = 0;
        try
        {
            if (*clsid != Ids.Clsid)
                return Hr.ClassNotAvailable;

            nint unknown = Native.Wrappers.GetOrCreateComInterfaceForObject(new ClassFactory(), CreateComInterfaceFlags.None);
            try
            {
                Guid iid = *riid;
                return Marshal.QueryInterface(unknown, in iid, out *ppv);
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"DllGetClassObject failed: {ex}");
            return Hr.Fail;
        }
    }

    // Never unload: the surrogate process is short-lived and reloading managed state is not worth the risk.
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow() => Hr.False;
}

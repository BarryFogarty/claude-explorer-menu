using System.Runtime.InteropServices;

namespace ClaudeMenu;

/// <summary>Resolves the folder the user right-clicked from either a shell item array or the host site.</summary>
internal static unsafe class Folder
{
    private const uint SigdnFileSysPath = 0x80058000;

    /// <summary>Path of the first item in an IShellItemArray, or null.</summary>
    public static string? FromItems(nint items)
    {
        if (items == 0)
            return null;

        try
        {
            nint* vt = *(nint**)items;

            uint count = 0;
            var getCount = (delegate* unmanaged<nint, uint*, int>)vt[7];
            if (getCount(items, &count) != 0 || count < 1)
                return null;

            nint item = 0;
            var getItemAt = (delegate* unmanaged<nint, uint, nint*, int>)vt[8];
            if (getItemAt(items, 0, &item) != 0 || item == 0)
                return null;

            try
            {
                nint* itemVt = *(nint**)item;
                nint name = 0;
                var getDisplayName = (delegate* unmanaged<nint, uint, nint*, int>)itemVt[5];
                if (getDisplayName(item, SigdnFileSysPath, &name) != 0 || name == 0)
                    return null;

                try
                {
                    return Marshal.PtrToStringUni(name);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(name);
                }
            }
            finally
            {
                Marshal.Release(item);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"FromItems failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Current folder of the Explorer window that hosts the menu (used for background right-clicks):
    /// site -> IServiceProvider -> IShellBrowser -> active view -> IFolderView -> IPersistFolder2.
    /// </summary>
    public static string? FromSite(nint site)
    {
        if (site == 0)
            return null;

        nint serviceProvider = 0, browser = 0, view = 0, folderView = 0, persist = 0, pidl = 0;
        try
        {
            Guid iidSp = Ids.IServiceProvider;
            if (Marshal.QueryInterface(site, in iidSp, out serviceProvider) != 0)
                return null;

            Guid sid = Ids.SidTopLevelBrowser;
            Guid iidBrowser = Ids.IShellBrowser;
            nint* spVt = *(nint**)serviceProvider;
            var queryService = (delegate* unmanaged<nint, Guid*, Guid*, nint*, int>)spVt[3];
            if (queryService(serviceProvider, &sid, &iidBrowser, &browser) != 0 || browser == 0)
                return null;

            nint* browserVt = *(nint**)browser;
            var queryActiveView = (delegate* unmanaged<nint, nint*, int>)browserVt[15];
            if (queryActiveView(browser, &view) != 0 || view == 0)
                return null;

            Guid iidFolderView = Ids.IFolderView;
            if (Marshal.QueryInterface(view, in iidFolderView, out folderView) != 0)
                return null;

            Guid iidPersist = Ids.IPersistFolder2;
            nint* fvVt = *(nint**)folderView;
            var getFolder = (delegate* unmanaged<nint, Guid*, nint*, int>)fvVt[5];
            if (getFolder(folderView, &iidPersist, &persist) != 0 || persist == 0)
                return null;

            nint* pfVt = *(nint**)persist;
            var getCurFolder = (delegate* unmanaged<nint, nint*, int>)pfVt[5];
            if (getCurFolder(persist, &pidl) != 0 || pidl == 0)
                return null;

            char* buffer = stackalloc char[1024];
            return Native.SHGetPathFromIDListEx(pidl, buffer, 1024, 0) ? new string(buffer) : null;
        }
        catch (Exception ex)
        {
            Log.Write($"FromSite failed: {ex.Message}");
            return null;
        }
        finally
        {
            if (pidl != 0) Marshal.FreeCoTaskMem(pidl);
            if (persist != 0) Marshal.Release(persist);
            if (folderView != 0) Marshal.Release(folderView);
            if (view != 0) Marshal.Release(view);
            if (browser != 0) Marshal.Release(browser);
            if (serviceProvider != 0) Marshal.Release(serviceProvider);
        }
    }
}

using System.Runtime.InteropServices;

namespace SmartStart;

/// <summary>Zeigt das Explorer-Kontextmenü für eine Datei / einen Ordner.</summary>
internal sealed class ShellContextMenu
{
    private const uint FirstShellId = 1;
    private const uint LastShellId = 0x7FFF;
    private const uint IdShowInExplorer = 0x8001;
    private const uint IdOpenTargetFolder = 0x8002;
    private const uint IdRefresh = 0x8003;

    private IContextMenu2? menu2;
    private IContextMenu3? menu3;

    /// <summary>Gerade angezeigtes Menü (für Nachrichtenweiterleitung im Owner-Fenster).</summary>
    public static ShellContextMenu? Current { get; private set; }

    public enum Result
    {
        None,
        Invoked,
        Rename,
        Refresh,
    }

    /// <summary>Leitet Owner-Draw-/Untermenü-Nachrichten an die Shell weiter.</summary>
    public bool HandleMessage(ref Message m)
    {
        try
        {
            if (menu3 != null && (m.Msg == Native.WM_MENUCHAR || m.Msg == Native.WM_INITMENUPOPUP ||
                                  m.Msg == Native.WM_DRAWITEM || m.Msg == Native.WM_MEASUREITEM))
            {
                if (menu3.HandleMenuMsg2(m.Msg, m.WParam, m.LParam, out IntPtr result) == 0)
                {
                    m.Result = result;
                    return true;
                }
            }
            else if (menu2 != null && (m.Msg == Native.WM_INITMENUPOPUP || m.Msg == Native.WM_DRAWITEM ||
                                       m.Msg == Native.WM_MEASUREITEM))
            {
                if (menu2.HandleMenuMsg(m.Msg, m.WParam, m.LParam) == 0)
                {
                    m.Result = IntPtr.Zero;
                    return true;
                }
            }
        }
        catch
        {
            // ignorieren
        }

        return false;
    }

    public static Result Show(IntPtr owner, string path, string? linkTargetFolder, Point screen)
    {
        var scm = new ShellContextMenu();
        return scm.ShowInternal(owner, path, linkTargetFolder, screen);
    }

    private Result ShowInternal(IntPtr owner, string path, string? linkTargetFolder, Point screen)
    {
        IntPtr pidl = IntPtr.Zero;
        IntPtr pFolder = IntPtr.Zero;
        IntPtr pMenu = IntPtr.Zero;
        IntPtr hMenu = IntPtr.Zero;
        IShellFolder? parent = null;
        IContextMenu? cm = null;
        try
        {
            if (Native.SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _) != 0 || pidl == IntPtr.Zero)
            {
                return Result.None;
            }

            if (Native.SHBindToParent(pidl, Native.IID_IShellFolder, out pFolder, out IntPtr child) != 0)
            {
                return Result.None;
            }

            parent = (IShellFolder)Marshal.GetObjectForIUnknown(pFolder);
            Guid iid = Native.IID_IContextMenu;
            if (parent.GetUIObjectOf(owner, 1, new[] { child }, ref iid, IntPtr.Zero, out pMenu) != 0)
            {
                return Result.None;
            }

            cm = (IContextMenu)Marshal.GetObjectForIUnknown(pMenu);
            menu2 = cm as IContextMenu2;
            menu3 = cm as IContextMenu3;

            hMenu = Native.CreatePopupMenu();
            uint flags = Native.CMF_NORMAL | Native.CMF_CANRENAME;
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                flags |= Native.CMF_EXTENDEDVERBS;
            }

            cm.QueryContextMenu(hMenu, 0, FirstShellId, LastShellId, flags);

            // Eigene Einträge oben
            uint pos = 0;
            Native.InsertMenuW(hMenu, pos++, Native.MF_BYPOSITION | Native.MF_STRING, (UIntPtr)IdShowInExplorer, L.ShowInExplorer);
            if (linkTargetFolder != null)
            {
                Native.InsertMenuW(hMenu, pos++, Native.MF_BYPOSITION | Native.MF_STRING, (UIntPtr)IdOpenTargetFolder, L.OpenTargetFolder);
            }

            Native.InsertMenuW(hMenu, pos++, Native.MF_BYPOSITION | Native.MF_STRING, (UIntPtr)IdRefresh, L.RefreshMenu);
            Native.InsertMenuW(hMenu, pos, Native.MF_BYPOSITION | Native.MF_SEPARATOR, UIntPtr.Zero, null);

            Current = this;
            Native.SetForegroundWindow(owner);
            uint cmd = Native.TrackPopupMenuEx(hMenu, Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON, screen.X, screen.Y, owner, IntPtr.Zero);
            Native.PostMessage(owner, 0, IntPtr.Zero, IntPtr.Zero);
            Current = null;

            switch (cmd)
            {
                case 0:
                    return Result.None;
                case IdShowInExplorer:
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    return Result.Invoked;
                case IdOpenTargetFolder:
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{linkTargetFolder}\"");
                    return Result.Invoked;
                case IdRefresh:
                    return Result.Refresh;
            }

            if (cmd < FirstShellId || cmd > LastShellId)
            {
                return Result.None;
            }

            string verb = GetVerb(cm, cmd - FirstShellId);
            if (verb.Equals("rename", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Rename;
            }

            var info = new Native.CMINVOKECOMMANDINFOEX
            {
                cbSize = Marshal.SizeOf<Native.CMINVOKECOMMANDINFOEX>(),
                fMask = Native.CMIC_MASK_UNICODE | Native.CMIC_MASK_PTINVOKE,
                hwnd = owner,
                lpVerb = (IntPtr)(cmd - FirstShellId),
                lpVerbW = (IntPtr)(cmd - FirstShellId),
                nShow = 1,
                ptInvoke = new Native.POINT(screen.X, screen.Y),
            };
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                info.fMask |= Native.CMIC_MASK_CONTROL_DOWN;
            }

            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                info.fMask |= Native.CMIC_MASK_SHIFT_DOWN;
            }

            cm.InvokeCommand(ref info);
            return Result.Invoked;
        }
        catch
        {
            return Result.None;
        }
        finally
        {
            Current = null;
            menu2 = null;
            menu3 = null;
            if (hMenu != IntPtr.Zero)
            {
                Native.DestroyMenu(hMenu);
            }

            if (cm != null)
            {
                Marshal.ReleaseComObject(cm);
            }

            if (parent != null)
            {
                Marshal.ReleaseComObject(parent);
            }

            if (pMenu != IntPtr.Zero)
            {
                Marshal.Release(pMenu);
            }

            if (pFolder != IntPtr.Zero)
            {
                Marshal.Release(pFolder);
            }

            if (pidl != IntPtr.Zero)
            {
                Native.ILFree(pidl);
            }
        }
    }

    private static string GetVerb(IContextMenu cm, uint offset)
    {
        IntPtr buffer = Marshal.AllocHGlobal(1024);
        try
        {
            Marshal.WriteInt16(buffer, 0);
            if (cm.GetCommandString((UIntPtr)offset, Native.GCS_VERBW, IntPtr.Zero, buffer, 512) == 0)
            {
                return Marshal.PtrToStringUni(buffer) ?? string.Empty;
            }
        }
        catch
        {
            // manche Handler werfen hier
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return string.Empty;
    }
}

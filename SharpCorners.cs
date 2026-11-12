namespace SmartStart;

/// <summary>
/// Schaltet für alle Fenster dieses Prozesses die abgerundeten Windows-11-Ecken ab –
/// auch für Kontextmenüs (Explorer-Menü, Tray-Menü), Dialoge und Meldungen.
/// </summary>
internal static class SharpCorners
{
    private static readonly List<IntPtr> Hooks = new();
    private static Native.WinEventProc? callback;

    public static void Install()
    {
        if (callback != null)
        {
            return;
        }

        callback = OnEvent;
        uint pid = (uint)Environment.ProcessId;
        foreach (var (min, max) in new[]
                 {
                     (Native.EVENT_SYSTEM_MENUPOPUPSTART, Native.EVENT_SYSTEM_MENUPOPUPSTART),
                     (Native.EVENT_OBJECT_CREATE, Native.EVENT_OBJECT_CREATE),
                     (Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW),
                 })
        {
            IntPtr h = Native.SetWinEventHook(min, max, IntPtr.Zero, callback, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (h != IntPtr.Zero)
            {
                Hooks.Add(h);
            }
        }
    }

    public static void Uninstall()
    {
        foreach (IntPtr h in Hooks)
        {
            Native.UnhookWinEvent(h);
        }

        Hooks.Clear();
    }

    private static void OnEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != 0 /* OBJID_WINDOW */)
        {
            return;
        }

        if (Native.GetAncestor(hwnd, 2 /* GA_ROOT */) == hwnd)
        {
            Native.SetSharpCorners(hwnd);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Timer = System.Windows.Forms.Timer;

namespace SmartStart;

/// <summary>Steuert alle Menü-Ebenen: Öffnen, Schließen, Maus, Tastatur, Suche.</summary>
internal sealed class MenuController : IDisposable
{
    private readonly List<MenuForm> levels = new();
    private readonly Dictionary<string, (List<Entry> Entries, FolderStatus Status)> entryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer closeTimer = new() { Interval = 100 };
    private readonly Timer hoverTimer = new();
    private AppSettings settings;
    private Theme theme;
    private MenuForm? main;
    private int focusLevel;
    private bool dragging;
    private int modal;
    private DateTime? leftSince;
    private Action? hoverAction;
    private int hoverActionLevel = -1;
    private float scale = 1f;
    private Point anchor;
    private DateTime openedAt;

    public MenuController(AppSettings settings)
    {
        this.settings = settings;
        theme = settings.IsDark() ? Theme.Dark : Theme.Light;
        closeTimer.Tick += CloseTimer_Tick;
        hoverTimer.Tick += HoverTimer_Tick;
    }

    public event Action? SettingsRequested;

    public event Action? RootFolderMissing;

    public bool Pinned { get; private set; }

    public bool IsOpen => main != null && !main.IsDisposed && main.Visible;

    public IReadOnlyList<MenuForm> OpenForms => levels;

    public void SetPinned(bool pinned)
    {
        Pinned = pinned;
        main?.Invalidate();
    }

    public void ApplySettings(AppSettings newSettings)
    {
        CloseAll();
        settings = newSettings;
        theme = settings.IsDark() ? Theme.Dark : Theme.Light;
        entryCache.Clear();
        IconLoader.Clear();
        main?.Dispose();
        main = null;
    }

    public void Toggle()
    {
        Log.Write($"Toggle (open={IsOpen})");
        if (IsOpen)
        {
            CloseAll();
        }
        else
        {
            Open();
        }
    }

    public void BeginModal() => modal++;

    public void EndModal() => modal = Math.Max(0, modal - 1);

    // ---------------------------------------------------------------- Öffnen / Schließen

    public async void Open()
    {
        if (IsOpen)
        {
            ForceForeground(main!);
            return;
        }

        string root = settings.RootFolder;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            RootFolderMissing?.Invoke();
            return;
        }

        anchor = Cursor.Position;
        scale = DpiScaleAt(anchor) * settings.SizePercent / 100f;

        if (main == null || main.IsDisposed || !string.Equals(main.Folder, root, StringComparison.OrdinalIgnoreCase))
        {
            main?.Dispose();
            main = new MenuForm(this, 0, root, null, -1);
        }

        main.Configure(theme, scale, settings);
        main.ResetState();
        levels.Clear();
        levels.Add(main);
        focusLevel = 0;
        Pinned = false;
        leftSince = null;

        if (entryCache.TryGetValue(root, out var cached))
        {
            main.SetEntries(cached.Entries, cached.Status);
        }
        else
        {
            var res = await Task.Run(() => FolderReader.Read(root, settings));
            entryCache[root] = res;
            main.SetEntries(res.Entries, res.Status);
        }

        main.OpensLeft = settings.Position == MenuPosition.BottomRight;
        main.Relayout(WorkingArea().Height);
        PositionMain();
        openedAt = DateTime.UtcNow;
        main.Show();
        ForceForeground(main);
        Log.Write($"Open: {root} at {main.Bounds}, foreground={(Native.GetForegroundWindow() == main.Handle)}");
        closeTimer.Start();

        _ = FolderIndex.Get(root).EnsureBuilt(settings);
        Reload(main, keepCacheFirst: true);
    }

    public void CloseAll()
    {
        CancelHover();
        CloseFrom(1);
        if (main != null && !main.IsDisposed)
        {
            main.Hide();
            main.ResetState();
        }

        levels.Clear();
        if (main != null)
        {
            levels.Add(main);
        }

        Pinned = false;
        focusLevel = 0;
        closeTimer.Stop();
    }

    private void CloseFrom(int level)
    {
        if (level < 1)
        {
            level = 1;
        }

        for (int i = levels.Count - 1; i >= level; i--)
        {
            var f = levels[i];
            levels.RemoveAt(i);
            f.Hide();
            f.Dispose();
        }

        if (level - 1 < levels.Count)
        {
            var parent = levels[level - 1];
            if (parent.OpenIndex != -1)
            {
                parent.OpenIndex = -1;
                parent.Invalidate();
            }
        }

        if (levels.Count == 0)
        {
            // Menü wurde noch nie geöffnet (z. B. Ordnerwahl beim ersten Start)
            focusLevel = 0;
            return;
        }

        if (focusLevel > levels.Count - 1)
        {
            focusLevel = levels.Count - 1;
            levels[focusLevel].Invalidate();
        }
    }

    private void OpenSubmenu(MenuForm parent, int index, bool focusIt)
    {
        if (parent.IsDisposed || !levels.Contains(parent) || index < 0 || index >= parent.Entries.Count)
        {
            return;
        }

        var e = parent.Entries[index];
        if (!e.IsFolder || e.FolderTarget == null)
        {
            return;
        }

        int childLevel = parent.Level + 1;
        if (parent.OpenIndex == index && levels.Count > childLevel &&
            string.Equals(levels[childLevel].Folder, e.FolderTarget, StringComparison.OrdinalIgnoreCase))
        {
            if (focusIt)
            {
                FocusLevel(childLevel, selectFirst: true);
            }

            return;
        }

        CloseFrom(childLevel);
        parent.OpenIndex = index;
        parent.Invalidate();

        var f = new MenuForm(this, childLevel, e.FolderTarget, parent, index);
        f.Configure(theme, scale, settings);
        levels.Add(f);
        if (entryCache.TryGetValue(e.FolderTarget, out var cached))
        {
            f.SetEntries(cached.Entries, cached.Status);
        }
        else
        {
            f.SetLoading();
        }

        f.SelectFirstWhenLoaded = focusIt;
        if (focusIt)
        {
            FocusLevel(childLevel, selectFirst: true);
        }

        f.Relayout(WorkingArea().Height);
        PositionSub(f);
        f.Show();
        Reload(f, keepCacheFirst: true);
    }

    private async void Reload(MenuForm f, bool keepCacheFirst = false)
    {
        int version = ++f.LoadVersion;
        string folder = f.Folder;
        entryCache.TryGetValue(folder, out var before);
        if (!keepCacheFirst)
        {
            entryCache.Remove(folder);
        }

        var res = await Task.Run(() => FolderReader.Read(folder, settings));
        if (f.IsDisposed || version != f.LoadVersion)
        {
            return;
        }

        entryCache[folder] = res;
        bool changed = before.Entries == null || !SameEntries(before.Entries, res.Entries) || before.Status != res.Status;
        if (changed || !keepCacheFirst)
        {
            // Untermenüs schließen, deren Zeilen sich verschoben haben könnten
            if (levels.Contains(f))
            {
                CloseFrom(f.Level + 1);
            }

            f.SetEntries(res.Entries, res.Status);
            AfterLayoutChange(f);
        }

        if (f.SelectFirstWhenLoaded)
        {
            f.SelectFirstWhenLoaded = false;
            if (f.Selected < 0 && f.Entries.Count > 0 && focusLevel == f.Level)
            {
                f.Selected = 0;
                f.Invalidate();
            }
        }
    }

    private static bool SameEntries(List<Entry> a, List<Entry> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Path, b[i].Path, StringComparison.Ordinal) ||
                a[i].DisplayName != b[i].DisplayName || a[i].IsFolder != b[i].IsFolder)
            {
                return false;
            }
        }

        return true;
    }

    public void RefreshAll()
    {
        entryCache.Clear();
        foreach (var f in levels.ToList())
        {
            foreach (var e in f.Entries)
            {
                IconLoader.Invalidate(e.Path);
            }

            Reload(f);
        }

        if (main != null)
        {
            _ = FolderIndex.Get(main.Folder).EnsureBuilt(settings, forceRefresh: true);
        }
    }

    private void RefreshLater(MenuForm f, string? alsoFolder = null)
    {
        var t = new Timer { Interval = 700 };
        t.Tick += (_, _) =>
        {
            t.Stop();
            t.Dispose();
            if (alsoFolder != null)
            {
                entryCache.Remove(alsoFolder);
            }

            if (!f.IsDisposed && levels.Contains(f))
            {
                Reload(f);
            }

            if (main != null)
            {
                _ = FolderIndex.Get(main.Folder).EnsureBuilt(settings, forceRefresh: true);
            }
        };
        t.Start();
    }

    // ---------------------------------------------------------------- Layout

    private Rectangle WorkingArea()
    {
        return Screen.FromPoint(anchor).WorkingArea;
    }

    private void AfterLayoutChange(MenuForm f)
    {
        if (f.IsDisposed)
        {
            return;
        }

        f.Relayout(WorkingArea().Height);
        if (f.IsMain)
        {
            PositionMain();
        }
        else
        {
            PositionSub(f);
        }

        for (int i = f.Level + 1; i < levels.Count; i++)
        {
            PositionSub(levels[i]);
        }

        f.Invalidate();
    }

    private void PositionMain()
    {
        if (main == null)
        {
            return;
        }

        var wa = WorkingArea();
        int w = main.Width, h = main.Height;
        int x, y;
        switch (settings.Position)
        {
            case MenuPosition.BottomRight:
                x = wa.Right - w;
                y = wa.Bottom - h;
                break;
            case MenuPosition.Mouse:
                // Obere linke Ecke (Suchfeld) am Mauszeiger. Passt es rechts nicht hin,
                // erscheint das Menü links vom Zeiger und Untermenüs öffnen nach links.
                // Passt es unten nicht hin, wird es nach oben geschoben.
                x = anchor.X;
                main.OpensLeft = false;
                if (x + w > wa.Right)
                {
                    x = anchor.X - w + 1;
                    main.OpensLeft = true;
                }

                y = anchor.Y;
                break;
            case MenuPosition.Center:
                x = wa.Left + ((wa.Width - w) / 2);
                y = wa.Top + ((wa.Height - h) / 2);
                break;
            default:
                x = wa.Left;
                y = wa.Bottom - h;
                break;
        }

        x = Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - w));
        y = Math.Clamp(y, wa.Top, Math.Max(wa.Top, wa.Bottom - h));
        main.Location = new Point(x, y);
    }

    private void PositionSub(MenuForm f)
    {
        var p = f.ParentMenu;
        if (p == null || p.IsDisposed)
        {
            return;
        }

        var wa = Screen.FromRectangle(p.Bounds).WorkingArea;
        var row = p.RowScreenRect(f.ParentRow);
        bool left = p.OpensLeft;
        int x = left ? p.Left - f.Width + 1 : p.Right - 1;
        if (!left && x + f.Width > wa.Right)
        {
            left = true;
            x = p.Left - f.Width + 1;
        }
        else if (left && x < wa.Left)
        {
            left = false;
            x = p.Right - 1;
        }

        x = Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - f.Width));
        int y = row.Top - 1;
        if (y + f.Height > wa.Bottom)
        {
            y = wa.Bottom - f.Height;
        }

        if (y < wa.Top)
        {
            y = wa.Top;
        }

        f.OpensLeft = left;
        f.Location = new Point(x, y);
    }

    private static float DpiScaleAt(Point p)
    {
        try
        {
            IntPtr mon = Native.MonitorFromPoint(new Native.POINT(p.X, p.Y), 2 /* MONITOR_DEFAULTTONEAREST */);
            if (Native.GetDpiForMonitor(mon, 0, out uint dpiX, out _) == 0 && dpiX > 0)
            {
                return dpiX / 96f;
            }
        }
        catch
        {
            // Fallback
        }

        return 1f;
    }

    // ---------------------------------------------------------------- Fokus / Aktivierung

    private static void ForceForeground(Form f)
    {
        IntPtr h = f.Handle;
        f.Activate();
        if (Native.GetForegroundWindow() == h)
        {
            return;
        }

        IntPtr fg = Native.GetForegroundWindow();
        uint fgThread = Native.GetWindowThreadProcessId(fg, out _);
        uint me = Native.GetCurrentThreadId();
        if (fgThread != 0 && fgThread != me)
        {
            Native.AttachThreadInput(me, fgThread, true);
            Native.BringWindowToTop(h);
            Native.SetForegroundWindow(h);
            Native.AttachThreadInput(me, fgThread, false);
        }
        else
        {
            Native.SetForegroundWindow(h);
        }

        if (Native.GetForegroundWindow() != h)
        {
            // Foreground-Lock umgehen: eine unbelegte Taste (vkE8) simulieren,
            // damit dieser Prozess die letzte Eingabe erhalten hat.
            Native.keybd_event(0xE8, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0xE8, 0, 2 /* KEYEVENTF_KEYUP */, UIntPtr.Zero);
            Native.SetForegroundWindow(h);
        }

        f.Activate();
    }

    public bool IsKeyboardFocus(MenuForm f) => f.Level == focusLevel;

    private void FocusLevel(int level, bool selectFirst)
    {
        if (level < 0 || level >= levels.Count)
        {
            return;
        }

        int old = focusLevel;
        focusLevel = level;
        var f = levels[level];
        if (selectFirst && f.Selected < 0 && f.Entries.Count > 0)
        {
            f.Selected = 0;
        }

        if (old < levels.Count)
        {
            levels[old].Invalidate();
        }

        f.Invalidate();
    }

    private void CloseTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsOpen)
        {
            closeTimer.Stop();
            return;
        }

        if (Pinned || dragging || modal > 0 || DateTime.UtcNow - openedAt < TimeSpan.FromMilliseconds(300))
        {
            leftSince = null;
            return;
        }

        bool active = IsOwnWindow(Native.GetForegroundWindow());
        var pos = Cursor.Position;
        bool over = levels.Any(l => !l.IsDisposed && l.Visible && l.Bounds.Contains(pos));
        if (active || over)
        {
            leftSince = null;
            return;
        }

        leftSince ??= DateTime.UtcNow;
        if (DateTime.UtcNow - leftSince.Value >= TimeSpan.FromMilliseconds(settings.CloseDelayMs))
        {
            Log.Write($"Close: Fokus verloren und Maus außerhalb (fg={Native.GetForegroundWindow():X})");
            CloseAll();
        }
    }

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    // ---------------------------------------------------------------- Hover-Verzögerung

    private void ScheduleHover(int level, Action action)
    {
        hoverAction = action;
        hoverActionLevel = level;
        hoverTimer.Stop();
        hoverTimer.Interval = Math.Max(1, settings.HoverOpenDelayMs);
        hoverTimer.Start();
    }

    private void CancelHover()
    {
        hoverTimer.Stop();
        hoverAction = null;
        hoverActionLevel = -1;
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        hoverTimer.Stop();
        var a = hoverAction;
        hoverAction = null;
        hoverActionLevel = -1;
        a?.Invoke();
    }

    // ---------------------------------------------------------------- Maus

    public void OnMenuMouseEnter(MenuForm f)
    {
        if (hoverAction != null && hoverActionLevel < f.Level)
        {
            CancelHover();
        }
    }

    public void OnMenuMouseLeave(MenuForm f)
    {
        if (hoverAction != null && hoverActionLevel == f.Level)
        {
            CancelHover();
        }

        if (f.Selected != -1 && !f.IsDisposed)
        {
            f.Selected = -1;
            f.Invalidate();
        }
    }

    public void OnRowHover(MenuForm f, int index)
    {
        if (f.Selected == index)
        {
            return;
        }

        f.Selected = index;
        if (focusLevel != f.Level)
        {
            int old = focusLevel;
            focusLevel = f.Level;
            if (old < levels.Count)
            {
                levels[old].Invalidate();
            }
        }

        f.Invalidate();
        if (index < 0)
        {
            return;
        }

        ScheduleForSelection(f, index);
    }

    private void ScheduleForSelection(MenuForm f, int index)
    {
        var e = f.Entries[index];
        if (e.IsFolder)
        {
            if (f.OpenIndex == index)
            {
                CancelHover();
                return;
            }

            ScheduleHover(f.Level, () => OpenSubmenu(f, index, false));
        }
        else if (levels.Count > f.Level + 1)
        {
            ScheduleHover(f.Level, () =>
            {
                if (levels.Contains(f))
                {
                    CloseFrom(f.Level + 1);
                }
            });
        }
        else
        {
            CancelHover();
        }
    }

    public void OnRowClick(MenuForm f, int index, Keys mods)
    {
        CancelHover();
        var e = f.Entries[index];
        if (e.IsFolder)
        {
            if (settings.OpenFolderWithSingleClick)
            {
                OpenInExplorer(e);
            }
            else
            {
                OpenSubmenu(f, index, false);
            }
        }
        else
        {
            Launch(e, mods);
        }
    }

    public void OnRowDoubleClick(MenuForm f, int index)
    {
        var e = f.Entries[index];
        if (e.IsFolder && !settings.OpenFolderWithSingleClick)
        {
            OpenInExplorer(e);
        }
    }

    public void OnRowMiddleClick(MenuForm f, int index)
    {
        var e = f.Entries[index];
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{e.Path}\"");
    }

    public void OnButton(int button)
    {
        switch (button)
        {
            case 0:
                Pinned = !Pinned;
                main?.Invalidate();
                break;
            case 1:
                if (main != null)
                {
                    StartProcess("explorer.exe", $"\"{main.Folder}\"");
                }

                break;
            case 2:
                CloseAll();
                SettingsRequested?.Invoke();
                break;
        }
    }

    public void OnScrolled(MenuForm f)
    {
        CancelHover();
        CloseFrom(f.Level + 1);
    }

    public void OnRowContext(MenuForm f, int index, Point screen)
    {
        CancelHover();
        var e = f.Entries[index];
        f.Selected = index;
        f.Invalidate();
        ShowShellMenu(f, e.Path, e.IsFolder && !string.Equals(e.FolderTarget, e.Path, StringComparison.OrdinalIgnoreCase) ? e.FolderTarget : null, screen, e);
    }

    public void OnBackgroundContext(MenuForm f, Point screen)
    {
        CancelHover();
        ShowShellMenu(f, f.Folder, null, screen, null);
    }

    private void ShowShellMenu(MenuForm f, string path, string? linkTarget, Point screen, Entry? entry)
    {
        if (main == null)
        {
            return;
        }

        BeginModal();
        ShellContextMenu.Result result;
        try
        {
            result = ShellContextMenu.Show(main.Handle, path, linkTarget, screen);
        }
        finally
        {
            EndModal();
        }

        switch (result)
        {
            case ShellContextMenu.Result.Rename when entry != null:
                Rename(f, entry);
                break;
            case ShellContextMenu.Result.Refresh:
                RefreshAll();
                break;
            case ShellContextMenu.Result.Invoked:
                IconLoader.Invalidate(path);
                if (!f.IsDisposed)
                {
                    RefreshLater(f, entry?.IsFolder == true ? entry.FolderTarget : null);
                }

                break;
        }
    }

    private void Rename(MenuForm f, Entry e)
    {
        string oldName = Path.GetFileName(e.Path);
        BeginModal();
        string? newName;
        try
        {
            newName = InputDialog.Show(L.Rename, L.NewName, oldName);
        }
        finally
        {
            EndModal();
        }

        if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
        {
            if (main != null && IsOpen)
            {
                ForceForeground(main);
            }

            return;
        }

        try
        {
            string dir = Path.GetDirectoryName(e.Path)!;
            string target = Path.Combine(dir, newName.Trim());
            if (Directory.Exists(e.Path) && !File.Exists(e.Path))
            {
                Directory.Move(e.Path, target);
            }
            else
            {
                File.Move(e.Path, target);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SmartStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        if (main != null && IsOpen)
        {
            ForceForeground(main);
        }

        Reload(f);
        if (main != null)
        {
            _ = FolderIndex.Get(main.Folder).EnsureBuilt(settings, forceRefresh: true);
        }
    }

    // ---------------------------------------------------------------- Starten

    private void Launch(Entry e, Keys mods)
    {
        bool admin = (mods & (Keys.Control | Keys.Shift)) == (Keys.Control | Keys.Shift);
        var psi = new ProcessStartInfo(e.Path)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(e.Path) ?? string.Empty,
        };
        if (admin)
        {
            psi.Verb = "runas";
        }

        try
        {
            Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // vom Benutzer abgebrochen (UAC)
            return;
        }
        catch (Exception ex)
        {
            BeginModal();
            try
            {
                MessageBox.Show(L.CouldNotStart(e.Path, ex.Message), "SmartStart",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                EndModal();
            }

            return;
        }

        if (settings.CloseAfterLaunch && !Pinned)
        {
            CloseAll();
        }
    }

    private void OpenInExplorer(Entry e)
    {
        StartProcess("explorer.exe", $"\"{e.FolderTarget ?? e.Path}\"");
        if (settings.CloseAfterLaunch && !Pinned)
        {
            CloseAll();
        }
    }

    private static void StartProcess(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SmartStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---------------------------------------------------------------- Tastatur

    public bool HandleKey(Keys keyData)
    {
        if (levels.Count == 0)
        {
            return false;
        }

        focusLevel = Math.Clamp(focusLevel, 0, levels.Count - 1);
        var f = levels[focusLevel];
        Keys key = keyData & Keys.KeyCode;
        Keys mods = keyData & Keys.Modifiers;

        switch (key)
        {
            case Keys.Down:
                Move(f, 1, wrap: true);
                return true;
            case Keys.Up:
                Move(f, -1, wrap: true);
                return true;
            case Keys.PageDown:
                Move(f, f.VisibleRows, wrap: false);
                return true;
            case Keys.PageUp:
                Move(f, -f.VisibleRows, wrap: false);
                return true;
            case Keys.Home:
                if (f.Entries.Count > 0)
                {
                    Move(f, -f.Selected - 1, wrap: false);
                }

                return true;
            case Keys.End:
                Move(f, f.Entries.Count, wrap: false);
                return true;
            case Keys.Right:
                if (f.Selected >= 0 && f.Entries[f.Selected].IsFolder)
                {
                    CancelHover();
                    OpenSubmenu(f, f.Selected, true);
                }

                return true;
            case Keys.Left:
                if (focusLevel > 0)
                {
                    CancelHover();
                    int lvl = focusLevel;
                    CloseFrom(lvl);
                    FocusLevel(lvl - 1, false);
                }

                return true;
            case Keys.Enter:
                if (f.Selected < 0 && f.Entries.Count > 0)
                {
                    f.Selected = 0;
                }

                if (f.Selected >= 0)
                {
                    CancelHover();
                    var e = f.Entries[f.Selected];
                    if (e.IsFolder)
                    {
                        if ((mods & Keys.Shift) != 0 || settings.OpenFolderWithSingleClick)
                        {
                            OpenInExplorer(e);
                        }
                        else
                        {
                            OpenSubmenu(f, f.Selected, true);
                        }
                    }
                    else
                    {
                        Launch(e, mods);
                    }
                }

                return true;
            case Keys.Escape:
                if (levels[0].Filter.Length > 0)
                {
                    SetFilter(levels[0], string.Empty);
                }
                else
                {
                    CloseAll();
                }

                return true;
            case Keys.Tab:
                if (levels.Count > 1)
                {
                    int next = (focusLevel + ((mods & Keys.Shift) != 0 ? -1 : 1) + levels.Count) % levels.Count;
                    FocusLevel(next, selectFirst: true);
                }

                return true;
            case Keys.Apps:
            case Keys.F10 when (mods & Keys.Shift) != 0:
                if (f.Selected >= 0)
                {
                    var r = f.RowScreenRect(f.Selected);
                    OnRowContext(f, f.Selected, new Point(r.Left + (r.Width / 3), r.Bottom));
                }

                return true;
            case Keys.F2:
                if (f.Selected >= 0)
                {
                    Rename(f, f.Entries[f.Selected]);
                }

                return true;
            case Keys.F5:
                RefreshAll();
                return true;
            case Keys.Back:
                if (levels[0].Filter.Length > 0)
                {
                    var m = levels[0];
                    SetFilter(m, (mods & Keys.Control) != 0 ? string.Empty : m.Filter[..^1]);
                }

                return true;
            case Keys.V when (mods & Keys.Control) != 0:
                try
                {
                    string text = Clipboard.GetText().Replace("\r", " ").Replace("\n", " ").Trim();
                    if (text.Length > 0)
                    {
                        SetFilter(levels[0], levels[0].Filter + text);
                    }
                }
                catch
                {
                    // Zwischenablage belegt
                }

                return true;
        }

        return false;
    }

    public void HandleChar(char c)
    {
        if (levels.Count == 0)
        {
            return;
        }

        // Globale Suche: Tippen geht immer in die Suche des Hauptmenüs (ganzer Stammordner)
        var f = levels[0];
        if (c == ' ' && f.Filter.Length == 0)
        {
            return;
        }

        SetFilter(f, f.Filter + c);
    }

    private void Move(MenuForm f, int delta, bool wrap)
    {
        int n = f.Entries.Count;
        if (n == 0)
        {
            return;
        }

        int sel;
        if (f.Selected < 0)
        {
            sel = delta > 0 ? 0 : n - 1;
        }
        else
        {
            sel = f.Selected + delta;
            sel = wrap ? ((sel % n) + n) % n : Math.Clamp(sel, 0, n - 1);
        }

        f.Selected = sel;
        f.EnsureVisible(sel);
        f.Invalidate();
        ScheduleForSelection(f, sel);
    }

    // ---------------------------------------------------------------- Suche

    private async void SetFilter(MenuForm f, string text)
    {
        CancelHover();
        CloseFrom(f.Level + 1);
        f.Filter = text;
        if (focusLevel != f.Level)
        {
            FocusLevel(f.Level, selectFirst: false);
        }

        if (text.Length == 0)
        {
            f.ShowBase();
            f.Selected = -1;
            AfterLayoutChange(f);
            return;
        }

        var index = FolderIndex.Get(f.Folder);
        var build = index.EnsureBuilt(settings);
        f.SetSearchResults(index.IsReady ? index.Search(text) : new List<Entry>(), !build.IsCompleted);
        f.Selected = f.Entries.Count > 0 ? 0 : -1;
        AfterLayoutChange(f);

        if (!build.IsCompleted)
        {
            try
            {
                await build;
            }
            catch
            {
                // ignorieren
            }

            if (!f.IsDisposed && f.Filter == text)
            {
                f.SetSearchResults(index.Search(text), false);
                f.Selected = f.Entries.Count > 0 ? 0 : -1;
                AfterLayoutChange(f);
            }
        }
    }

    // ---------------------------------------------------------------- Drag & Drop

    public void StartDrag(MenuForm f, int index)
    {
        if (index < 0 || index >= f.Entries.Count)
        {
            return;
        }

        CancelHover();
        var e = f.Entries[index];
        var data = new DataObject(DataFormats.FileDrop, new[] { e.Path });
        dragging = true;
        DragDropEffects result;
        try
        {
            result = f.DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }
        finally
        {
            dragging = false;
        }

        if (result == DragDropEffects.Move && !f.IsDisposed)
        {
            RefreshLater(f);
        }
    }

    public bool CanAcceptDrop(IDataObject? data)
    {
        if (dragging || data == null)
        {
            return false;
        }

        return data.GetDataPresent(DataFormats.FileDrop) || GetUrl(data) != null;
    }

    public void OnDragHover(MenuForm f)
    {
        leftSince = null;
    }

    public void OnDrop(MenuForm f, int row, IDataObject data)
    {
        string folder = row >= 0 && row < f.Entries.Count && f.Entries[row].IsFolder
            ? f.Entries[row].FolderTarget ?? f.Folder
            : f.Folder;
        try
        {
            if (data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                foreach (string src in files)
                {
                    string ext = Path.GetExtension(src);
                    bool isShortcut = ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                      ext.Equals(".url", StringComparison.OrdinalIgnoreCase);
                    if (isShortcut)
                    {
                        File.Copy(src, UniquePath(folder, Path.GetFileNameWithoutExtension(src), ext));
                    }
                    else
                    {
                        string name = Path.GetFileNameWithoutExtension(src.TrimEnd('\\'));
                        if (string.IsNullOrEmpty(name))
                        {
                            name = src.TrimEnd('\\').Replace(":", string.Empty);
                        }

                        ShellLink.Create(UniquePath(folder, name, ".lnk"), src);
                    }
                }
            }
            else
            {
                var url = GetUrl(data);
                if (url != null)
                {
                    string title = GetUrlTitle(data) ?? new Uri(url).Host;
                    string path = UniquePath(folder, SafeName(title), ".url");
                    File.WriteAllText(path, $"[InternetShortcut]\r\nURL={url}\r\n", Encoding.UTF8);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SmartStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        entryCache.Remove(folder);
        Reload(f);
        if (main != null)
        {
            _ = FolderIndex.Get(main.Folder).EnsureBuilt(settings, forceRefresh: true);
        }
    }

    private static string? GetUrl(IDataObject data)
    {
        try
        {
            if (data.GetDataPresent("UniformResourceLocatorW") && data.GetData("UniformResourceLocatorW") is MemoryStream ms)
            {
                string s = Encoding.Unicode.GetString(ms.ToArray()).TrimEnd('\0').Trim();
                if (IsWebUrl(s))
                {
                    return s;
                }
            }

            if (data.GetDataPresent(DataFormats.UnicodeText) && data.GetData(DataFormats.UnicodeText) is string t)
            {
                t = t.Trim();
                if (IsWebUrl(t))
                {
                    return t;
                }
            }
        }
        catch
        {
            // ignorieren
        }

        return null;
    }

    private static bool IsWebUrl(string s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    private static string? GetUrlTitle(IDataObject data)
    {
        try
        {
            if (data.GetDataPresent("FileGroupDescriptorW") && data.GetData("FileGroupDescriptorW") is MemoryStream ms)
            {
                byte[] b = ms.ToArray();
                const int nameOffset = 4 + 72;
                if (b.Length >= nameOffset + 2)
                {
                    string name = Encoding.Unicode.GetString(b, nameOffset, Math.Min(520, b.Length - nameOffset));
                    int z = name.IndexOf('\0');
                    if (z >= 0)
                    {
                        name = name[..z];
                    }

                    name = Path.GetFileNameWithoutExtension(name);
                    return string.IsNullOrWhiteSpace(name) ? null : name;
                }
            }
        }
        catch
        {
            // ignorieren
        }

        return null;
    }

    private static string SafeName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            s = s.Replace(c, '_');
        }

        s = s.Trim();
        return s.Length > 80 ? s[..80] : (s.Length == 0 ? "Link" : s);
    }

    private static string UniquePath(string folder, string name, string ext)
    {
        string path = Path.Combine(folder, name + ext);
        int i = 2;
        while (File.Exists(path) || Directory.Exists(path))
        {
            path = Path.Combine(folder, $"{name} ({i++}){ext}");
        }

        return path;
    }

    public void Dispose()
    {
        closeTimer.Dispose();
        hoverTimer.Dispose();
        foreach (var f in levels.ToList())
        {
            f.Dispose();
        }

        main?.Dispose();
    }
}

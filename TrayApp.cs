namespace SmartStart;

/// <summary>Tray-Symbol, Hotkey und Lebenszyklus der Anwendung.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly HotkeyWindow hotkey;
    private readonly MenuController controller;
    private readonly Control invoker;
    private readonly EventWaitHandle showEvent;
    private readonly EventWaitHandle quitEvent;
    private readonly ToolStripMenuItem autostartItem;
    private readonly ContextMenuStrip trayMenu;
    private AppSettings settings;
    private bool settingsOpen;

    /// <summary>Dark/Light für Explorer-Kontextmenü und Tray-Menü.</summary>
    private void ApplyLook()
    {
        bool dark = settings.IsDark();
        Theme.ApplyNativeMenuTheme(settings.Theme);
        try
        {
            // Dialoge (Einstellungen, Umbenennen, Ordnerauswahl-Rahmen) im passenden Design
#pragma warning disable WFO5001
            Application.SetColorMode(dark ? SystemColorMode.Dark : SystemColorMode.Classic);
#pragma warning restore WFO5001
        }
        catch (Exception ex)
        {
            Log.Write("SetColorMode: " + ex.Message);
        }

        trayMenu.Renderer = Theme.CreateMenuRenderer(dark);
        var fore = dark ? Color.FromArgb(242, 242, 242) : SystemColors.ControlText;
        foreach (ToolStripItem item in trayMenu.Items)
        {
            item.ForeColor = fore;
        }
    }

    public TrayApp(string configPath, bool autostart)
    {
        settings = AppSettings.Load(configPath);
        Autostart.RepairApproval();
        SharpCorners.Install();
        invoker = new Control();
        invoker.CreateControl();
        _ = invoker.Handle;

        controller = new MenuController(settings);
        controller.SettingsRequested += () => invoker.BeginInvoke(ShowSettings);
        controller.RootFolderMissing += () => invoker.BeginInvoke(() => ChooseRootFolder(openAfterwards: true));

        var menu = new ContextMenuStrip();
        menu.Items.Add(L.TrayOpenMenu, null, (_, _) => invoker.BeginInvoke(controller.Open)).Font =
            new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(L.TrayOpenRoot, null, (_, _) => OpenRootInExplorer());
        menu.Items.Add(L.TrayChooseRoot, null, (_, _) => ChooseRootFolder(openAfterwards: false));
        menu.Items.Add(L.TraySettings, null, (_, _) => ShowSettings());
        autostartItem = new ToolStripMenuItem(L.StartWithWindows) { CheckOnClick = true };
        autostartItem.Click += (_, _) => SetAutostart(autostartItem.Checked);
        menu.Items.Add(autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.TrayExit, null, (_, _) => ExitThread());
        menu.Opening += (_, _) => autostartItem.Checked = Autostart.IsEnabled;
        trayMenu = menu;
        ApplyLook();

        tray = new NotifyIcon
        {
            Icon = AppIcon.Get(SystemInformation.SmallIconSize.Width),
            Text = "SmartStart",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                controller.Toggle();
            }
        };

        hotkey = new HotkeyWindow();
        hotkey.Pressed += controller.Toggle;
        RegisterHotkey(showError: true);

        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.QuitEventName);
        var listener = new Thread(ListenForOtherInstances) { IsBackground = true, Name = "InstanceListener" };
        listener.Start();

        if (Program.SelfTestDir != null)
        {
            string dir = Program.SelfTestDir;
            invoker.BeginInvoke(() => _ = SelfTest.Run(controller, settings, dir, ExitThread));
        }
        else if (string.IsNullOrWhiteSpace(settings.RootFolder))
        {
            invoker.BeginInvoke(() => ChooseRootFolder(openAfterwards: true));
        }
        else if (!autostart)
        {
            invoker.BeginInvoke(controller.Open);
        }
    }

    private void ListenForOtherInstances()
    {
        var handles = new WaitHandle[] { showEvent, quitEvent };
        while (true)
        {
            int i = WaitHandle.WaitAny(handles);
            try
            {
                if (i == 0)
                {
                    invoker.BeginInvoke(controller.Open);
                }
                else
                {
                    invoker.BeginInvoke(ExitThread);
                    return;
                }
            }
            catch
            {
                return;
            }
        }
    }

    private void RegisterHotkey(bool showError)
    {
        bool ok = hotkey.Register(settings.Hotkey, out string error);
        Log.Write($"Hotkey '{settings.Hotkey}' registriert={ok} {error}");
        tray.Text = ok ? $"SmartStart ({HotkeyDisplay(settings.Hotkey)})" : L.TrayNoHotkey;
        if (!ok && showError)
        {
            tray.ShowBalloonTip(5000, "SmartStart", error, ToolTipIcon.Warning);
        }
    }

    public static string HotkeyDisplay(string spec) => L.HotkeyDisplay(spec);

    private void OpenRootInExplorer()
    {
        if (Directory.Exists(settings.RootFolder))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"\"{settings.RootFolder}\"");
        }
    }

    private void ChooseRootFolder(bool openAfterwards)
    {
        if (settingsOpen)
        {
            return;
        }

        settingsOpen = true;
        try
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = L.ChooseRootDescription,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
            };
            if (Directory.Exists(settings.RootFolder))
            {
                dlg.InitialDirectory = settings.RootFolder;
            }

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var updated = settings.Clone();
            updated.RootFolder = dlg.SelectedPath;
            Apply(updated);
        }
        finally
        {
            settingsOpen = false;
        }

        if (openAfterwards)
        {
            controller.Open();
        }
    }

    private void ShowSettings()
    {
        if (settingsOpen)
        {
            return;
        }

        settingsOpen = true;
        controller.CloseAll();
        hotkey.UnregisterAll();
        try
        {
            using var form = new SettingsForm(settings.Clone());
            if (form.ShowDialog() == DialogResult.OK)
            {
                Apply(form.Result);
                if (form.AutostartChanged)
                {
                    SetAutostart(form.AutostartEnabled);
                }
            }
        }
        finally
        {
            settingsOpen = false;
            RegisterHotkey(showError: true);
        }
    }

    private void Apply(AppSettings updated)
    {
        try
        {
            updated.Save();
        }
        catch (Exception ex)
        {
            Log.Error($"Einstellungen nicht gespeichert ({updated.FilePath}): {ex}");
            MessageBox.Show(L.CouldNotSaveSettings(ex.Message), "SmartStart",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        settings = updated;
        ApplyLook();
        controller.ApplySettings(settings);
        RegisterHotkey(showError: true);
    }

    private static void SetAutostart(bool enabled)
    {
        try
        {
            Autostart.Set(enabled);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SmartStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void ExitThreadCore()
    {
        SharpCorners.Uninstall();
        tray.Visible = false;
        tray.Dispose();
        hotkey.Dispose();
        controller.Dispose();
        base.ExitThreadCore();
    }
}

internal static class AppIcon
{
    public static Icon Get(int size)
    {
        using var s = typeof(AppIcon).Assembly.GetManifestResourceStream("SmartStart.ico");
        return s != null ? new Icon(s, size, size) : SystemIcons.Application;
    }
}

namespace SmartStart;

/// <summary>Einstellungsdialog (im Code aufgebaut, kein Designer).</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings s;
    private readonly TextBox rootBox = new() { Width = 360 };
    private readonly TextBox hotkeyBox = new() { Width = 200, ReadOnly = true };
    private readonly ComboBox themeBox = Combo(L.DesignOptions);
    private readonly ComboBox positionBox = Combo(L.PositionOptions);
    private readonly ComboBox hiddenBox = Combo(L.HiddenOptions);
    private readonly NumericUpDown sizeBox = Num(70, 250, 5);
    private readonly NumericUpDown widthBox = Num(200, 1200, 20);
    private readonly NumericUpDown openDelayBox = Num(0, 2000, 50);
    private readonly NumericUpDown closeDelayBox = Num(100, 10000, 100);
    private readonly CheckBox closeAfterLaunch = new() { Text = L.CloseAfterLaunch, AutoSize = true };
    private readonly CheckBox folderSingleClick = new() { Text = L.FolderSingleClick, AutoSize = true };
    private readonly CheckBox resolveLinks = new() { Text = L.ResolveLinks, AutoSize = true };
    private readonly CheckBox autostart = new() { Text = L.StartWithWindows, AutoSize = true };
    private readonly bool autostartInitial;
    private string hotkey;

    public SettingsForm(AppSettings settings)
    {
        s = settings;
        hotkey = settings.Hotkey;
        Text = L.SettingsTitle;
        Icon = AppIcon.Get(32);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        TopMost = true;

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var browse = new Button { Text = "…", Width = 32 };
        browse.Click += (_, _) => Browse();
        AddRow(grid, L.RootFolder, Flow(rootBox, browse));

        var resetHotkey = new Button { Text = L.HotkeyDefaultButton, AutoSize = true };
        resetHotkey.Click += (_, _) => SetHotkey("Ctrl+LWin");
        hotkeyBox.KeyDown += HotkeyBox_KeyDown;
        hotkeyBox.KeyUp += (_, e) => e.Handled = true;
        hotkeyBox.PreviewKeyDown += (_, e) => e.IsInputKey = true;
        AddRow(grid, L.Hotkey, Flow(hotkeyBox, resetHotkey));
        AddRow(grid, string.Empty, new Label { Text = L.HotkeyHint, AutoSize = true, ForeColor = SystemColors.GrayText });

        AddRow(grid, L.Design, themeBox);
        AddRow(grid, L.Position, positionBox);
        AddRow(grid, L.Size, sizeBox);
        AddRow(grid, L.MaxWidth, widthBox);
        AddRow(grid, L.OpenDelay, openDelayBox);
        AddRow(grid, L.CloseDelay, closeDelayBox);
        AddRow(grid, L.HiddenFiles, hiddenBox);

        var checks = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        checks.Controls.AddRange(new Control[] { closeAfterLaunch, folderSingleClick, resolveLinks, autostart });
        grid.Controls.Add(checks);
        grid.SetColumnSpan(checks, 2);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0) };
        var cancel = new Button { Text = L.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 0) };
        ok.Click += (_, _) => Collect();
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        grid.Controls.Add(buttons);
        grid.SetColumnSpan(buttons, 2);

        Controls.Add(grid);
        AcceptButton = ok;
        CancelButton = cancel;

        rootBox.Text = s.RootFolder;
        hotkeyBox.Text = TrayApp.HotkeyDisplay(hotkey);
        themeBox.SelectedIndex = (int)s.Theme;
        positionBox.SelectedIndex = (int)s.Position;
        hiddenBox.SelectedIndex = (int)s.HiddenFiles;
        sizeBox.Value = s.SizePercent;
        widthBox.Value = s.MaxWidth;
        openDelayBox.Value = s.HoverOpenDelayMs;
        closeDelayBox.Value = s.CloseDelayMs;
        closeAfterLaunch.Checked = s.CloseAfterLaunch;
        folderSingleClick.Checked = s.OpenFolderWithSingleClick;
        resolveLinks.Checked = s.ResolveLinksToFolders;
        autostartInitial = Autostart.IsEnabled;
        autostart.Checked = autostartInitial;
    }

    public AppSettings Result => s;

    public bool AutostartChanged => autostart.Checked != autostartInitial;

    public bool AutostartEnabled => autostart.Checked;

    private static ComboBox Combo(params string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        c.Items.AddRange(items);
        return c;
    }

    private static NumericUpDown Num(int min, int max, int step) =>
        new() { Minimum = min, Maximum = max, Increment = step, Width = 90 };

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        p.Controls.AddRange(controls);
        return p;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 0) });
        grid.Controls.Add(control);
    }

    private void Browse()
    {
        using var dlg = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = L.ChooseRootShort };
        if (Directory.Exists(rootBox.Text))
        {
            dlg.InitialDirectory = rootBox.Text;
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            rootBox.Text = dlg.SelectedPath;
        }
    }

    private void SetHotkey(string spec)
    {
        hotkey = spec;
        hotkeyBox.Text = TrayApp.HotkeyDisplay(spec);
    }

    private void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        var parts = new List<string>();
        bool win = (GetKeyState(Keys.LWin) & 0x8000) != 0 || (GetKeyState(Keys.RWin) & 0x8000) != 0;
        if (e.Control)
        {
            parts.Add("Ctrl");
        }

        if (e.Alt)
        {
            parts.Add("Alt");
        }

        if (e.Shift)
        {
            parts.Add("Shift");
        }

        Keys k = e.KeyCode;
        if (k is Keys.LWin or Keys.RWin)
        {
            if (parts.Count == 0)
            {
                hotkeyBox.Text = "Win + …";
                return;
            }

            parts.Add(k == Keys.RWin ? "RWin" : "LWin");
            SetHotkey(string.Join("+", parts));
            return;
        }

        if (k is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LControlKey or Keys.RControlKey)
        {
            if (win && k is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey && parts.Count == 1)
            {
                SetHotkey("Ctrl+LWin");
                return;
            }

            hotkeyBox.Text = string.Join(" + ", parts) + " + …";
            return;
        }

        if (win)
        {
            parts.Insert(0, "Win");
        }

        if (parts.Count == 0 && !(k >= Keys.F1 && k <= Keys.F24))
        {
            hotkeyBox.Text = L.HotkeyCombine;
            return;
        }

        parts.Add(k.ToString());
        SetHotkey(string.Join("+", parts));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(Keys key);

    private void Collect()
    {
        s.RootFolder = rootBox.Text.Trim();
        s.Hotkey = hotkey;
        s.Theme = (ThemeMode)Math.Max(0, themeBox.SelectedIndex);
        s.Position = (MenuPosition)Math.Max(0, positionBox.SelectedIndex);
        s.HiddenFiles = (HiddenFilesMode)Math.Max(0, hiddenBox.SelectedIndex);
        s.SizePercent = (int)sizeBox.Value;
        s.MaxWidth = (int)widthBox.Value;
        s.HoverOpenDelayMs = (int)openDelayBox.Value;
        s.CloseDelayMs = (int)closeDelayBox.Value;
        s.CloseAfterLaunch = closeAfterLaunch.Checked;
        s.OpenFolderWithSingleClick = folderSingleClick.Checked;
        s.ResolveLinksToFolders = resolveLinks.Checked;
    }
}

/// <summary>Einfacher Eingabedialog (z. B. zum Umbenennen).</summary>
internal static class InputDialog
{
    public static string? Show(string title, string prompt, string value)
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            TopMost = true,
            ShowInTaskbar = false,
        };
        var box = new TextBox { Text = value, Width = 380 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = L.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        panel.Controls.Add(new Label { Text = prompt, AutoSize = true });
        panel.Controls.Add(box);
        panel.Controls.Add(buttons);
        form.Controls.Add(panel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) =>
        {
            form.Activate();
            box.Focus();
            int dot = value.LastIndexOf('.');
            box.Select(0, dot > 0 ? dot : value.Length);
        };
        return form.ShowDialog() == DialogResult.OK ? box.Text : null;
    }
}

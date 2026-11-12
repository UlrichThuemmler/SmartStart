using System.Drawing.Drawing2D;

namespace SmartStart;

/// <summary>Ein Menü-Fenster (eine Ebene). Komplett selbst gezeichnet.</summary>
internal sealed class MenuForm : Form
{
    private const int BorderW = 1;
    private readonly MenuController controller;
    private readonly Dictionary<Entry, int> textWidths = new();
    private List<Entry> baseEntries = new();
    private FolderStatus status = FolderStatus.Ok;
    private bool loading;
    private bool searching;
    private int scroll;
    private int hoverButton = -1;
    private bool thumbDragging;
    private int thumbDragStartY;
    private int thumbDragStartScroll;
    private int mouseDownRow = -1;
    private Point mouseDownPoint;
    private int dropRow = -1;

    private Theme theme = Theme.Dark;
    private float scale = 1f;
    private int rowH = 24;
    private int iconSize = 16;
    private int padX = 6;
    private int gapX = 8;
    private int arrowW = 16;
    private int searchH = 30;
    private int scrollW = 6;
    private int maxWidth = 420;
    private int minWidth = 180;
    private Font? font;
    private Font? smallFont;
    private Font? glyphFont;
    private Font? glyphFontSmall;

    public MenuForm(MenuController controller, int level, string folder, MenuForm? parentMenu, int parentRow)
    {
        this.controller = controller;
        Level = level;
        Folder = folder;
        ParentMenu = parentMenu;
        ParentRow = parentRow;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        AllowDrop = true;
        Text = "SmartStart";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public int Level { get; }

    public string Folder { get; }

    public MenuForm? ParentMenu { get; }

    public int ParentRow { get; }

    public bool OpensLeft { get; set; }

    public List<Entry> Entries { get; private set; } = new();

    public string Filter { get; set; } = string.Empty;

    public int Selected { get; set; } = -1;

    public int OpenIndex { get; set; } = -1;

    public int LoadVersion { get; set; }

    public bool SelectFirstWhenLoaded { get; set; }

    public bool IsMain => Level == 0;

    public bool HasEntries => Entries.Count > 0;

    /// <summary>Suche (oben) gibt es nur im Hauptmenü – sie durchsucht global den ganzen Stammordner.</summary>
    private bool ShowSearchRow => IsMain;

    private int SearchOffset => ShowSearchRow ? searchH : 0;

    private Rectangle ListRect => new(BorderW, BorderW + SearchOffset, Width - (2 * BorderW), ListHeight);

    private int ListHeight => Height - (2 * BorderW) - SearchOffset;

    private Rectangle SearchRect => new(BorderW, BorderW, Width - (2 * BorderW), searchH);

    private int ContentHeight => Math.Max(1, Entries.Count) * rowH;

    private bool HasScrollbar => ContentHeight > ListHeight;

    private int MaxScroll => Math.Max(0, ContentHeight - ListHeight);

    protected override bool ShowWithoutActivation => !IsMain;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
            if (!IsMain)
            {
                cp.ExStyle |= Native.WS_EX_NOACTIVATE;
            }

            cp.ClassStyle |= Native.CS_DROPSHADOW;
            return cp;
        }
    }

    public void Configure(Theme theme, float scale, AppSettings settings)
    {
        this.theme = theme;
        this.scale = scale;
        rowH = Px(24);
        iconSize = Px(16);
        padX = Px(6);
        gapX = Px(8);
        arrowW = Px(18);
        searchH = Px(30);
        scrollW = Math.Max(4, Px(6));
        maxWidth = Px(settings.MaxWidth);
        minWidth = Math.Min(IsMain ? Px(240) : Px(160), maxWidth);
        font?.Dispose();
        smallFont?.Dispose();
        glyphFont?.Dispose();
        glyphFontSmall?.Dispose();
        font = new Font("Segoe UI", 12f * scale, GraphicsUnit.Pixel);
        smallFont = new Font("Segoe UI", 11f * scale, GraphicsUnit.Pixel);
        glyphFont = new Font(Theme.GlyphFontName, 12f * scale, GraphicsUnit.Pixel);
        glyphFontSmall = new Font(Theme.GlyphFontName, 9f * scale, GraphicsUnit.Pixel);
        textWidths.Clear();
        BackColor = theme.Background;
        ApplyCorners();
    }

    public int IconPixelSize => iconSize;

    private int Px(int v) => Math.Max(1, (int)Math.Round(v * scale));

    public void SetLoading()
    {
        loading = true;
        baseEntries = new List<Entry>();
        if (Filter.Length == 0)
        {
            Entries = baseEntries;
        }
    }

    public void SetEntries(List<Entry> list, FolderStatus folderStatus)
    {
        loading = false;
        status = folderStatus;
        baseEntries = list;
        if (Filter.Length == 0)
        {
            Entries = baseEntries;
            ClampSelection();
        }
    }

    public void ShowBase()
    {
        searching = false;
        Entries = baseEntries;
        scroll = 0;
        ClampSelection();
    }

    public void SetSearchResults(List<Entry> results, bool stillSearching)
    {
        searching = stillSearching;
        Entries = results;
        scroll = 0;
        ClampSelection();
    }

    public void ResetState()
    {
        Filter = string.Empty;
        Selected = -1;
        OpenIndex = -1;
        scroll = 0;
        searching = false;
        hoverButton = -1;
        Entries = baseEntries;
    }

    private void ClampSelection()
    {
        if (Selected >= Entries.Count)
        {
            Selected = Entries.Count - 1;
        }

        if (OpenIndex >= Entries.Count)
        {
            OpenIndex = -1;
        }
    }

    /// <summary>Größe neu berechnen.</summary>
    public void Relayout(int maxHeight)
    {
        int textMax = 0;
        using (var g = CreateGraphics())
        {
            foreach (var e in Entries)
            {
                if (!textWidths.TryGetValue(e, out int w))
                {
                    w = TextRenderer.MeasureText(g, e.DisplayName, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                    if (!string.IsNullOrEmpty(e.SubText))
                    {
                        w += gapX + TextRenderer.MeasureText(g, e.SubText, smallFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                    }

                    textWidths[e] = w;
                }

                textMax = Math.Max(textMax, w);
            }

            string msg = StatusMessage();
            if (msg.Length > 0)
            {
                textMax = Math.Max(textMax, TextRenderer.MeasureText(g, msg, font).Width);
            }
        }

        int searchRow = ShowSearchRow ? searchH : 0;
        int wantedListH = Math.Max(1, Entries.Count) * rowH;
        int maxListH = Math.Max(rowH, maxHeight - (2 * BorderW) - searchRow);
        int listH = Math.Min(wantedListH, maxListH - (maxListH % rowH == 0 ? 0 : 0));
        bool needsScroll = wantedListH > listH;
        int width = (2 * BorderW) + padX + iconSize + gapX + textMax + Px(10) + arrowW + (needsScroll ? scrollW : 0);
        width = Math.Clamp(width, minWidth, Math.Max(minWidth, maxWidth));
        int height = listH + searchRow + (2 * BorderW);
        if (Size != new Size(width, height))
        {
            Size = new Size(width, height);
        }

        scroll = Math.Clamp(scroll, 0, MaxScroll);
        Invalidate();
    }

    private string StatusMessage()
    {
        if (Entries.Count > 0)
        {
            return string.Empty;
        }

        if (Filter.Length > 0)
        {
            return searching ? L.Searching : L.NoResults;
        }

        if (loading)
        {
            return L.Loading;
        }

        return status switch
        {
            FolderStatus.NoAccess => L.NoAccess,
            FolderStatus.NotFound => L.FolderNotFound,
            _ => L.EmptyFolder,
        };
    }

    public Rectangle RowRect(int index) => new(BorderW, BorderW + SearchOffset + (index * rowH) - scroll, Width - (2 * BorderW) - (HasScrollbar ? scrollW : 0), rowH);

    /// <summary>Zeilenrechteck in Bildschirmkoordinaten (für Untermenü-Position).</summary>
    public Rectangle RowScreenRect(int index)
    {
        var r = RowRect(index);
        var list = ListRect;
        if (r.Top < list.Top)
        {
            r.Y = list.Top;
        }

        if (r.Bottom > list.Bottom)
        {
            r.Y = list.Bottom - rowH;
        }

        return RectangleToScreen(r);
    }

    public int VisibleRows => Math.Max(1, ListHeight / rowH);

    public void EnsureVisible(int index)
    {
        if (index < 0)
        {
            return;
        }

        int top = index * rowH;
        if (top < scroll)
        {
            scroll = top;
        }
        else if (top + rowH > scroll + ListHeight)
        {
            scroll = top + rowH - ListHeight;
        }

        scroll = Math.Clamp(scroll, 0, MaxScroll);
    }

    public int HitRow(Point client)
    {
        var list = ListRect;
        if (!list.Contains(client) || (HasScrollbar && client.X >= list.Right - scrollW))
        {
            return -1;
        }

        int idx = (client.Y - list.Top + scroll) / rowH;
        return idx >= 0 && idx < Entries.Count ? idx : -1;
    }

    private Rectangle ButtonRect(int i)
    {
        // 0 = Pin, 1 = Ordner öffnen, 2 = Einstellungen (von rechts nach links: 2,1,0)
        var s = SearchRect;
        int bw = searchH;
        int x = s.Right - ((3 - i) * bw);
        return new Rectangle(x, s.Top, bw, s.Height);
    }

    private int HitButton(Point client)
    {
        if (!IsMain)
        {
            return -1;
        }

        for (int i = 0; i < 3; i++)
        {
            if (ButtonRect(i).Contains(client))
            {
                return i;
            }
        }

        return -1;
    }

    private Rectangle ThumbRect()
    {
        var list = ListRect;
        int trackH = list.Height;
        int thumbH = Math.Max(Px(24), (int)((long)trackH * trackH / ContentHeight));
        int maxScroll = MaxScroll;
        int y = list.Top + (maxScroll == 0 ? 0 : (int)((long)(trackH - thumbH) * scroll / maxScroll));
        return new Rectangle(list.Right - scrollW, y, scrollW, thumbH);
    }

    public void ScrollBy(int delta)
    {
        int old = scroll;
        scroll = Math.Clamp(scroll + delta, 0, MaxScroll);
        if (old != scroll)
        {
            Invalidate();
            controller.OnScrolled(this);
        }
    }

    private void ApplyCorners()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        Native.SetSharpCorners(Handle);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyCorners();
        try
        {
            Theme.AllowDarkModeForWindow(Handle, true);
        }
        catch
        {
            // ältere Windows-Versionen
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (ShellContextMenu.Current != null && ShellContextMenu.Current.HandleMessage(ref m))
        {
            return;
        }

        if (m.Msg == Native.WM_MOUSEACTIVATE && !IsMain)
        {
            m.Result = (IntPtr)Native.MA_NOACTIVATE;
            return;
        }

        if (m.Msg == Native.WM_DPICHANGED)
        {
            // Größe/Skalierung verwaltet der Controller selbst
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (controller.HandleKey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!char.IsControl(e.KeyChar))
        {
            controller.HandleChar(e.KeyChar);
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(theme.Background);
        var list = ListRect;
        g.SetClip(list);

        int first = Math.Max(0, scroll / rowH);
        int last = Math.Min(Entries.Count - 1, (scroll + list.Height) / rowH);
        for (int i = first; i <= last; i++)
        {
            DrawRow(g, i);
        }

        string msg = StatusMessage();
        if (msg.Length > 0)
        {
            var r = new Rectangle(list.Left + padX + iconSize + gapX, list.Top, list.Width - padX - iconSize - gapX, rowH);
            TextRenderer.DrawText(g, msg, font, r, theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        if (HasScrollbar)
        {
            var thumb = ThumbRect();
            using var b = new SolidBrush(theme.Scrollbar);
            int inset = Math.Max(1, scrollW / 4);
            g.FillRectangle(b, new Rectangle(thumb.X + inset, thumb.Y + inset, thumb.Width - (2 * inset), thumb.Height - (2 * inset)));
        }

        g.ResetClip();

        if (ShowSearchRow)
        {
            DrawSearchRow(g);
        }

        using (var pen = new Pen(theme.Border))
        {
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    private void DrawRow(Graphics g, int i)
    {
        var e = Entries[i];
        var r = RowRect(i);
        bool isOpen = i == OpenIndex;
        bool isSel = i == Selected || i == dropRow;
        if (isOpen || isSel)
        {
            var fill = isOpen ? theme.OpenFolder : theme.Selected;
            var border = isOpen ? theme.OpenFolderBorder : theme.SelectedBorder;
            if (isOpen && isSel && !controller.IsKeyboardFocus(this))
            {
                fill = theme.OpenFolder;
            }

            using var b = new SolidBrush(fill);
            g.FillRectangle(b, r);
            using var p = new Pen(border);
            g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        int iconX = r.X + padX;
        int iconY = r.Y + ((rowH - iconSize) / 2);
        if (e.Icon == null && !e.IconRequested)
        {
            var cached = IconLoader.TryGetCached(e.Path, iconSize);
            if (cached != null)
            {
                e.Icon = cached;
                e.IconRequested = true;
            }
            else
            {
                IconLoader.Request(e, iconSize, () =>
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(Invalidate);
                    }
                });
            }
        }

        if (e.Icon != null)
        {
            g.InterpolationMode = e.Icon.Width == iconSize ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.DrawImage(e.Icon, new Rectangle(iconX, iconY, iconSize, iconSize));
        }

        int textX = iconX + iconSize + gapX;
        int textRight = r.Right - arrowW - Px(4);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        var textRect = new Rectangle(textX, r.Y, Math.Max(0, textRight - textX), r.Height);
        if (string.IsNullOrEmpty(e.SubText))
        {
            TextRenderer.DrawText(g, e.DisplayName, font, textRect, theme.Text, flags);
        }
        else
        {
            int nameW = Math.Min(textRect.Width, TextRenderer.MeasureText(g, e.DisplayName, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width);
            TextRenderer.DrawText(g, e.DisplayName, font, new Rectangle(textRect.X, textRect.Y, nameW, textRect.Height), theme.Text, flags);
            var subRect = new Rectangle(textRect.X + nameW + gapX, textRect.Y, Math.Max(0, textRect.Width - nameW - gapX), textRect.Height);
            if (subRect.Width > Px(20))
            {
                TextRenderer.DrawText(g, e.SubText, smallFont, subRect, theme.TextDim, flags | TextFormatFlags.PathEllipsis);
            }
        }

        if (e.IsFolder)
        {
            var ar = new Rectangle(r.Right - arrowW - Px(2), r.Y, arrowW, r.Height);
            TextRenderer.DrawText(g, Theme.GlyphChevronRight, glyphFontSmall, ar, theme.Glyph,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private void DrawSearchRow(Graphics g)
    {
        var s = SearchRect;
        using (var b = new SolidBrush(theme.SearchField))
        {
            g.FillRectangle(b, s);
        }

        using (var p = new Pen(theme.Border))
        {
            g.DrawLine(p, s.Left, s.Bottom - 1, s.Right, s.Bottom - 1);
        }

        var glyphRect = new Rectangle(s.Left + padX, s.Top, iconSize, s.Height);
        TextRenderer.DrawText(g, Theme.GlyphSearch, glyphFont, glyphRect, theme.Glyph,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        int textX = glyphRect.Right + gapX;
        int textRight = IsMain ? ButtonRect(0).Left - Px(4) : s.Right - padX;
        var tr = new Rectangle(textX, s.Top, Math.Max(0, textRight - textX), s.Height);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        if (Filter.Length == 0)
        {
            TextRenderer.DrawText(g, L.Search, font, tr, theme.TextDim, flags | TextFormatFlags.EndEllipsis);
        }
        else
        {
            // Rechtsbündig abschneiden, damit das Ende (mit Cursor) sichtbar bleibt
            var size = TextRenderer.MeasureText(g, Filter, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            var f2 = size.Width > tr.Width ? flags | TextFormatFlags.Right : flags;
            TextRenderer.DrawText(g, Filter, font, tr, theme.Text, f2);
            int caretX = Math.Min(tr.Right, tr.Left + size.Width) + 1;
            if (controller.IsKeyboardFocus(this))
            {
                using var p = new Pen(theme.Text);
                g.DrawLine(p, caretX, s.Top + Px(8), caretX, s.Bottom - Px(8));
            }
        }

        if (IsMain)
        {
            string[] glyphs = { controller.Pinned ? Theme.GlyphPinned : Theme.GlyphPin, Theme.GlyphFolder, Theme.GlyphSettings };
            for (int i = 0; i < 3; i++)
            {
                var br = ButtonRect(i);
                if (i == hoverButton)
                {
                    using var hb = new SolidBrush(theme.ButtonHover);
                    g.FillRectangle(hb, Rectangle.Inflate(br, -Px(3), -Px(3)));
                }

                var color = i == hoverButton ? theme.GlyphHover : (i == 0 && controller.Pinned ? theme.Accent : theme.Glyph);
                TextRenderer.DrawText(g, glyphs[i], glyphFont, br, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        controller.OnMenuMouseEnter(this);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverButton != -1)
        {
            hoverButton = -1;
            Invalidate();
        }

        controller.OnMenuMouseLeave(this);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (thumbDragging)
        {
            var list = ListRect;
            var thumb = ThumbRect();
            int track = list.Height - thumb.Height;
            if (track > 0)
            {
                int newScroll = thumbDragStartScroll + (int)((long)(e.Y - thumbDragStartY) * MaxScroll / track);
                ScrollBy(newScroll - scroll);
            }

            return;
        }

        int btn = HitButton(e.Location);
        if (btn != hoverButton)
        {
            hoverButton = btn;
            Invalidate();
        }

        if (e.Button == MouseButtons.Left && mouseDownRow >= 0)
        {
            var drag = SystemInformation.DragSize;
            if (Math.Abs(e.X - mouseDownPoint.X) > drag.Width || Math.Abs(e.Y - mouseDownPoint.Y) > drag.Height)
            {
                int row = mouseDownRow;
                mouseDownRow = -1;
                controller.StartDrag(this, row);
                return;
            }
        }

        controller.OnRowHover(this, HitRow(e.Location));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && HasScrollbar && ThumbRect().Contains(e.Location))
        {
            thumbDragging = true;
            thumbDragStartY = e.Y;
            thumbDragStartScroll = scroll;
            Capture = true;
            return;
        }

        if (e.Button == MouseButtons.Left && HasScrollbar && e.X >= ListRect.Right - scrollW && ListRect.Contains(e.Location))
        {
            // Klick in die Scrollleiste → seitenweise
            ScrollBy(e.Y < ThumbRect().Top ? -ListHeight : ListHeight);
            return;
        }

        mouseDownRow = e.Button == MouseButtons.Left ? HitRow(e.Location) : -1;
        mouseDownPoint = e.Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (thumbDragging)
        {
            thumbDragging = false;
            Capture = false;
            return;
        }

        int row = HitRow(e.Location);
        if (e.Button == MouseButtons.Left)
        {
            int btn = HitButton(e.Location);
            if (btn >= 0)
            {
                controller.OnButton(btn);
            }
            else if (row >= 0 && row == mouseDownRow)
            {
                controller.OnRowClick(this, row, ModifierKeys);
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (row >= 0)
            {
                controller.OnRowContext(this, row, PointToScreen(e.Location));
            }
            else if (HitButton(e.Location) < 0)
            {
                controller.OnBackgroundContext(this, PointToScreen(e.Location));
            }
        }
        else if (e.Button == MouseButtons.Middle && row >= 0)
        {
            controller.OnRowMiddleClick(this, row);
        }

        mouseDownRow = -1;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int row = HitRow(e.Location);
        if (e.Button == MouseButtons.Left && row >= 0)
        {
            controller.OnRowDoubleClick(this, row);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int lines = SystemInformation.MouseWheelScrollLines;
        if (lines <= 0)
        {
            lines = 3;
        }

        ScrollBy(-e.Delta * lines * rowH / 120);
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        UpdateDrop(e);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        UpdateDrop(e);
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        SetDropRow(-1);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        int row = dropRow;
        SetDropRow(-1);
        if (e.Data != null)
        {
            controller.OnDrop(this, row, e.Data);
        }
    }

    private void UpdateDrop(DragEventArgs e)
    {
        if (!controller.CanAcceptDrop(e.Data))
        {
            e.Effect = DragDropEffects.None;
            SetDropRow(-1);
            return;
        }

        e.Effect = (e.AllowedEffect & DragDropEffects.Link) != 0 ? DragDropEffects.Link : DragDropEffects.Copy;
        var p = PointToClient(new Point(e.X, e.Y));
        int row = HitRow(p);
        SetDropRow(row >= 0 && Entries[row].IsFolder ? row : -1);
        controller.OnDragHover(this);
    }

    private void SetDropRow(int row)
    {
        if (dropRow != row)
        {
            dropRow = row;
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            font?.Dispose();
            smallFont?.Dispose();
            glyphFont?.Dispose();
            glyphFontSmall?.Dispose();
        }

        base.Dispose(disposing);
    }
}

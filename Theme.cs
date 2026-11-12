using System.Drawing.Text;

namespace SmartStart;

/// <summary>Farben (angelehnt an die bisherigen SystemTrayMenu-Einstellungen).</summary>
internal sealed class Theme
{
    public Color Background { get; private init; }
    public Color Border { get; private init; }
    public Color Text { get; private init; }
    public Color TextDim { get; private init; }
    public Color Selected { get; private init; }
    public Color SelectedBorder { get; private init; }
    public Color OpenFolder { get; private init; }
    public Color OpenFolderBorder { get; private init; }
    public Color SearchField { get; private init; }
    public Color Glyph { get; private init; }
    public Color GlyphHover { get; private init; }
    public Color ButtonHover { get; private init; }
    public Color Scrollbar { get; private init; }
    public Color Accent { get; private init; }

    public static Theme Dark { get; } = new()
    {
        Background = ColorTranslator.FromHtml("#202020"),
        Border = ColorTranslator.FromHtml("#000000"),
        Text = ColorTranslator.FromHtml("#F2F2F2"),
        TextDim = ColorTranslator.FromHtml("#8A8F94"),
        Selected = ColorTranslator.FromHtml("#333333"),
        SelectedBorder = ColorTranslator.FromHtml("#141D4B"),
        OpenFolder = ColorTranslator.FromHtml("#14412A"),
        OpenFolderBorder = ColorTranslator.FromHtml("#144B55"),
        SearchField = ColorTranslator.FromHtml("#191919"),
        Glyph = ColorTranslator.FromHtml("#95A0A6"),
        GlyphHover = ColorTranslator.FromHtml("#FFFFFF"),
        ButtonHover = ColorTranslator.FromHtml("#373737"),
        Scrollbar = ColorTranslator.FromHtml("#5A5A5A"),
        Accent = ColorTranslator.FromHtml("#4CC2FF"),
    };

    public static Theme Light { get; } = new()
    {
        Background = ColorTranslator.FromHtml("#FFFFFF"),
        Border = ColorTranslator.FromHtml("#9A9A9A"),
        Text = ColorTranslator.FromHtml("#000000"),
        TextDim = ColorTranslator.FromHtml("#6E6E6E"),
        Selected = ColorTranslator.FromHtml("#CCE8FF"),
        SelectedBorder = ColorTranslator.FromHtml("#99D1FF"),
        OpenFolder = ColorTranslator.FromHtml("#C2F5DE"),
        OpenFolderBorder = ColorTranslator.FromHtml("#99FFA5"),
        SearchField = ColorTranslator.FromHtml("#F3F3F3"),
        Glyph = ColorTranslator.FromHtml("#606A70"),
        GlyphHover = ColorTranslator.FromHtml("#000000"),
        ButtonHover = ColorTranslator.FromHtml("#DADADA"),
        Scrollbar = ColorTranslator.FromHtml("#B0B0B0"),
        Accent = ColorTranslator.FromHtml("#0067C0"),
    };

    private static string? glyphFontName;

    /// <summary>Segoe Fluent Icons (Win11) bzw. Segoe MDL2 Assets (Win10).</summary>
    public static string GlyphFontName
    {
        get
        {
            if (glyphFontName == null)
            {
                using var fonts = new InstalledFontCollection();
                glyphFontName = fonts.Families.Any(f => f.Name == "Segoe Fluent Icons")
                    ? "Segoe Fluent Icons"
                    : "Segoe MDL2 Assets";
            }

            return glyphFontName;
        }
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int mode);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", EntryPoint = "#133")]
    public static extern bool AllowDarkModeForWindow(IntPtr hwnd, bool allow);

    /// <summary>Dunkle/helle Win32-Kontextmenüs (Explorer-Menü) passend zum Design.</summary>
    public static void ApplyNativeMenuTheme(ThemeMode mode)
    {
        try
        {
            SetPreferredAppMode(mode switch
            {
                ThemeMode.Dark => 2,   // ForceDark
                ThemeMode.Light => 3,  // ForceLight
                _ => 1,                // AllowDark (folgt Windows)
            });
            FlushMenuThemes();
        }
        catch
        {
            // ältere Windows-Versionen
        }
    }

    /// <summary>Renderer für das Tray-Menü.</summary>
    public static ToolStripRenderer CreateMenuRenderer(bool dark) =>
        dark ? new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false } : new ToolStripProfessionalRenderer { RoundedEdges = false };

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        private static readonly Color Bg = ColorTranslator.FromHtml("#2B2B2B");
        private static readonly Color Sel = ColorTranslator.FromHtml("#3D3D3D");
        private static readonly Color Line = ColorTranslator.FromHtml("#454545");

        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Sel;
        public override Color MenuItemSelected => Sel;
        public override Color MenuItemSelectedGradientBegin => Sel;
        public override Color MenuItemSelectedGradientEnd => Sel;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Bg;
        public override Color CheckBackground => Sel;
        public override Color CheckSelectedBackground => Sel;
        public override Color CheckPressedBackground => Sel;
    }

    public const string GlyphChevronRight = "";
    public const string GlyphSearch = "";
    public const string GlyphPin = "";
    public const string GlyphPinned = "";
    public const string GlyphFolder = "";
    public const string GlyphSettings = "";
}

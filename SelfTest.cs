namespace SmartStart;

/// <summary>
/// Interner Selbsttest (--selftest &lt;ordner&gt;): steuert den Controller ohne echte Tastatur-/Mauseingaben
/// und speichert Bilder aller offenen Menüs. Nur für Entwicklung.
/// </summary>
internal static class SelfTest
{
    public static async Task Run(MenuController c, string outDir, Action exit)
    {
        Directory.CreateDirectory(outDir);
        try
        {
            c.Open();
            await Task.Delay(1200);
            c.SetPinned(true);
            Snap(c, outDir, "01_main");

            Press(c, Keys.Down, Keys.Down, Keys.Down, Keys.Right);
            await Task.Delay(700);
            Press(c, Keys.Down, Keys.Right);
            await Task.Delay(700);
            Snap(c, outDir, "02_tools_deep");

            Press(c, Keys.Left, Keys.Left);
            await Task.Delay(300);
            Press(c, Keys.Up);
            await Task.Delay(700);
            Snap(c, outDir, "03_spiele_hover_open");

            Press(c, Keys.Right, Keys.End);
            await Task.Delay(300);
            Snap(c, outDir, "04_spiele_end_scrolled");

            Press(c, Keys.Left);
            foreach (char ch in "deep")
            {
                c.HandleChar(ch);
            }

            await Task.Delay(1500);
            Snap(c, outDir, "05_search_deep");

            Press(c, Keys.Escape);
            foreach (char ch in "spiel 1")
            {
                c.HandleChar(ch);
            }

            await Task.Delay(800);
            Snap(c, outDir, "06_search_spiel1");

            Press(c, Keys.Escape);
            await Task.Delay(200);
            Snap(c, outDir, "07_after_escape_clears");
            Press(c, Keys.Escape);
            await Task.Delay(200);
            Log.Write($"SelfTest: nach 2x Esc offen={c.IsOpen}");
        }
        catch (Exception ex)
        {
            Log.Write("SelfTest FEHLER: " + ex);
        }

        exit();
    }

    private static void Press(MenuController c, params Keys[] keys)
    {
        foreach (var k in keys)
        {
            c.HandleKey(k);
        }
    }

    private static void Snap(MenuController c, string outDir, string name)
    {
        var forms = c.OpenForms.Where(f => f.Visible).ToList();
        Log.Write($"SelfTest {name}: " + string.Join(" | ", forms.Select(f =>
            $"L{f.Level} {f.Bounds} sel={f.Selected} open={f.OpenIndex} filter='{f.Filter}' n={f.Entries.Count}")));
        if (forms.Count == 0)
        {
            return;
        }

        var all = forms.Select(f => f.Bounds).Aggregate(Rectangle.Union);
        using var bmp = new Bitmap(all.Width, all.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Magenta);
        }

        foreach (var f in forms)
        {
            using var fb = new Bitmap(f.Width, f.Height);
            f.DrawToBitmap(fb, new Rectangle(0, 0, f.Width, f.Height));
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(fb, f.Left - all.Left, f.Top - all.Top);
        }

        bmp.Save(Path.Combine(outDir, name + ".png"));
    }
}


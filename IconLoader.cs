using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SmartStart;

/// <summary>Lädt Shell-Icons auf einem eigenen STA-Thread und cached sie.</summary>
internal static class IconLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly BlockingCollection<(Entry Entry, int Size, Action Done)> Queue = new();
    private static Thread? worker;

    public static void Request(Entry entry, int size, Action done)
    {
        if (entry.IconRequested)
        {
            return;
        }

        entry.IconRequested = true;
        string key = Key(entry.Path, size);
        if (Cache.TryGetValue(key, out var bmp))
        {
            entry.Icon = bmp;
            return;
        }

        EnsureWorker();
        Queue.Add((entry, size, done));
    }

    /// <summary>Icon synchron aus dem Cache holen (null, wenn noch nicht geladen).</summary>
    public static Bitmap? TryGetCached(string path, int size)
    {
        return Cache.TryGetValue(Key(path, size), out var bmp) ? bmp : null;
    }

    public static void Invalidate(string path)
    {
        foreach (string key in Cache.Keys.Where(k => k.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            Cache.TryRemove(key, out _);
        }
    }

    public static void Clear()
    {
        Cache.Clear();
    }

    private static string Key(string path, int size) => path + "|" + size;

    private static void EnsureWorker()
    {
        if (worker != null)
        {
            return;
        }

        worker = new Thread(Run) { IsBackground = true, Name = "IconLoader" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    private static void Run()
    {
        foreach (var (entry, size, done) in Queue.GetConsumingEnumerable())
        {
            string key = Key(entry.Path, size);
            if (!Cache.TryGetValue(key, out var bmp))
            {
                bmp = Load(entry.Path, size);
                if (Cache.Count > 4000)
                {
                    Cache.Clear();
                }

                Cache[key] = bmp;
            }

            entry.Icon = bmp;
            try
            {
                done();
            }
            catch
            {
                // Formular evtl. schon geschlossen
            }
        }
    }

    public static Bitmap? Load(string path, int size)
    {
        try
        {
            Native.SHCreateItemFromParsingName(path, IntPtr.Zero, Native.IID_IShellItemImageFactory, out var factory);
            try
            {
                // SIIGBF_ICONONLY (0x4): keine Vorschaubilder, nur Icons
                int hr = factory.GetImage(new Native.SIZE { cx = size, cy = size }, 0x4, out IntPtr hbm);
                if (hr == 0 && hbm != IntPtr.Zero)
                {
                    try
                    {
                        return FromHBitmap(hbm);
                    }
                    finally
                    {
                        Native.DeleteObject(hbm);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }
        catch
        {
            // Fallback unten
        }

        try
        {
            var info = default(Native.SHFILEINFO);
            Native.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<Native.SHFILEINFO>(),
                Native.SHGFI_ICON | (size <= 16 ? Native.SHGFI_SMALLICON : Native.SHGFI_LARGEICON));
            if (info.hIcon != IntPtr.Zero)
            {
                try
                {
                    using var icon = System.Drawing.Icon.FromHandle(info.hIcon);
                    var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                    using var g = Graphics.FromImage(bmp);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawIcon(icon, new Rectangle(0, 0, size, size));
                    return bmp;
                }
                finally
                {
                    Native.DestroyIcon(info.hIcon);
                }
            }
        }
        catch
        {
            // kein Icon
        }

        return null;
    }

    /// <summary>HBITMAP (32 bpp, premultiplied) → Bitmap mit Alphakanal.</summary>
    private static unsafe Bitmap FromHBitmap(IntPtr hbm)
    {
        var dib = default(Native.DIBSECTION);
        if (Native.GetObjectDib(hbm, Marshal.SizeOf<Native.DIBSECTION>(), ref dib) == 0 ||
            dib.dsBm.bmBitsPixel != 32 || dib.dsBm.bmBits == IntPtr.Zero)
        {
            return Image.FromHbitmap(hbm);
        }

        int w = dib.dsBm.bmWidth;
        int h = Math.Abs(dib.dsBm.bmHeight);
        int srcStride = dib.dsBm.bmWidthBytes;
        bool bottomUp = dib.dsBmih.biHeight > 0;
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        bool anyAlpha = false;
        try
        {
            for (int y = 0; y < h; y++)
            {
                byte* src = (byte*)dib.dsBm.bmBits + ((bottomUp ? h - 1 - y : y) * (long)srcStride);
                byte* dst = (byte*)data.Scan0 + (y * (long)data.Stride);
                Buffer.MemoryCopy(src, dst, w * 4, w * 4);
                if (!anyAlpha)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (dst[(x * 4) + 3] != 0)
                        {
                            anyAlpha = true;
                            break;
                        }
                    }
                }
            }

            if (!anyAlpha)
            {
                // Bitmap ohne Alphakanal → voll deckend
                for (int y = 0; y < h; y++)
                {
                    byte* dst = (byte*)data.Scan0 + (y * (long)data.Stride);
                    for (int x = 0; x < w; x++)
                    {
                        dst[(x * 4) + 3] = 255;
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return bmp;
    }
}

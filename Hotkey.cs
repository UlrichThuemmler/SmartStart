namespace SmartStart;

/// <summary>Globales Tastenkürzel über RegisterHotKey (z. B. Strg+Win).</summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private readonly List<int> ids = new();
    private int nextId = 1;
    private DateTime last = DateTime.MinValue;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public event Action? Pressed;

    /// <summary>Registriert das Kürzel. Liefert false, wenn das Haupt-Kürzel nicht registriert werden konnte.</summary>
    public bool Register(string spec, out string error)
    {
        UnregisterAll();
        error = string.Empty;
        if (!TryParse(spec, out uint mods, out Keys key))
        {
            error = L.HotkeyInvalid(spec);
            return false;
        }

        // Ist die Haupttaste selbst eine Modifikatortaste (z. B. Win bei Strg+Win), zählt Windows sie beim
        // Drücken bereits als Modifikator → ihr eigenes Flag muss mit registriert werden.
        mods |= OwnModifier(key);

        if (!RegisterOne(mods, key))
        {
            error = L.HotkeyInUse(spec);
            return false;
        }

        // Win-Taste als Haupttaste: auch die andere Win-Taste und die umgekehrte Reihenfolge (erst Win, dann Strg) erlauben
        if (key is Keys.LWin or Keys.RWin)
        {
            RegisterOne(mods, key == Keys.LWin ? Keys.RWin : Keys.LWin);
            if (mods == (Native.MOD_CONTROL | Native.MOD_WIN))
            {
                RegisterOne(mods, Keys.LControlKey);
                RegisterOne(mods, Keys.RControlKey);
                RegisterOne(mods, Keys.ControlKey);
            }
        }

        return true;
    }

    private static uint OwnModifier(Keys key) => key switch
    {
        Keys.LWin or Keys.RWin => Native.MOD_WIN,
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => Native.MOD_CONTROL,
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => Native.MOD_SHIFT,
        Keys.Menu or Keys.LMenu or Keys.RMenu => Native.MOD_ALT,
        _ => 0,
    };

    private bool RegisterOne(uint mods, Keys key)
    {
        int id = nextId++;
        bool ok = Native.RegisterHotKey(Handle, id, mods | Native.MOD_NOREPEAT, (uint)key);
        Log.Write($"  RegisterHotKey mods={mods} key={key}: {ok}");
        if (ok)
        {
            ids.Add(id);
        }

        return ok;
    }

    public void UnregisterAll()
    {
        foreach (int id in ids)
        {
            Native.UnregisterHotKey(Handle, id);
        }

        ids.Clear();
    }

    public static bool TryParse(string spec, out uint mods, out Keys key)
    {
        mods = 0;
        key = Keys.None;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        string[] parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].ToUpperInvariant();
            bool isLast = i == parts.Length - 1;
            if (!isLast)
            {
                switch (p)
                {
                    case "CTRL":
                    case "STRG":
                    case "CONTROL":
                        mods |= Native.MOD_CONTROL;
                        continue;
                    case "ALT":
                        mods |= Native.MOD_ALT;
                        continue;
                    case "SHIFT":
                    case "UMSCHALT":
                        mods |= Native.MOD_SHIFT;
                        continue;
                    case "WIN":
                    case "LWIN":
                    case "RWIN":
                        mods |= Native.MOD_WIN;
                        continue;
                    default:
                        return false;
                }
            }

            key = p switch
            {
                "WIN" or "LWIN" => Keys.LWin,
                "RWIN" => Keys.RWin,
                "CTRL" or "STRG" or "CONTROL" => Keys.ControlKey,
                "ALT" => Keys.Menu,
                "SHIFT" => Keys.ShiftKey,
                "SPACE" or "LEERTASTE" => Keys.Space,
                "ESC" => Keys.Escape,
                _ => ParseKey(parts[i]),
            };
        }

        return key != Keys.None;
    }

    private static Keys ParseKey(string s)
    {
        if (s.Length == 1 && char.IsDigit(s[0]))
        {
            return Keys.D0 + (s[0] - '0');
        }

        return Enum.TryParse(s, true, out Keys k) ? k : Keys.None;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            var now = DateTime.UtcNow;
            if (now - last > TimeSpan.FromMilliseconds(250))
            {
                last = now;
                Pressed?.Invoke();
            }

            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}

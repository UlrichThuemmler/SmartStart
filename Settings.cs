using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace SmartStart;

internal enum MenuPosition
{
    BottomLeft,
    BottomRight,
    Mouse,
    Center,
}

internal enum ThemeMode
{
    Dark,
    Light,
    System,
}

internal enum HiddenFilesMode
{
    LikeExplorer,
    Always,
    Never,
}

/// <summary>Einstellungen, gespeichert als JSON (standardmäßig %APPDATA%\SmartStart\settings.json).</summary>
internal sealed class AppSettings
{
    public string RootFolder { get; set; } = string.Empty;

    /// <summary>z. B. "Ctrl+LWin", "Ctrl+Alt+Space", "Win+Y".</summary>
    public string Hotkey { get; set; } = "Ctrl+LWin";

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    public MenuPosition Position { get; set; } = MenuPosition.Mouse;

    /// <summary>Skalierung von Zeilenhöhe/Schrift in Prozent.</summary>
    public int SizePercent { get; set; } = 100;

    /// <summary>Maximale Menübreite in Pixeln bei 100 % / 96 DPI.</summary>
    public int MaxWidth { get; set; } = 250;

    /// <summary>Nach dem Start einer Datei/Verknüpfung (kein Ordner-Untermenü) schließt sich das Menü.</summary>
    public bool CloseAfterLaunch { get; set; } = true;

    /// <summary>Einfacher Klick auf Ordner öffnet ihn im Explorer (sonst Doppelklick).</summary>
    public bool OpenFolderWithSingleClick { get; set; }

    public int HoverOpenDelayMs { get; set; } = 100;

    public int CloseDelayMs { get; set; } = 400;

    public HiddenFilesMode HiddenFiles { get; set; } = HiddenFilesMode.LikeExplorer;

    public bool ResolveLinksToFolders { get; set; } = true;

    [JsonIgnore]
    public string FilePath { get; private set; } = string.Empty;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartStart", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load(string path)
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            settings = new AppSettings();
        }

        settings.FilePath = path;
        settings.Clamp();
        return settings;
    }

    public void Save()
    {
        Clamp();
        string? dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        return copy;
    }

    private void Clamp()
    {
        SizePercent = Math.Clamp(SizePercent, 70, 250);
        MaxWidth = Math.Clamp(MaxWidth, 200, 1200);
        HoverOpenDelayMs = Math.Clamp(HoverOpenDelayMs, 0, 2000);
        CloseDelayMs = Math.Clamp(CloseDelayMs, 100, 10000);
    }

    public bool IsDark()
    {
        if (Theme == ThemeMode.Dark)
        {
            return true;
        }

        if (Theme == ThemeMode.Light)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>Autostart über HKCU\...\Run.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SmartStart";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --autostart");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}

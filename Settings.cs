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
        AppSettings? settings = null;
        bool exists = File.Exists(path);
        if (exists)
        {
            // Mehrere Versuche: direkt nach der Anmeldung kann die Datei kurz gesperrt sein
            // (Virenscanner, Synchronisierung). Erst danach auf Standardwerte zurückfallen.
            for (int attempt = 1; attempt <= 10 && settings == null; attempt++)
            {
                try
                {
                    string json = File.ReadAllText(path);
                    settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings == null)
                    {
                        throw new JsonException("Datei ist leer");
                    }
                }
                catch (JsonException ex)
                {
                    // Kaputte Datei aufbewahren statt sie beim nächsten Speichern zu überschreiben
                    Log.Error($"Einstellungen ungültig ({path}): {ex.Message}");
                    try
                    {
                        File.Copy(path, path + ".defekt", true);
                    }
                    catch
                    {
                        // ignorieren
                    }

                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Error($"Einstellungen nicht lesbar, Versuch {attempt}/10 ({path}): {ex.GetType().Name}: {ex.Message}");
                    Thread.Sleep(300);
                }
            }
        }

        settings ??= new AppSettings();
        if (exists && string.IsNullOrWhiteSpace(settings.RootFolder))
        {
            Log.Error($"Einstellungsdatei vorhanden, aber ohne Stammordner ({path})");
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

        // Erst in eine Temp-Datei schreiben, dann ersetzen: so entsteht nie eine halbe Datei
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, FilePath, true);
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

/// <summary>
/// Autostart über HKCU\...\Run. Windows führt Einträge nur aus, wenn sie in der Autostart-Liste
/// (Task-Manager / Einstellungen → Apps → Autostart, gespeichert unter StartupApproved\Run) als
/// aktiv gelten. Beim Einschalten in SmartStart wird der Eintrag dort deshalb ausdrücklich freigegeben.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "SmartStart";

    /// <summary>Eingetragen und nicht im Task-Manager deaktiviert.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is not string s || string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            // Erstes Byte ungerade (z. B. 03) = vom Benutzer deaktiviert
            return approved?.GetValue(ValueName) is not byte[] flags || flags.Length == 0 || (flags[0] & 1) == 0;
        }
    }

    /// <summary>
    /// Ist SmartStart (diese EXE) im Run-Schlüssel eingetragen, fehlt aber die Freigabe in der
    /// Windows-Autostart-Liste, wird sie ergänzt. Eine Deaktivierung durch den Benutzer bleibt bestehen.
    /// </summary>
    public static void RepairApproval()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is not string cmd ||
                Environment.ProcessPath == null ||
                !cmd.Contains(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey);
            if (approved.GetValue(ValueName) == null)
            {
                approved.SetValue(ValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
                Log.Write("Autostart-Freigabe ergänzt");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Autostart-Freigabe: " + ex.Message);
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        using var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --autostart");

            // 02 00 00 00 + 8 Byte Zeitstempel (0) = aktiv
            approved.SetValue(ValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }
        else
        {
            key.DeleteValue(ValueName, false);
            approved.DeleteValue(ValueName, false);
        }
    }
}

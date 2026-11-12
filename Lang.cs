using System.Globalization;

namespace SmartStart;

/// <summary>
/// Oberflächentexte Deutsch/Englisch. Die Sprache richtet sich nach der Windows-Anzeigesprache:
/// Deutsch bei "de-*", sonst Englisch (Fallback). Mit --lang de|en überschreibbar.
/// </summary>
internal static class L
{
    public static bool De { get; private set; }

    public static void Init(string? overrideLanguage)
    {
        string lang = overrideLanguage ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        De = lang.Equals("de", StringComparison.OrdinalIgnoreCase);
    }

    private static string T(string de, string en) => De ? de : en;

    // Menü
    public static string Search => T("Suchen …", "Search …");
    public static string Searching => T("Suche …", "Searching …");
    public static string NoResults => T("Keine Treffer", "No results");
    public static string Loading => T("Lädt …", "Loading …");
    public static string NoAccess => T("Kein Zugriff", "Access denied");
    public static string FolderNotFound => T("Ordner nicht gefunden", "Folder not found");
    public static string EmptyFolder => T("(leer)", "(empty)");

    // Kontextmenü
    public static string ShowInExplorer => T("Im Explorer anzeigen", "Show in Explorer");
    public static string OpenTargetFolder => T("Zielordner öffnen", "Open target folder");
    public static string RefreshMenu => T("Menü aktualisieren", "Refresh menu");

    // Umbenennen / Meldungen
    public static string Rename => T("Umbenennen", "Rename");
    public static string NewName => T("Neuer Name:", "New name:");
    public static string CouldNotStart(string path, string error) =>
        T($"Konnte nicht gestartet werden:\n{path}\n\n{error}", $"Could not start:\n{path}\n\n{error}");
    public static string CouldNotSaveSettings(string error) =>
        T($"Einstellungen konnten nicht gespeichert werden:\n{error}", $"Settings could not be saved:\n{error}");
    public static string HotkeyInvalid(string spec) =>
        T($"Tastenkürzel \"{spec}\" ist ungültig.", $"Hotkey \"{spec}\" is invalid.");
    public static string HotkeyInUse(string spec) =>
        T($"Tastenkürzel \"{spec}\" ist bereits von einem anderen Programm belegt.",
          $"Hotkey \"{spec}\" is already in use by another program.");

    // Tray
    public static string TrayOpenMenu => T("Menü öffnen", "Open menu");
    public static string TrayOpenRoot => T("Stammordner im Explorer öffnen", "Open root folder in Explorer");
    public static string TrayChooseRoot => T("Stammordner wählen …", "Choose root folder …");
    public static string TraySettings => T("Einstellungen …", "Settings …");
    public static string StartWithWindows => T("Mit Windows starten", "Start with Windows");
    public static string TrayExit => T("Beenden", "Exit");
    public static string TrayNoHotkey => T("SmartStart (kein Tastenkürzel)", "SmartStart (no hotkey)");
    public static string ChooseRootDescription => T(
        "Ordner wählen, dessen Inhalt (Verknüpfungen, Dateien, Ordner) im SmartStart-Menü erscheint",
        "Choose the folder whose contents (shortcuts, files, folders) appear in the SmartStart menu");

    // Einstellungen
    public static string SettingsTitle => T("SmartStart – Einstellungen", "SmartStart – Settings");
    public static string RootFolder => T("Stammordner:", "Root folder:");
    public static string ChooseRootShort => T("Stammordner wählen", "Choose root folder");
    public static string Hotkey => T("Tastenkürzel:", "Hotkey:");
    public static string HotkeyDefaultButton => T("Standard (Strg+Win)", "Default (Ctrl+Win)");
    public static string HotkeyHint => T("In das Feld klicken und die gewünschte Kombination drücken.",
        "Click into the field and press the desired key combination.");
    public static string HotkeyCombine => T("Bitte mit Strg/Alt/Umschalt/Win kombinieren", "Please combine with Ctrl/Alt/Shift/Win");
    public static string Design => T("Design:", "Theme:");
    public static string[] DesignOptions => De ? new[] { "Dunkel", "Hell", "Wie Windows" } : new[] { "Dark", "Light", "Like Windows" };
    public static string Position => T("Position:", "Position:");
    public static string[] PositionOptions => De
        ? new[] { "Unten links", "Unten rechts", "Am Mauszeiger", "Bildschirmmitte" }
        : new[] { "Bottom left", "Bottom right", "At mouse pointer", "Screen center" };
    public static string Size => T("Größe (%):", "Size (%):");
    public static string MaxWidth => T("Max. Breite (px):", "Max. width (px):");
    public static string OpenDelay => T("Untermenü öffnen nach (ms):", "Open submenu after (ms):");
    public static string CloseDelay => T("Schließen nach Verlassen (ms):", "Close after leaving (ms):");
    public static string HiddenFiles => T("Versteckte Dateien:", "Hidden files:");
    public static string[] HiddenOptions => De
        ? new[] { "Wie im Explorer", "Immer anzeigen", "Nie anzeigen" }
        : new[] { "Like Explorer", "Always show", "Never show" };
    public static string CloseAfterLaunch => T("Menü nach dem Starten eines Programms schließen", "Close menu after launching a program");
    public static string FolderSingleClick => T("Ordner mit einfachem Klick im Explorer öffnen (sonst Doppelklick)",
        "Open folders in Explorer with a single click (otherwise double-click)");
    public static string ResolveLinks => T("Verknüpfungen zu Ordnern als Untermenü anzeigen", "Show shortcuts to folders as submenus");
    public static string Cancel => T("Abbrechen", "Cancel");

    /// <summary>Tastenkürzel zur Anzeige ("Ctrl+LWin" → "Strg+Win" bzw. "Ctrl+Win").</summary>
    public static string HotkeyDisplay(string spec)
    {
        string s = spec.Replace("LWin", "Win", StringComparison.OrdinalIgnoreCase);
        if (De)
        {
            s = s.Replace("Ctrl", "Strg", StringComparison.OrdinalIgnoreCase)
                 .Replace("Shift", "Umschalt", StringComparison.OrdinalIgnoreCase);
        }

        return s;
    }
}

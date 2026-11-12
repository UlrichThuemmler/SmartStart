# SmartStart

<img src="Resources/SmartStart.png" alt="SmartStart" width="96" align="right">

**Deutsch** · [English](README.md)

Schlankes Schnellstart-Menü für Windows. Zeigt den Inhalt eines Stammordners
(Verknüpfungen, Dateien, Ordner) als Kaskadenmenü am Mauszeiger an – geöffnet mit **Strg+Win**.
Mit globaler Suche über alle Unterordner, Explorer-Kontextmenü und Dark Mode.
Die Oberfläche ist auf Deutsch, wenn Windows auf Deutsch läuft, sonst auf Englisch.

Die Bedienung ist an [SystemTrayMenu](https://github.com/Hofknecht/SystemTrayMenu) angelehnt;
SmartStart ist aber eine eigenständige, komplett neu geschriebene Umsetzung.

## Download

Fertige Version unter **[Releases](https://github.com/UlrichThuemmler/SmartStart/releases/latest)**:

| Datei | Für wen |
|---|---|
| `SmartStart-<Version>-win-x64-standalone.zip` | Läuft sofort, ohne installiertes .NET (ca. 42 MB) |
| `SmartStart-<Version>-win-x64.zip` | Klein (0,2 MB), braucht die [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

ZIP entpacken und **`Install.cmd`** doppelklicken (keine Adminrechte nötig) – oder `SmartStart.exe`
ohne Installation direkt starten. Da das Programm nicht signiert ist, kann Windows SmartScreen beim
ersten Start warnen: „Weitere Informationen“ → „Trotzdem ausführen“.

Voraussetzung: Windows 10/11 (64 Bit).

## Aus dem Quellcode bauen

Benötigt das .NET 10 SDK.

```powershell
.\build.ps1      # erzeugt publish\SmartStart.exe (benötigt .NET 10 Desktop Runtime)
.\install.ps1    # kopiert nach %LOCALAPPDATA%\Programs\SmartStart + Startmenü-Verknüpfung
```

Optionen: `.\install.ps1 -Target "C:\Tools\SmartStart" -Autostart`.
Ohne installiertes .NET: `.\build.ps1 -SelfContained` (größere EXE).
Alternativ einfach `publish\SmartStart.exe` irgendwo hinkopieren und starten – sie ist portabel.

Beim ersten Start wird der Stammordner abgefragt. Danach liegt SmartStart im Infobereich (Tray).
Ein erneuter Start über Windows-Start öffnet das Menü der laufenden Instanz.

## Bedienung

| Eingabe | Funktion |
|---|---|
| Strg+Win (auch Win+Strg) | Menü öffnen/schließen |
| Klick aufs Tray-Symbol | Menü öffnen/schließen |
| Maus über Ordner | Untermenü öffnet sich |
| Klick | Datei/Programm starten (Menü schließt sich danach), Ordner aufklappen |
| Doppelklick auf Ordner | Ordner im Explorer öffnen |
| Strg+Umschalt+Klick / Strg+Umschalt+Enter | Als Administrator starten |
| Rechtsklick | Explorer-Kontextmenü (+ „Im Explorer anzeigen“, „Menü aktualisieren“) |
| Mittelklick | Im Explorer anzeigen |
| Tippen | Globale Suche oben im Menü (ganzer Stammordner inkl. Unterordner, Leerzeichen egal), Enter startet den ersten Treffer |
| ↑ ↓ ← → Bild↑ Bild↓ Pos1 Ende | Navigation |
| Tab / Umschalt+Tab | Zwischen Menüebenen wechseln |
| Esc | Suche leeren bzw. Menü schließen |
| Kontextmenü-Taste / Umschalt+F10 | Kontextmenü des markierten Eintrags |
| F2 | Umbenennen |
| F5 | Neu laden |
| Ziehen aus dem Menü | Datei auf Desktop/in Programme ziehen |
| Dateien/Links ins Menü ziehen | Verknüpfung (.lnk / .url) im Ordner anlegen |

Das Menü bleibt offen, solange es den Fokus hat oder die Maus darüber ist (z. B. um mehrere Programme
nacheinander zu starten), und schließt sich nach dem Verlassen. Mit der Pinnadel bleibt es offen.

## Einstellungen

Über das Zahnrad im Menü oder Rechtsklick aufs Tray-Symbol. Gespeichert in
`%APPDATA%\SmartStart\settings.json`: Stammordner, Tastenkürzel, Design (dunkel/hell/System),
Position (Standard: am Mauszeiger; sonst unten links/rechts, Mitte), Größe, Breite, Verzögerungen, versteckte Dateien und
Autostart. Alle Fenster und Menüs haben harte Ecken (keine Windows-11-Rundung).

## Entwicklung

- .NET 10, WinForms, komplett selbst gezeichnete Menüs (`MenuForm`), Logik in `MenuController`.
- Texte Deutsch/Englisch in `Lang.cs`; die Sprache folgt der Windows-Anzeigesprache (Fallback Englisch).
- `--config <datei>` andere Einstellungsdatei, `--log <datei>` Diagnose-Log, `--lang de|en` Sprache erzwingen,
  `--selftest <ordner>` interner Test ohne echte Eingaben (speichert Bilder der Menüs).
- Icon neu erzeugen: `tools\make-icon.ps1`.
- Release-Pakete erzeugen: `release.ps1` (legt beide ZIPs in `release\` ab; Version aus `SmartStart.csproj`).

## Lizenz

[MIT](LICENSE) © 2026 Ulrich Thümmler

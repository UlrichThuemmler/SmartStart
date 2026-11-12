# SmartStart

<img src="Resources/SmartStart.png" alt="SmartStart" width="96" align="right">

[Deutsch](README.de.md) · **English**

A lightweight quick-launch menu for Windows. Shows the contents of a root folder
(shortcuts, files, folders) as a cascading menu right at your mouse pointer – opened with **Ctrl+Win**.
With global search across all subfolders, the Explorer context menu and dark mode.
The interface is in German on German Windows and in English everywhere else.

Its handling is inspired by [SystemTrayMenu](https://github.com/Hofknecht/SystemTrayMenu);
SmartStart is an independent implementation written from scratch.

## Download

Get the latest version from **[Releases](https://github.com/UlrichThuemmler/SmartStart/releases/latest)**:

| File | For whom |
|---|---|
| `SmartStart-<version>-win-x64-standalone.zip` | Runs right away, no .NET installation needed (approx. 42 MB) |
| `SmartStart-<version>-win-x64.zip` | Small (0.2 MB), requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

Unzip and double-click **`Install.cmd`** (no admin rights needed) – or run `SmartStart.exe`
directly without installing. The program is not code-signed, so Windows SmartScreen may warn on
first start: "More info" → "Run anyway".

Requirement: Windows 10/11 (64-bit).

## Building from source

Requires the .NET 10 SDK.

```powershell
.\build.ps1      # creates publish\SmartStart.exe (requires the .NET 10 Desktop Runtime)
.\install.ps1    # copies it to %LOCALAPPDATA%\Programs\SmartStart + Start menu shortcut
```

Options: `.\install.ps1 -Target "C:\Tools\SmartStart" -Autostart`.
Without .NET installed: `.\build.ps1 -SelfContained` (larger EXE).
Or simply copy `publish\SmartStart.exe` anywhere and run it – it is portable.

On first start SmartStart asks for the root folder. After that it lives in the notification area (tray).
Launching it again from Windows Start opens the menu of the running instance.

## Usage

| Input | Action |
|---|---|
| Ctrl+Win (also Win+Ctrl) | Open/close the menu |
| Click on the tray icon | Open/close the menu |
| Hover over a folder | Opens its submenu |
| Click | Launch file/program (the menu closes afterwards), expand folder |
| Double-click a folder | Open the folder in Explorer |
| Ctrl+Shift+click / Ctrl+Shift+Enter | Run as administrator |
| Right-click | Explorer context menu (+ "Show in Explorer", "Refresh menu") |
| Middle-click | Show in Explorer |
| Type | Global search at the top of the menu (whole root folder incl. subfolders, spaces ignored), Enter launches the first hit |
| ↑ ↓ ← → PgUp PgDn Home End | Navigation |
| Tab / Shift+Tab | Switch between menu levels |
| Esc | Clear the search, or close the menu |
| Menu key / Shift+F10 | Context menu of the selected entry |
| F2 | Rename |
| F5 | Reload |
| Drag from the menu | Drop a file onto the desktop or into programs |
| Drag files/links into the menu | Creates a shortcut (.lnk / .url) in that folder |

The menu stays open while it has focus or the mouse is over it (e.g. to launch several programs
in a row) and closes after you leave it. Use the pin button to keep it open.

## Settings

Via the gear icon in the menu or by right-clicking the tray icon. Stored in
`%APPDATA%\SmartStart\settings.json`: root folder, hotkey, theme (dark/light/system),
position (default: at the mouse pointer; or bottom left/right, center), size, width, delays, hidden files
and autostart. All windows and menus have square corners (no Windows 11 rounding).

## Development

- .NET 10, WinForms, fully custom-drawn menus (`MenuForm`), logic in `MenuController`.
- German/English texts in `Lang.cs`; the language follows the Windows display language (fallback: English).
- `--config <file>` alternative settings file, `--log <file>` diagnostic log, `--lang de|en` force a language,
  `--selftest <folder>` internal test without real input (saves images of the menus).
- Regenerate the icon: `tools\make-icon.ps1`.
- Build release packages: `release.ps1` (puts both ZIPs into `release\`; version taken from `SmartStart.csproj`).

## License

[MIT](LICENSE) © 2026 Ulrich Thümmler

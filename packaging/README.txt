SmartStart – quick-launch menu for Windows
==========================================
(Deutsch: siehe LIESMICH.txt)

Shows the contents of a folder (shortcuts, files, subfolders) as a cascading
menu right at your mouse pointer. Open and close it with Ctrl+Win.
The interface is in German on German Windows, otherwise in English.

INSTALL
  Double-click "Install.cmd".
  -> copies SmartStart to %LOCALAPPDATA%\Programs\SmartStart,
     adds a Start menu entry and starts the program.
  No administrator rights needed.

WITHOUT INSTALLING (portable)
  Just copy SmartStart.exe anywhere and run it.

FIRST START
  SmartStart asks for the root folder whose contents appear in the menu.
  After that it lives in the notification area of the taskbar (tray).
  Settings: gear icon in the menu, or right-click the tray icon.
  "Start with Windows" can be switched on there as well.

WINDOWS WARNING
  The program is not digitally signed. On first start Windows may show
  "Windows protected your PC".
  -> "More info" -> "Run anyway".

REQUIREMENTS
  Windows 10/11 (64-bit).
  Package "win-x64": requires the .NET 10 Desktop Runtime
    (https://dotnet.microsoft.com/download/dotnet/10.0 -> ".NET Desktop Runtime").
  Package "win-x64-standalone": runs without .NET installed.

UNINSTALL
  Double-click "Uninstall.cmd" (your settings are kept).

Source code, documentation and license (MIT):
  https://github.com/UlrichThuemmler/SmartStart

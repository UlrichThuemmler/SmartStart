@echo off
rem Removes SmartStart (program, Start menu shortcut, autostart). Settings are kept.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
pause

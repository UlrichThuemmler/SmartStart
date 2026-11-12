@echo off
rem Entfernt SmartStart (Programm, Startmenue-Eintrag, Autostart). Einstellungen bleiben erhalten.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
pause

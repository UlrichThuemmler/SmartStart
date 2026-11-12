@echo off
rem Installs SmartStart for the current user (no admin rights needed).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 pause

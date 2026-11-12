# Entfernt SmartStart (Programmdateien, Startmenü-Verknüpfung, Autostart).
# Die Einstellungen in %APPDATA%\SmartStart bleiben erhalten, außer mit -RemoveSettings.
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\SmartStart'),
    [switch]$RemoveSettings
)

$exe = Join-Path $Target 'SmartStart.exe'
if (Test-Path $exe) { Start-Process $exe -ArgumentList '--quit' -Wait; Start-Sleep -Milliseconds 500 }
Get-Process SmartStart -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SmartStart' -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'SmartStart.lnk') -ErrorAction SilentlyContinue
if (Test-Path $Target) { Remove-Item $Target -Recurse -Force }
if ($RemoveSettings) { Remove-Item (Join-Path $env:APPDATA 'SmartStart') -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host 'SmartStart entfernt.'

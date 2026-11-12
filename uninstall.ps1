# Removes SmartStart (program files, Start menu shortcut, autostart entry).
# Settings in %APPDATA%\SmartStart are kept unless -RemoveSettings is given.
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\SmartStart'),
    [switch]$RemoveSettings
)

$de = (Get-UICulture).TwoLetterISOLanguageName -eq 'de'

$exe = Join-Path $Target 'SmartStart.exe'
if (Test-Path $exe) { Start-Process $exe -ArgumentList '--quit' -Wait; Start-Sleep -Milliseconds 500 }
Get-Process SmartStart -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SmartStart' -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'SmartStart.lnk') -ErrorAction SilentlyContinue
if (Test-Path $Target) { Remove-Item $Target -Recurse -Force }
if ($RemoveSettings) { Remove-Item (Join-Path $env:APPDATA 'SmartStart') -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host $(if ($de) { 'SmartStart entfernt.' } else { 'SmartStart removed.' })

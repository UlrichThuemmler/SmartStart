# Installiert SmartStart (ohne Administratorrechte):
#  - kopiert publish\SmartStart.exe nach $Target (Standard: %LOCALAPPDATA%\Programs\SmartStart)
#  - legt eine Verknüpfung im Startmenü an (dann per Windows-Start "SmartStart" aufrufbar)
#  - optional: Autostart mit Windows (-Autostart)
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\SmartStart'),
    [switch]$Autostart,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'publish\SmartStart.exe'
if (-not (Test-Path $src)) { throw "Zuerst build.ps1 ausführen ($src fehlt)." }

# Laufende Instanz beenden
$running = Get-Process SmartStart -ErrorAction SilentlyContinue
if ($running) {
    $exe = $running | Select-Object -First 1 -ExpandProperty Path
    if ($exe) { Start-Process $exe -ArgumentList '--quit' -Wait }
    Start-Sleep -Milliseconds 500
    Get-Process SmartStart -ErrorAction SilentlyContinue | Stop-Process -Force
}

New-Item -ItemType Directory -Force -Path $Target | Out-Null
Copy-Item $src (Join-Path $Target 'SmartStart.exe') -Force
$exePath = Join-Path $Target 'SmartStart.exe'

# Startmenü-Verknüpfung
$programs = [Environment]::GetFolderPath('Programs')
$lnk = Join-Path $programs 'SmartStart.lnk'
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $exePath
$sc.WorkingDirectory = $Target
$sc.Description = 'SmartStart – Schnellstart-Menü (Strg+Win)'
$sc.IconLocation = "$exePath,0"
$sc.Save()

if ($Autostart) {
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SmartStart' -Value "`"$exePath`" --autostart"
}

Write-Host "SmartStart installiert nach $Target"
Write-Host "Startmenü-Verknüpfung: $lnk"
if (-not $NoStart) { Start-Process $exePath }

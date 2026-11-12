# Installs SmartStart for the current user (no admin rights needed):
#  - copies SmartStart.exe (from the download package, or publish\ after build.ps1)
#    to $Target (default: %LOCALAPPDATA%\Programs\SmartStart)
#  - creates a Start menu shortcut (then "SmartStart" can be launched from Windows Start)
#  - optional: start with Windows (-Autostart)
# Messages are shown in German on German Windows, otherwise in English.
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\SmartStart'),
    [switch]$Autostart,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$de = (Get-UICulture).TwoLetterISOLanguageName -eq 'de'
function T([string]$german, [string]$english) { if ($de) { $german } else { $english } }

# Download package: EXE next to this script. Source code: publish\ after build.ps1.
$src = Join-Path $PSScriptRoot 'SmartStart.exe'
if (-not (Test-Path $src)) { $src = Join-Path $PSScriptRoot 'publish\SmartStart.exe' }
if (-not (Test-Path $src)) {
    throw (T 'SmartStart.exe nicht gefunden. Im Quellcode zuerst build.ps1 ausführen.' 'SmartStart.exe not found. When building from source, run build.ps1 first.')
}

# Stop a running instance
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

# Start menu shortcut
$programs = [Environment]::GetFolderPath('Programs')
$lnk = Join-Path $programs 'SmartStart.lnk'
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $exePath
$sc.WorkingDirectory = $Target
$sc.Description = (T 'SmartStart – Schnellstart-Menü (Strg+Win)' 'SmartStart – quick-launch menu (Ctrl+Win)')
$sc.IconLocation = "$exePath,0"
$sc.Save()

if ($Autostart) {
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SmartStart' -Value "`"$exePath`" --autostart"
}

Write-Host (T "SmartStart installiert nach $Target" "SmartStart installed to $Target")
Write-Host (T "Startmenü-Verknüpfung: $lnk" "Start menu shortcut: $lnk")
if (-not $NoStart) { Start-Process $exePath }

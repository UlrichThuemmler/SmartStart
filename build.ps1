# Baut SmartStart als einzelne SmartStart.exe nach .\publish
# Standard: benötigt die .NET 10 Desktop Runtime (klein, ~0,5 MB).
# Mit -SelfContained läuft die EXE auch ohne installiertes .NET (ca. 150 MB).
param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$out = Join-Path $PSScriptRoot 'publish'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

$sc = if ($SelfContained) { 'true' } else { 'false' }
dotnet publish SmartStart.csproj -c Release -r win-x64 --self-contained $sc `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen" }
Get-ChildItem $out | Format-Table Name, Length

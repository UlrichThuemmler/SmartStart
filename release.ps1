# Erzeugt die Download-Pakete für ein GitHub-Release in .\release:
#   SmartStart-<Version>-win-x64.zip             (klein, braucht .NET 10 Desktop Runtime)
#   SmartStart-<Version>-win-x64-standalone.zip  (läuft ohne installiertes .NET)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$proj = Get-Content 'SmartStart.csproj'
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$rel = Join-Path $PSScriptRoot 'release'
$tmp = Join-Path $rel '_build'
if (Test-Path $rel) { Remove-Item $rel -Recurse -Force }
New-Item -ItemType Directory -Force $tmp | Out-Null

function Publish([string]$out, [bool]$selfContained) {
    $sc = if ($selfContained) { 'true' } else { 'false' }
    dotnet publish SmartStart.csproj -c Release -r win-x64 --self-contained $sc `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=$sc -p:DebugType=none -o $out | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen ($out)" }
}

function Package([string]$exeDir, [string]$zipName) {
    $pkg = Join-Path $tmp ([IO.Path]::GetFileNameWithoutExtension($zipName))
    New-Item -ItemType Directory -Force $pkg | Out-Null
    Copy-Item (Join-Path $exeDir 'SmartStart.exe') $pkg
    Copy-Item 'install.ps1', 'uninstall.ps1', 'LICENSE' $pkg
    Copy-Item 'packaging\*' $pkg
    Compress-Archive -Path (Join-Path $pkg '*') -DestinationPath (Join-Path $rel $zipName) -CompressionLevel Optimal
}

Publish (Join-Path $tmp 'fd') $false
Publish (Join-Path $tmp 'sc') $true
Package (Join-Path $tmp 'fd') "SmartStart-$version-win-x64.zip"
Package (Join-Path $tmp 'sc') "SmartStart-$version-win-x64-standalone.zip"
Remove-Item $tmp -Recurse -Force

Get-ChildItem $rel | ForEach-Object { '{0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
Get-FileHash (Join-Path $rel '*.zip') -Algorithm SHA256 | ForEach-Object { '{0}  {1}' -f $_.Hash.ToLower(), (Split-Path $_.Path -Leaf) }

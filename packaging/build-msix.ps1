# Packs the output of build.ps1 (dist\Claudius) into an MSIX for the Microsoft Store.
#   -Version 1.2.3      app version; the package gets 1.2.3.0 (the Store needs revision 0)
#   -Register           instead of packing, register the layout for a local test
#                       (needs Developer Mode; no signing required)
# The package is unsigned: the Store signs it after certification.
param(
    [Parameter(Mandatory)] [string]$Version,
    [string]$Source = "$PSScriptRoot\..\dist\Claudius",
    [string]$OutDir = "$PSScriptRoot\..\dist",
    [switch]$Register
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)') { throw "Version '$Version' is not like 1.2.3" }
$packageVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
if (-not (Test-Path "$Source\Claudius.exe")) { throw "$Source\Claudius.exe not found - run build.ps1 first." }

# Layout: the app as built, the manifest with the version filled in and the logos.
$layout = Join-Path $OutDir 'msix'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
Copy-Item $Source $layout -Recurse
Copy-Item "$PSScriptRoot\Assets" "$layout\Assets" -Recurse
(Get-Content "$PSScriptRoot\AppxManifest.xml" -Raw).Replace('{VERSION}', $packageVersion) |
    Set-Content "$layout\AppxManifest.xml" -Encoding utf8

if ($Register) {
    Add-AppxPackage -Register "$layout\AppxManifest.xml" -ForceApplicationShutdown
    Write-Host "`nRegistriert: Claudius - KI Usage Pet $packageVersion (aus $layout)"
    return
}

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" |
    Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
if (-not $makeappx) { throw 'makeappx.exe not found - install the Windows SDK.' }

$package = Join-Path $OutDir "Claudius-$($Matches[0]).msix"
& $makeappx.FullName pack /d $layout /p $package /o
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "`nFertig: $package"

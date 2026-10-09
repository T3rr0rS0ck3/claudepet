# Builds Claudius.exe and ClaudiusBridge.exe into dist\Claudius
#   -Version 1.2.3     version stamped into the binaries
#   -SelfContained     bundle the .NET runtime (used for releases, no runtime install needed)
param(
    [string]$Output = "$PSScriptRoot\dist\Claudius",
    [string]$Version = "0.0.0",
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$sc = if ($SelfContained) { 'true' } else { 'false' }
$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', $sc,
    '-p:PublishReadyToRun=true', '-p:DebugType=none', "-p:Version=$Version", '-o', $Output)

dotnet publish "$PSScriptRoot\src\Claudius\Claudius.csproj" @common
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish "$PSScriptRoot\src\Claudius.Bridge\Claudius.Bridge.csproj" @common
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nFertig: $Output\Claudius.exe"

# Builds ClaudePet.exe and ClaudePetBridge.exe into dist\ClaudePet
#   -Version 1.2.3     version stamped into the binaries
#   -SelfContained     bundle the .NET runtime (used for releases, no runtime install needed)
param(
    [string]$Output = "$PSScriptRoot\dist\ClaudePet",
    [string]$Version = "0.0.0",
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$sc = if ($SelfContained) { 'true' } else { 'false' }
$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', $sc,
    '-p:PublishReadyToRun=true', '-p:DebugType=none', "-p:Version=$Version", '-o', $Output)

dotnet publish "$PSScriptRoot\src\ClaudePet\ClaudePet.csproj" @common
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish "$PSScriptRoot\src\ClaudePet.Bridge\ClaudePet.Bridge.csproj" @common
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nFertig: $Output\ClaudePet.exe"

# Drift guard for GameStubs/: builds the stubs, then checks that every stub type and member exists in the installed
# game's assemblies with the same kind, signature, static-ness, base type and declaring type.
# Run after a game update, and after you add a member to GameStubs/. Needs the game, so it is not part of CI.
#
#   pwsh tools/check-stubs.ps1 [-GameDir "D:\SteamLibrary\steamapps\common\Calculate It"]
#
# Exit code: 0 no differences, 1 differences, 2 setup problem.
param(
    [string]$GameDir = 'D:\SteamLibrary\steamapps\common\Calculate It'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $GameDir 'CalculateIt_Data\Managed'
if (-not (Test-Path (Join-Path $managed 'Assembly-CSharp.dll'))) {
    Write-Error "Assembly-CSharp.dll not found in $managed. Pass -GameDir <game folder>."
    exit 2
}

dotnet build (Join-Path $root 'GameStubs') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit 2 }
dotnet build (Join-Path $root 'tools\StubCheck') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit 2 }

$stubDirs = (Join-Path $root 'GameStubs\bin\Release\net472'), (Join-Path $root 'GameStubs\UI\bin\Release\net472')
dotnet (Join-Path $root 'tools\StubCheck\bin\Release\net9.0\StubCheck.dll') --game $managed --stubs ($stubDirs -join ';')
exit $LASTEXITCODE

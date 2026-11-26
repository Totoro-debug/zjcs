param([Parameter(Mandatory=$true)][string]$Workspace)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskWorkspace = (Get-Item -LiteralPath $Workspace).FullName
if ((Split-Path -Parent $taskWorkspace) -ne (Join-Path $taskRoot 'output/validation') -or
    -not (Test-Path -LiteralPath (Join-Path $taskWorkspace '.zjcs-validation.json'))) {
    throw 'Use an owned validation workspace.'
}
Add-Type -IgnoreWarnings -WarningAction SilentlyContinue -Path @(
    (Join-Path $taskRoot 'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwisePackageBuild.cs'),
    (Join-Path $PSScriptRoot 'fixtures/battle_wwise_package_build_harness.cs'))
[BattleWwisePackageBuildHarness]::Run($taskWorkspace)

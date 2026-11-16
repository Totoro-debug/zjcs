$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'scripts/fixtures/mainline_monster_reward_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Offline/Progression/OfficialMainlineMonsterRewardCatalog.cs')
)
[MainlineMonsterRewardHarness]::Run()
Write-Output 'Mainline monster reward selection checks passed.'

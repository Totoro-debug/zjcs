$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + @(
    (Join-Path $PSScriptRoot 'fixtures/battle_live_graph_driver_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveGraphDriver.cs')))
[Zjcs.Presentation.Combat.BattleLiveGraphDriverHarness]::Run()

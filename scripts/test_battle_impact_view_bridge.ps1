$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + @(
    (Join-Path $PSScriptRoot 'fixtures/battle_impact_view_bridge_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleImpactViewBridge.cs')))
[Zjcs.Presentation.Combat.BattleImpactViewBridgeHarness]::Run()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repository 'unity/Assets/Game/Runtime/Presentation/Combat'
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'fixtures/battle_skill_graph_dispatch_harness.cs'),
    (Join-Path $runtime 'OfficialBattleSkillGraphCatalog.cs'),
    (Join-Path $runtime 'IBattleMovementActionView.cs'),
    (Join-Path $runtime 'OfficialBattleSkillGraphPlayback.cs'),
    (Join-Path $runtime 'OfficialBattleUnitEndPlayback.cs'),
    (Join-Path $runtime 'OfficialBattleUnitSpawnPlayback.cs'),
    (Join-Path $runtime 'OfficialBattleUnitLifePlayback.cs'),
    (Join-Path $repository 'unity/Assets/Game/Runtime/Official/Combat/FightOrdinaryMovementTiming.cs')
)
[BattleSkillGraphDispatchHarness]::Run()

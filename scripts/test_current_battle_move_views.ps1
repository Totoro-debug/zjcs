$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskRuntime=Join-Path $taskRoot 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
Add-Type -CompilerOptions '/nowarn:0649' -Path @(
 (Join-Path $PSScriptRoot 'fixtures/current_battle_move_views_harness.cs'),
 (Join-Path $taskRuntime 'IFightActionSkillMoveView.cs'),
 (Join-Path $taskRuntime 'ECFightActionSkillMoveView.cs'),
 (Join-Path $taskRuntime 'FightActionSkillMoveComponent.cs'),
 (Join-Path $taskRuntime 'FightActionSkillMoveComponentInfo.cs'))
[CurrentBattleMoveViewsHarness]::Run()

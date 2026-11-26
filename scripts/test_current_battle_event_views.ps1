$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOfficial=Join-Path $taskRoot 'unity/Assets/Game/Runtime/Official'
Add-Type -CompilerOptions '/nowarn:0649' -Path @(
    (Join-Path $PSScriptRoot 'fixtures/current_battle_event_views_harness.cs'),
    (Join-Path $taskOfficial 'BattleSkillPlayables/FightViewEvents.cs'),
    (Join-Path $taskOfficial 'BattleSkillPlayables/PlayPlayableWithSourceAndTargetHandler.cs'),
    (Join-Path $taskOfficial 'BattleStatusPlayables/ECStatusInvokedEventSourceView.cs'),
    (Join-Path $taskOfficial 'BattleStatusPlayables/StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter.cs')
)
[CurrentBattleEventViewsHarness]::Run()

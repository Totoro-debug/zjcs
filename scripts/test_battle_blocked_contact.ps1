$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/IFightActionSkillMoveView.cs'),
    (Join-Path $PSScriptRoot 'fixtures/battle_blocked_contact_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/BattleBlockedTrackContact.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMoveActionPlayback.cs')
)
[CollisionContactHarness]::Run()

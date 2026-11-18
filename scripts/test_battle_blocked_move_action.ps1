$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'fixtures/battle_blocked_move_action_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMoveActionPlayback.cs')
)
[BlockedActionHarness]::Run()

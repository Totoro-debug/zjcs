$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'fixtures/battle_blocked_live_session_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveImpactSession.cs')
)
[Zjcs.Presentation.Combat.BlockedLiveSessionHarness]::Run()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'fixtures/battle_hit_arrival_origin_harness.cs'),
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHitReceiptPlayback.cs'))
[Zjcs.Presentation.Combat.BattleHitArrivalOriginHarness]::Run()

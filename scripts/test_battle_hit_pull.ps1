. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$sources = @(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_hit_pull_harness.cs')
Add-Type -Path $sources
[BattleHitPullHarness]::Run()
$root = Split-Path -Parent $PSScriptRoot
$catalog = Get-Content -Raw -LiteralPath (Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json') | ConvertFrom-Json -AsHashtable
$comparison = Get-Content -Raw -LiteralPath (Join-Path $root 'assets/reference/battle-rules/active-entity-configuration-source-manifest.json') | ConvertFrom-Json -AsHashtable
if ($comparison.manifest -ne '884_156692') { throw 'Unexpected selected official combat source.' }
$officialRows = @{}
foreach ($row in $comparison.rows) { $officialRows[[int]$row.entity_id] = $row }
$sourceIds = @(12877,13127,57594,57713,58642,58927,59274,59494,59634,61564,61647,61994,65014,16452,16642,10702,10972,11832)
$targetIds = @(59972,15543,10583,11342,11352)
$awayIds = @(59523,63243,63863,64833,17823)
$facingIds = @(13214,57522,59072,17612)
$targetDistanceIds = @(12322,13802,60043,64362,15984,16373,18324,19364,11122)
$shieldOwnerDistanceIds = @(10872)
$sideIds = @(56233,56323,56524,56834,57623,58063,59153,59733,59753,62403,62443,62448,63573,63883,64813,17143,19623,11733)
foreach ($id in ($sourceIds + $targetIds + $awayIds + $facingIds + $targetDistanceIds + $shieldOwnerDistanceIds + $sideIds)) {
    $raw = $catalog.entities[[string]$id].FightDamageComponent.MoveCfg
    if ($null -eq $raw) { throw "Missing official damage movement $id" }
    $component = $officialRows[$id].changed.FightDamageComponent
    if ($null -eq $component -or
        (ConvertTo-Json -InputObject $component.installed.MoveCfg -Depth 20 -Compress) -ne
        (ConvertTo-Json -InputObject $component.selected.MoveCfg -Depth 20 -Compress) -or
        (ConvertTo-Json -InputObject $component.installed.MoveCfg -Depth 20 -Compress) -ne
        (ConvertTo-Json -InputObject $raw -Depth 20 -Compress)) {
        throw "Installed movement differs from the selected official source comparison: $id"
    }
    $move = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
    $move.MoveBehaviourId = $raw.MoveBehaviourId; $move.Distance = $raw.Distance
    $move.Direction = $raw.SkillMoveDirection; $move.TowardsTargetPos = $raw.TowardsTargetPos
    $move.OffsetX = $raw.Offset.X; $move.OffsetY = $raw.Offset.Y
    $move.CanExpandOffset = $raw.CanExpandOffset; $move.CancelDiagonally = $raw.CancelDiagonally
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]@{ DamageId = $id; HitMove = $move }
    $matched = $null -ne [Zjcs.Combat.Official.FightHitMovementRules]::For($hit)
    if ($matched -ne ($id -in ($sourceIds + $targetIds))) { throw "Unexpected original movement mapping $id" }
    $x = 0; $y = 0
    $facingMatched = [Zjcs.Combat.Official.FightHitMovementRules]::TryGetFacingPush($hit, 0, [ref]$x, [ref]$y)
    if ($facingMatched -ne ($id -in $facingIds)) { throw "Unexpected original facing mapping $id" }
    if ([Zjcs.Combat.Official.FightHitMovementRules]::UsesSkillTarget($hit) -ne ($id -in $targetDistanceIds)) {
        throw "Unexpected target-relative distance mapping $id"
    }
    if ([Zjcs.Combat.Official.FightHitMovementRules]::UsesConfirmedSideMove($hit) -ne ($id -in $sideIds)) {
        throw "Unexpected side-move mapping $id"
    }
}
Write-Output 'All 60 selected hit-movement configurations matched the installed and selected official catalogs.'

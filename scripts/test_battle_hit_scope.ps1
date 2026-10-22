$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Scope($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Unit($id, $side, $x, $y, $hp) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = 100 - $id
    return $unit
}

function New-ScopeSkill($entityId, $rangeX, $scope, $anchor, $targetMode, $randomCount) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = $entityId; $skill.ItemId = $entityId + 1000; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new($rangeX, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = $entityId + 1; $hit.DamageId = $entityId + 2; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 5; $hit.TargetMode = $targetMode
    $hit.RandomCount = $randomCount; $hit.Anchor = $anchor
    for ($index = 0; $index + 1 -lt $scope.Count; $index += 2) {
        $hit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new($scope[$index], $scope[$index + 1]))
    }
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-ScopeBattle($skill, $units) {
    $player = $units[0]
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.RandomSeed = 7
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 12; $checkpoint.Grid.Height = 12; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add($player)
    $waveIds = [System.Collections.Generic.List[int]]::new()
    foreach ($unit in $units) {
        if ($unit.Id -ne $player.Id) { $checkpoint.Units.Add($unit); $waveIds.Add($unit.Id) }
    }
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0
    foreach ($id in $waveIds) { $wave.UnitIds.Add($id) }
    $checkpoint.Waves.Add($wave)
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
        $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
    return $executor.Step(0, 1)
}

# AoE around the primary target: Scope (0,0) and (0,1) hit the primary and its neighbour.
$player = New-Unit 1 1 0 0 1000
$primary = New-Unit 2 2 2 0 5000
$neighbour = New-Unit 3 2 2 1 5000
$outside = New-Unit 4 2 5 0 5000
$result = Invoke-ScopeBattle (New-ScopeSkill 4001 2 @(0, 0, 0, 1) 0 1 1) @($player, $primary, $neighbour, $outside)
Assert-Scope ($result.Event.DamageEvents.Count -eq 2) 'Scope did not expand the hit to the nearby unit'
$ids = @($result.Event.DamageEvents | ForEach-Object { $_.TargetId })
Assert-Scope ($ids -contains 2 -and $ids -contains 3 -and -not ($ids -contains 4)) 'Scope hit the wrong units'
Assert-Scope ($outside.Hp -eq 5000 -and $neighbour.Hp -eq 4995) 'Scope damage was applied to the wrong units'

# OnSourceCastPos anchor: the area is centred on the caster, not the primary target.
$sourcePlayer = New-Unit 1 1 0 0 1000
$sourcePrimary = New-Unit 2 2 3 0 5000
$sourceNear = New-Unit 3 2 1 0 5000
$sourceResult = Invoke-ScopeBattle (New-ScopeSkill 4002 3 @(0, 0, 1, 0) 1 1 1) @($sourcePlayer, $sourcePrimary, $sourceNear)
$sourceIds = @($sourceResult.Event.DamageEvents | ForEach-Object { $_.TargetId })
Assert-Scope ($sourceResult.Event.DamageEvents.Count -eq 1 -and $sourceIds[0] -eq 3) 'Source-anchored scope did not centre on the caster'

# A scope without any unit produces no damage.
$emptyPlayer = New-Unit 1 1 0 0 1000
$emptyTarget = New-Unit 2 2 2 0 5000
$emptyResult = Invoke-ScopeBattle (New-ScopeSkill 4003 2 @(5, 5) 0 1 1) @($emptyPlayer, $emptyTarget)
Assert-Scope ($emptyResult.Event.DamageEvents.Count -eq 0 -and $emptyTarget.Hp -eq 5000) 'Empty scope still applied damage'

# Random hits choose their count inside the scope only.
$randomPlayer = New-Unit 1 1 0 0 1000
$randomPrimary = New-Unit 2 2 2 0 5000
$randomNeighbour = New-Unit 3 2 2 1 5000
$randomResult = Invoke-ScopeBattle (New-ScopeSkill 4004 2 @(0, 0, 0, 1) 0 2 1) @($randomPlayer, $randomPrimary, $randomNeighbour)
Assert-Scope ($randomResult.Event.DamageEvents.Count -eq 1) 'Random scope hit count mismatch'
$randomTargetId = $randomResult.Event.DamageEvents[0].TargetId
Assert-Scope ($randomTargetId -eq 2 -or $randomTargetId -eq 3) 'Random scope selected a unit outside the area'


# Point-target skills with no enemy in Range may detonate their Scope around the caster.
$pointPlayer = New-Unit 1 1 0 0 1000
$pointNear = New-Unit 2 2 0 1 5000
$pointSkill = New-ScopeSkill 4005 0 @(0, 0, 0, 1) 0 1 1
$pointSkill.SkillTargetType = 0
$pointResult = Invoke-ScopeBattle $pointSkill @($pointPlayer, $pointNear)
Assert-Scope ($pointResult.Event.DamageEvents.Count -eq 1 -and $pointNear.Hp -eq 4995) 'Caster-centred point area did not hit the nearby enemy'
$pointCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$pointCheckpoint.Units.Add($pointPlayer); $pointCheckpoint.Units.Add($pointNear)
$ready = [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($pointPlayer,
    [Zjcs.Combat.Official.FightBattleUnitState[]]@($pointPlayer, $pointNear), 1)
Assert-Scope ($null -ne $ready -and $ready.EntityId -eq 4005) 'Point area skill was not considered ready with a caster-area target'

# The same point skill is not released when nothing is inside its area.
$emptyPointPlayer = New-Unit 1 1 0 0 1000
$emptyPointEnemy = New-Unit 2 2 6 6 5000
$emptyPointSkill = New-ScopeSkill 4006 0 @(0, 0, 0, 1) 0 1 1
$emptyPointSkill.SkillTargetType = 0
$emptyPointResult = Invoke-ScopeBattle $emptyPointSkill @($emptyPointPlayer, $emptyPointEnemy)
Assert-Scope ($emptyPointResult.Event.DamageEvents.Count -eq 0 -and $emptyPointEnemy.Hp -eq 5000) 'Point area skill was released without any target in range'


Write-Output 'Battle hit scope checks passed: target-anchored AoE, source-anchored AoE, empty scope, random hits inside scope and caster-centred point area release.'

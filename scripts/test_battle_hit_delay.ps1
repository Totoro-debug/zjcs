$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Delay($condition, $message) {
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

function New-DelaySkill($entityId, $delay, $factor, $anchor, $scope, $targetMode) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = $entityId; $skill.ItemId = $entityId + 1000; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(3, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = $entityId + 1; $hit.DamageId = $entityId + 2; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1
    $hit.Delay = $delay; $hit.DelayDistFactor = $factor; $hit.Anchor = $anchor; $hit.TargetMode = $targetMode
    for ($index = 0; $index + 1 -lt $scope.Count; $index += 2) {
        $hit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new($scope[$index], $scope[$index + 1]))
    }
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-DelayBattle($skill, $units) {
    $player = $units[0]
    $player.Skills.Clear()
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.RandomSeed = 11
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 12; $checkpoint.Grid.Height = 12; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add($player)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0
    foreach ($unit in $units) {
        if ($unit.Id -eq $player.Id) { continue }
        $checkpoint.Units.Add($unit); $wave.UnitIds.Add($unit.Id)
    }
    $checkpoint.Waves.Add($wave)
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
        $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
    return $executor.Step(0, 1)
}

# FinalDelay = Delay + distance * DelayDistFactor for target-anchored hits.
$player = New-Unit 1 1 0 0 1000
$enemy = New-Unit 2 2 3 0 5000
$result = Invoke-DelayBattle (New-DelaySkill 5001 0.1 0.2 0 @() 0) @($player, $enemy)
$delay = $result.Event.DamageEvents[0].Delay
Assert-Delay ([Math]::Abs($delay - 0.7) -lt 0.0001) "Distance delay mismatch: $delay"

# Source-anchored hits measure from the caster position, so the configured delay stays.
$sourceResult = Invoke-DelayBattle (New-DelaySkill 5002 0.1 0.2 1 @() 0) @($player, $enemy)
$sourceDelay = $sourceResult.Event.DamageEvents[0].Delay
Assert-Delay ([Math]::Abs($sourceDelay - 0.1) -lt 0.0001) "Source-anchored delay mismatch: $sourceDelay"

# Area hits keep one delay per target according to its own distance.
$areaPlayer = New-Unit 1 1 0 0 1000
$areaPrimary = New-Unit 2 2 3 0 5000
$areaNeighbour = New-Unit 3 2 4 0 5000
$areaResult = Invoke-DelayBattle (New-DelaySkill 5003 0.1 0.2 0 @(0, 0, 1, 0) 1) @($areaPlayer, $areaPrimary, $areaNeighbour)
$areaDelays = @($areaResult.Event.DamageEvents | ForEach-Object { [Math]::Round($_.Delay, 4) } | Sort-Object)
Assert-Delay ($areaResult.Event.DamageEvents.Count -eq 2 -and $areaDelays[0] -eq 0.7 -and $areaDelays[1] -eq 0.9) "Area distance delays mismatch: $($areaDelays -join ',')"
Assert-Delay ($areaResult.Event.DamageEvents[0].Delay -le $areaResult.Event.DamageEvents[1].Delay) 'Area damage events are not ordered by their distance delay'

Write-Output 'Battle hit delay checks passed: distance-scaled delay, source-anchored delay and per-target area delays.'

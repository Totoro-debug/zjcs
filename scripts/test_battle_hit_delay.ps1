. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
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
    $script:lastDelayCheckpoint = $checkpoint
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

# Original MyFightHitBase waits for the flight to its hit grid once, then plays
# the ordered scope. Neighbouring targets share that flight, not their own distances.
$areaPlayer = New-Unit 1 1 0 0 1000
$areaPrimary = New-Unit 2 2 3 0 5000
$areaNeighbour = New-Unit 3 2 4 0 5000
$areaResult = Invoke-DelayBattle (New-DelaySkill 5003 0.1 0.2 0 @(0, 0, 1, 0) 1) @($areaPlayer, $areaPrimary, $areaNeighbour)
$areaDelays = @($areaResult.Event.DamageEvents | ForEach-Object { [Math]::Round($_.Delay, 4) } | Sort-Object)
Assert-Delay ($areaResult.Event.DamageEvents.Count -eq 2 -and $areaDelays[0] -eq 0.7 -and $areaDelays[1] -eq 0.7) "Area centre delays mismatch: $($areaDelays -join ',')"
Assert-Delay ($areaResult.Event.DamageEvents[0].Delay -le $areaResult.Event.DamageEvents[1].Delay) 'Area damage events are not ordered by their distance delay'

$areaSkill = New-DelaySkill 5004 0.1 0.2 0 @(0, 0, 1, 0) 1
$areaSkill.Hits[0].ScopeEntries.Add([Zjcs.Combat.Official.FightBattleHitScopeEntry]@{Position=[Zjcs.Combat.Official.FightGridPoint]::new(0,0);Delay=0})
$areaSkill.Hits[0].ScopeEntries.Add([Zjcs.Combat.Official.FightBattleHitScopeEntry]@{Position=[Zjcs.Combat.Official.FightGridPoint]::new(1,0);Delay=0.25})
$areaResult = Invoke-DelayBattle $areaSkill @((New-Unit 1 1 0 0 1000),(New-Unit 2 2 3 0 5000),(New-Unit 3 2 4 0 5000))
$areaDelays = @($areaResult.Event.DamageEvents | ForEach-Object { [Math]::Round($_.Delay, 4) } | Sort-Object)
Assert-Delay ($areaDelays.Count -eq 2 -and $areaDelays[0] -eq 0.7 -and $areaDelays[1] -eq 0.95) 'Scope delay was not added after the common hit flight'

# Two random body cells of the same entity are independent hits, including when
# the child has no Scope. Keep each actual draw, its distance and its centre index.
$body = New-Unit 2 2 3 0 5000
$body.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0))
$body.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$randomSkill = New-DelaySkill 5005 0.1 0.2 0 @() 1
$randomHit = $randomSkill.Hits[0]
$randomHit.RandomTargetGroupId=1;$randomHit.RandomCount=2;$randomHit.RandomTargetNotSameGrid=$true
$randomHit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(3,0))
$randomHit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(4,0))
$randomHit.RandomTargetAnchor=1
$bodyResult = Invoke-DelayBattle $randomSkill @((New-Unit 1 1 0 0 1000),$body)
$bodyDelays = @($bodyResult.Event.DamageEvents | ForEach-Object { [Math]::Round($_.Delay, 4) } | Sort-Object)
Assert-Delay ($bodyDelays.Count -eq 2 -and $bodyDelays[0] -eq 0.7 -and $bodyDelays[1] -eq 0.9 -and $body.Hp -eq 4998) 'Random body draws were merged or timed from the entity centre'
$selection=$bodyResult.Event.SkillCasts[0].HitSelections[0]
Assert-Delay ($selection.Targets.Count -eq 2 -and $selection.Targets[0].EffectCenterIndex -ne $selection.Targets[1].EffectCenterIndex -and $selection.EffectDelays.Count -eq 2) 'Selection receipt lost its effect-centre association'
Assert-Delay (@($bodyResult.Event.DamageEvents | Where-Object {-not $_.HasHitGridPosition}).Count -eq 0) 'Random body hits lost their selected damage grids'
$restoredBody = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($script:lastDelayCheckpoint))
$restoredSelection = $restoredBody.Receipts[0].Event.SkillCasts[0].HitSelections[0]
Assert-Delay ($restoredSelection.EffectDelays.Count -eq 2 -and $restoredSelection.Targets[0].EffectCenterIndex -ne $restoredSelection.Targets[1].EffectCenterIndex -and [Math]::Abs($restoredSelection.EffectDelays[0]-$selection.EffectDelays[0]) -lt 0.00001 -and [Math]::Abs($restoredSelection.EffectDelays[1]-$selection.EffectDelays[1]) -lt 0.00001) 'Checkpoint lost actual flight delays or centre associations'

# Ported original flight offsets include Z, source mirroring and centre rotation.
$cast=[Zjcs.Combat.Official.FightBattleSkillCastPresentation]::new()
$offsetHit=[Zjcs.Combat.Official.FightBattleHitDefinition]@{Delay=0.1;DelayDistFactor=0.2;Trajectory=[Zjcs.Combat.Official.FightBattleHitTrajectory]@{OffsetX=3;OffsetY=4}}
$offsetDelay=[Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($offsetHit,$cast,[Zjcs.Combat.Official.FightGridPoint]::new(0,0))
Assert-Delay ([Math]::Abs($offsetDelay-1.1) -lt 0.00001) 'Source-anchored hit incorrectly discarded its original flight offset'
$offsetHit.Trajectory.OffsetY=0;$offsetHit.Trajectory.OffsetZ=4
Assert-Delay ([Math]::Abs([Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($offsetHit,$cast,[Zjcs.Combat.Official.FightGridPoint]::new(0,0))-1.1) -lt 0.00001) 'Flight distance discarded the original Z offset'
$offsetHit.Trajectory.OffsetX=2;$offsetHit.Trajectory.OffsetZ=0;$cast.FlipX=$true
Assert-Delay ([Math]::Abs([Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($offsetHit,$cast,[Zjcs.Combat.Official.FightGridPoint]::new(3,0))-1.1) -lt 0.00001) 'Flight distance discarded source mirroring'
$cast.FlipX=$false;$offsetHit.Trajectory.IsUseCenterRotation=$true;$offsetHit.Trajectory.RotateCenterOffsetX=1
$rotationExpected=0.1+([Math]::Sqrt(37)-1)*0.2
Assert-Delay ([Math]::Abs([Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($offsetHit,$cast,[Zjcs.Combat.Official.FightGridPoint]::new(0,6))-$rotationExpected) -lt 0.00001) 'Original centre-rotation start position changed'

Write-Output 'Battle hit delay checks passed: actual hit centres, scope delays, repeated body draws, original trajectory offsets, mirror and centre rotation.'

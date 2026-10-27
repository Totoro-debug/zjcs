. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Persist($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Unit($id, $side, $x, $y, $hp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
    if ($side -eq 2) { $unit.MaxHpPlies = 4 }
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = $speed
    return $unit
}

$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())

$player = New-Unit 1 1 0 0 1000 100
$player.MoveMask = 4
$player.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 1))
$player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1; Cooldown = 2; ResetCooldownAtStart = $true })
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 900; $skill.ItemId = 901; $skill.TargetType = 1
$skill.ActionDuration = 1.1; $skill.Duration = 0.7
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = 902; $hit.DamageId = 903; $hit.DamageType = 8
$hit.HasCustomDamage = $true; $hit.CustomDamage = 25; $hit.TargetMode = 0; $hit.Delay = 0.4
$skill.Hits.Add($hit)
$player.Skills.Add($skill)
$skill.ElementType = 101
$hit.StatusListRandomOne = $true
$hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 24078; TargetType = 2; Chance = 0.5; LessThanHpPer = 0.5 })
$hit.DisperseChance = 0.25; $hit.DisperseCount = 1; $hit.DisperseRandom = $true
$hit.DisperseStatusTypes.Add(2)
$hit.HitMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
$hit.HitMove.Distance = 1; $hit.HitMove.Direction = 1; $hit.HitMove.TowardsTargetPos = $true
$hit.HemophagiaProperty = 213
$status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$status.InstanceId = 1; $status.ClassId = 24078; $status.DurationRound = 2; $status.StackCount = 2
$status.RootSkillIds = [int[]]@(900, 901)
$status.HpTriggerPer = 0.25; $status.HpTriggerMaxCount = 2; $status.HpTriggerAtStart = $true
$status.HpTriggerStatuses.Add(24078); $status.HpTriggerSkills.Add(50010)
$status.HasHpAccDecrease = $true; $status.HpAccUnitHp = -1; $status.HpAccUnitHpPercentage = 0.15
$status.HpAccResetAtRoundEnd = $false; $status.HpAccMaxCount = 99; $status.HpAccUpdateHpPerOnCure = $true
$status.HpAccDamageAccumulated = 125; $status.HpAccTriggeredCount = 3
$status.HpAccTriggerStatuses.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{
    EntityId = 24240; BasePercent = 1; Target = 2
})
$status.HpAccTriggerSkills.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{
    EntityId = 58170; BasePercent = 1; Source = 2; Target = 2; TargetPos = 1
})
$player.Statuses.Add($status)
$enemy = New-Unit 2 2 1 0 500 50
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 22001; $checkpoint.StageType = 3; $checkpoint.IsPvp = $true
$checkpoint.Mode = 1; $checkpoint.MaxRound = 12; $checkpoint.RandomSeed = 48271
$checkpoint.IsPlayerRoundLimit = $true; $checkpoint.PlayerRoundLimit = 8
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 6; $checkpoint.Grid.Height = 7; $checkpoint.Grid.OriginX = 1.5; $checkpoint.Grid.OriginY = -2.25
$checkpoint.Grid.Cells.Add([Zjcs.Combat.Official.FightBattleGridCell]@{ X = 1; Y = 2; GroundType = 2; Height = 3 })
$checkpoint.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(5, 6))
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
$checkpoint.StageStatus = [Zjcs.Combat.Official.FightBattleStageStatusConfig]::new()
$checkpoint.StageStatus.ClassId = 1
$checkpoint.StageStatus.Status.Add(26004)
$checkpoint.StageStatus.GlobalRoundStatus.Add(26005)
$checkpoint.StageStatus.GlobalRoundStatusDelay = 3
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$result = $executor.Step(0, 1)
Assert-Persist ($result.Checkpoint.Counter -eq 1 -and $result.Checkpoint.Receipts.Count -eq 1) 'Battle did not record the step before persistence'

$json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint)
Assert-Persist ($json.Length -gt 100 -and $json.StartsWith('{"version":1,"checkpoint":')) 'Checkpoint payload header mismatch'
$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
Assert-Persist ($loaded.StageId -eq 22001 -and $loaded.Mode -eq 1 -and $loaded.MaxRound -eq 12 -and $loaded.RandomSeed -eq $checkpoint.RandomSeed) 'Checkpoint header did not survive the round trip'
Assert-Persist ($loaded.StageType -eq 3 -and $loaded.IsPvp) 'Checkpoint formula context did not survive the round trip'
Assert-Persist ($loaded.IsPlayerRoundLimit -and $loaded.PlayerRoundLimit -eq 8 -and $loaded.Counter -eq 1 -and $loaded.Receipts.Count -eq 1) 'Checkpoint limits or receipts did not survive the round trip'
Assert-Persist ($loaded.Units.Count -eq 2 -and $loaded.Units[0].Hp -eq 1000 -and $loaded.Units[0].MoveMask -eq 4) 'Checkpoint units did not survive the round trip'
Assert-Persist ($loaded.Units[1].MaxHpPlies -eq 4) 'Checkpoint boss HP plies did not survive the round trip'
Assert-Persist ($loaded.Units[0].BodyOffsets.Count -eq 2 -and $loaded.Units[0].BodyOffsets[1].Y -eq 1) 'Checkpoint body offsets did not survive the round trip'
Assert-Persist ($loaded.Units[0].Skills.Count -eq 1 -and $loaded.Units[0].Skills[0].EntityId -eq 900 -and [Math]::Abs($loaded.Units[0].Skills[0].ActionDuration - 1.1) -lt 0.0001) 'Checkpoint skills did not survive the round trip'
Assert-Persist ($loaded.Units[0].Skills[0].Hits.Count -eq 1 -and [Math]::Abs($loaded.Units[0].Skills[0].Hits[0].Delay - 0.4) -lt 0.0001) 'Checkpoint hit delays did not survive the round trip'
Assert-Persist ($loaded.Units[0].Statuses.Count -eq 1 -and $loaded.Units[0].Statuses[0].ClassId -eq 24078 -and $loaded.Units[0].Statuses[0].RootSkillIds[1] -eq 901) 'Checkpoint statuses did not survive the round trip'
Assert-Persist ($loaded.Grid.Width -eq 6 -and $loaded.Grid.Height -eq 7 -and [Math]::Abs($loaded.Grid.OriginY + 2.25) -lt 0.0001) 'Checkpoint grid did not survive the round trip'
Assert-Persist ($loaded.Grid.Cells.Count -eq 1 -and $loaded.Grid.Cells[0].Height -eq 3 -and $loaded.Grid.BlockedPositions[0].X -eq 5) 'Checkpoint grid cells did not survive the round trip'
Assert-Persist ($loaded.Timeline.Entries.Count -eq $checkpoint.Timeline.Entries.Count -and $loaded.Timeline.Round -eq $checkpoint.Timeline.Round) 'Checkpoint timeline did not survive the round trip'
Assert-Persist ($loaded.Statistics.Units.Count -eq $checkpoint.Statistics.Units.Count -and $loaded.Statistics.Skill(1, 900, 901).Damage -eq 25) 'Checkpoint statistics did not survive the round trip'
Assert-Persist ($loaded.Receipts[0].Event.DamageEvents.Count -eq 1 -and $loaded.Receipts[0].Event.DamageEvents[0].TargetId -eq 2) 'Checkpoint receipt damage events did not survive the round trip'
Assert-Persist ($loaded.Receipts[0].Event.DamageEvents[0].ElementType -eq 101) 'Checkpoint damage element did not survive the round trip'
Assert-Persist ($loaded.Units[0].Statuses[0].HpTriggerPer -eq 0.25 -and $loaded.Units[0].Statuses[0].HpTriggerMaxCount -eq 2 -and $loaded.Units[0].Statuses[0].HpTriggerAtStart) 'Checkpoint HP trigger fields did not survive the round trip'
Assert-Persist ($loaded.Units[0].Statuses[0].HpTriggerStatuses[0] -eq 24078 -and $loaded.Units[0].Statuses[0].HpTriggerSkills[0] -eq 50010) 'Checkpoint HP trigger lists did not survive the round trip'
$loadedAcc = $loaded.Units[0].Statuses[0]
Assert-Persist ($loadedAcc.HasHpAccDecrease -and $loadedAcc.HpAccUnitHp -eq -1 -and [Math]::Abs($loadedAcc.HpAccUnitHpPercentage - 0.15) -lt 0.00001 -and $loadedAcc.HpAccMaxCount -eq 99 -and $loadedAcc.HpAccDamageAccumulated -eq 125 -and $loadedAcc.HpAccTriggeredCount -eq 3) 'Checkpoint HP accumulated damage state did not survive the round trip'
Assert-Persist ($loadedAcc.HpAccTriggerStatuses.Count -eq 1 -and $loadedAcc.HpAccTriggerStatuses[0].EntityId -eq 24240 -and $loadedAcc.HpAccTriggerSkills.Count -eq 1 -and $loadedAcc.HpAccTriggerSkills[0].EntityId -eq 58170 -and $loadedAcc.HpAccTriggerSkills[0].Source -eq 2 -and $loadedAcc.HpAccTriggerSkills[0].Target -eq 2 -and $loadedAcc.HpAccTriggerSkills[0].TargetPos -eq 1) 'Checkpoint HP accumulated trigger configuration did not survive the round trip'
$loadedHit = $loaded.Units[0].Skills[0].Hits[0]
Assert-Persist ($loadedHit.StatusListRandomOne -and $loadedHit.AppliedStatuses.Count -eq 1 -and $loadedHit.AppliedStatuses[0].Chance -eq 0.5) 'Checkpoint hit status list did not survive the round trip'
Assert-Persist ($loadedHit.DisperseStatusTypes[0] -eq 2 -and $loadedHit.DisperseCount -eq 1 -and $loadedHit.DisperseRandom -and $loadedHit.DisperseChance -eq 0.25) 'Checkpoint hit dispel did not survive the round trip'
Assert-Persist ($null -ne $loadedHit.HitMove -and $loadedHit.HitMove.Distance -eq 1 -and $loadedHit.HitMove.TowardsTargetPos) 'Checkpoint hit move did not survive the round trip'
Assert-Persist ($loadedHit.HemophagiaProperty -eq 213) 'Checkpoint hit lifesteal property did not survive the round trip'
Assert-Persist ($null -ne $loaded.StageStatus -and $loaded.StageStatus.ClassId -eq 1 -and $loaded.StageStatus.Status[0] -eq 26004 -and $loaded.StageStatus.GlobalRoundStatus[0] -eq 26005 -and [Math]::Abs($loaded.StageStatus.GlobalRoundStatusDelay - 3) -lt 0.0001) 'Checkpoint stage status did not survive the round trip'

$resaved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($loaded)
Assert-Persist ($resaved -eq $json) 'Checkpoint serialization was not stable across a round trip'

$restored = [Zjcs.Combat.Official.FightBattleExecutor]::Restore(
    $loaded, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$replay = $restored.Step(0, 1)
Assert-Persist ($replay.Replayed -and $replay.Event.ActorId -eq 1 -and $loaded.Units[0].Hp -eq 1000) 'Restored checkpoint replay did not reproduce the recorded step'
$next = $restored.Step(0, 2)
Assert-Persist ($next.Checkpoint.Counter -eq 2 -and $next.Event.DamageEvents.Count -ge 0) 'Restored checkpoint did not continue the battle'

$badVersion = $false
try { [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load('{"version":2,"checkpoint":{}}') } catch { $badVersion = $true }
Assert-Persist $badVersion 'Checkpoint serializer accepted an unsupported payload version'
$emptyRejected = $false
try { [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load('') } catch { $emptyRejected = $true }
Assert-Persist $emptyRejected 'Checkpoint serializer accepted an empty payload'

Write-Output 'Battle checkpoint persistence checks passed: versioned payload, full field round trip, stable re-save, restore replay and continuation, and invalid payload rejection.'

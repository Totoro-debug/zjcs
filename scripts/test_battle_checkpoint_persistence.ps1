$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Persist($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Unit($id, $side, $x, $y, $hp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
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
$status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$status.InstanceId = 1; $status.ClassId = 24078; $status.DurationRound = 2; $status.StackCount = 2
$status.RootSkillIds = [int[]]@(900, 901)
$player.Statuses.Add($status)
$enemy = New-Unit 2 2 1 0 500 50
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 22001; $checkpoint.Mode = 1; $checkpoint.MaxRound = 12; $checkpoint.RandomSeed = 48271
$checkpoint.IsPlayerRoundLimit = $true; $checkpoint.PlayerRoundLimit = 8
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 6; $checkpoint.Grid.Height = 7; $checkpoint.Grid.OriginX = 1.5; $checkpoint.Grid.OriginY = -2.25
$checkpoint.Grid.Cells.Add([Zjcs.Combat.Official.FightBattleGridCell]@{ X = 1; Y = 2; GroundType = 2; Height = 3 })
$checkpoint.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(5, 6))
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$result = $executor.Step(0, 1)
Assert-Persist ($result.Checkpoint.Counter -eq 1 -and $result.Checkpoint.Receipts.Count -eq 1) 'Battle did not record the step before persistence'

$json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint)
Assert-Persist ($json.Length -gt 100 -and $json.StartsWith('{"version":1,"checkpoint":')) 'Checkpoint payload header mismatch'
$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
Assert-Persist ($loaded.StageId -eq 22001 -and $loaded.Mode -eq 1 -and $loaded.MaxRound -eq 12 -and $loaded.RandomSeed -eq 48271) 'Checkpoint header did not survive the round trip'
Assert-Persist ($loaded.IsPlayerRoundLimit -and $loaded.PlayerRoundLimit -eq 8 -and $loaded.Counter -eq 1 -and $loaded.Receipts.Count -eq 1) 'Checkpoint limits or receipts did not survive the round trip'
Assert-Persist ($loaded.Units.Count -eq 2 -and $loaded.Units[0].Hp -eq 1000 -and $loaded.Units[0].MoveMask -eq 4) 'Checkpoint units did not survive the round trip'
Assert-Persist ($loaded.Units[0].BodyOffsets.Count -eq 2 -and $loaded.Units[0].BodyOffsets[1].Y -eq 1) 'Checkpoint body offsets did not survive the round trip'
Assert-Persist ($loaded.Units[0].Skills.Count -eq 1 -and $loaded.Units[0].Skills[0].EntityId -eq 900 -and [Math]::Abs($loaded.Units[0].Skills[0].ActionDuration - 1.1) -lt 0.0001) 'Checkpoint skills did not survive the round trip'
Assert-Persist ($loaded.Units[0].Skills[0].Hits.Count -eq 1 -and [Math]::Abs($loaded.Units[0].Skills[0].Hits[0].Delay - 0.4) -lt 0.0001) 'Checkpoint hit delays did not survive the round trip'
Assert-Persist ($loaded.Units[0].Statuses.Count -eq 1 -and $loaded.Units[0].Statuses[0].ClassId -eq 24078 -and $loaded.Units[0].Statuses[0].RootSkillIds[1] -eq 901) 'Checkpoint statuses did not survive the round trip'
Assert-Persist ($loaded.Grid.Width -eq 6 -and $loaded.Grid.Height -eq 7 -and [Math]::Abs($loaded.Grid.OriginY + 2.25) -lt 0.0001) 'Checkpoint grid did not survive the round trip'
Assert-Persist ($loaded.Grid.Cells.Count -eq 1 -and $loaded.Grid.Cells[0].Height -eq 3 -and $loaded.Grid.BlockedPositions[0].X -eq 5) 'Checkpoint grid cells did not survive the round trip'
Assert-Persist ($loaded.Timeline.Entries.Count -eq $checkpoint.Timeline.Entries.Count -and $loaded.Timeline.Round -eq $checkpoint.Timeline.Round) 'Checkpoint timeline did not survive the round trip'
Assert-Persist ($loaded.Statistics.Units.Count -eq $checkpoint.Statistics.Units.Count -and $loaded.Statistics.Skill(1, 900, 901).Damage -eq 25) 'Checkpoint statistics did not survive the round trip'
Assert-Persist ($loaded.Receipts[0].Event.DamageEvents.Count -eq 1 -and $loaded.Receipts[0].Event.DamageEvents[0].TargetId -eq 2) 'Checkpoint receipt damage events did not survive the round trip'

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

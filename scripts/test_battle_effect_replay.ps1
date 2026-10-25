$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Effect($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
function Get-Sha($path) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $path))
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')

$officialHit = $catalog.CreateSkill(12390, 1, 1, 1, 1, 0, $null).HitGraphEntities |
    Where-Object { $_.EntityId -eq 12391 } | Select-Object -First 1
Assert-Effect ($null -ne $officialHit -and $officialHit.StatusListRandomOne) 'Official random-one status list was not mapped'
Assert-Effect ($officialHit.AppliedStatuses.Count -ge 2) 'Official random-one status list lost its entries'

function New-Unit($id, $side, $x, $y, $hp) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $id; $u.Side = $side; $u.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $u.X = $x; $u.Y = $y; $u.Hp = $hp; $u.MaxHp = $hp
    $u.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $u.Turn.Id = $id; $u.Turn.Hp = $hp; $u.Turn.Speed = 100 - $id
    return $u
}

function New-EffectSkill($randomOne) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 8301; $hit.DamageId = 8302; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $hit.StatusListRandomOne = $randomOne
    $hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 1.0; AffectedByProp = $false; LessThanHpPer = -1 })
    $hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26005; TargetType = 2; Chance = 1.0; AffectedByProp = $false; LessThanHpPer = -1 })
    $hit.HitMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
    $hit.HitMove.Distance = 1; $hit.HitMove.Direction = 1; $hit.HitMove.TowardsTargetPos = $false
    $hit.DisperseChance = 1.0; $hit.DisperseCount = 1
    $hit.DisperseStatusTypes.Add(2)
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-EffectBattle($skill, [ref]$checkpointRef) {
    $player = New-Unit 1 1 0 0 1000
    $enemy = New-Unit 2 2 1 0 5000
    $dispersible = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $dispersible.InstanceId = 1; $dispersible.ClassId = 27001; $dispersible.StatusType = 2
    $dispersible.CanDispersed = $true; $dispersible.DurationRound = -1; $dispersible.CurDurationRound = -1
    $dispersible.StartRound = -1; $dispersible.OwnerId = 2; $dispersible.CreatorId = 2
    $enemy.Statuses.Add($dispersible)
    $skill.Slot = 0; $skill.EntityId = 8300; $skill.ItemId = 8300
    $player.Skills.Clear(); $player.Skills.Add($skill)
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 31
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width = 10; $cp.Grid.Height = 10; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $checkpointRef.Value = $cp
    $f = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $f, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    return $executor.Step(0, 1)
}

$cpA = $null
$single = Invoke-EffectBattle (New-EffectSkill $true) ([ref]$cpA)
$applied = @($cpA.Units[1].Statuses | Where-Object { $_.ClassId -eq 26004 -or $_.ClassId -eq 26005 })
Assert-Effect ($applied.Count -eq 1) "Random-one status list applied $($applied.Count) entries"

$cpB = $null
$both = Invoke-EffectBattle (New-EffectSkill $false) ([ref]$cpB)
$appliedBoth = @($cpB.Units[1].Statuses | Where-Object { $_.ClassId -eq 26004 -or $_.ClassId -eq 26005 })
Assert-Effect ($appliedBoth.Count -eq 2) "Sequential status list applied $($appliedBoth.Count) entries"

# Replay: the same step must reproduce the recorded event and state.
$cpInitial = $null
$first = Invoke-EffectBattle (New-EffectSkill $false) ([ref]$cpInitial)
$json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cpInitial)
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
Assert-Effect ($restored.Sequence -eq $cpInitial.Sequence -and $restored.Counter -eq $cpInitial.Counter) 'Restored checkpoint lost its sequence state'
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$replay = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog).Step($restored.Sequence, $restored.Counter)
Assert-Effect ($replay.Replayed) 'Restored executor did not replay the recorded step'
Assert-Effect ($replay.Event.DamageEvents.Count -eq $first.Event.DamageEvents.Count) 'Replay damage event count differs'
Assert-Effect ($replay.Event.Movements.Count -eq $first.Event.Movements.Count) 'Replay movement count differs'
$replayedStatuses = @($restored.Units[1].Statuses | Where-Object { $_.ClassId -eq 26004 -or $_.ClassId -eq 26005 })
Assert-Effect ($replayedStatuses.Count -eq 2) 'Replay state lost the applied statuses'
Assert-Effect ($restored.Units[1].X -eq $cpInitial.Units[1].X) 'Replay state position differs'

# The restored executor must continue with the next step.
$nextCounter = $restored.Counter + 1
$nextSequence = $restored.Sequence
$next = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog).Step($nextSequence, $nextCounter)
Assert-Effect ($next.Checkpoint.Sequence -eq $nextSequence -and $next.Checkpoint.Counter -eq $nextCounter) 'Restored executor could not continue the battle'

Write-Output 'Battle effect replay checks passed: random-one status list, sequential list, checkpoint replay and continuation with status, dispel and displacement effects.'

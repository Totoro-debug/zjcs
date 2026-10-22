$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Status($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Status($classId, $duration) {
    $status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $status.ClassId = $classId
    $status.InstanceId = 1
    $status.DurationRound = $duration
    $status.CurDurationRound = $duration
    $status.StartRound = -1
    $status.RoundTarget = 2
    $status.RoundUpdateTiming = 1
    return $status
}

$timed = New-Status 5001 2
[Zjcs.Combat.Official.FightStatusLifecycle]::Initialize($timed, 0)
Assert-Status ($timed.StartRound -eq 0 -and $timed.CurDurationRound -eq 2) 'Status initialization did not record the official round bookkeeping'
Assert-Status ([Zjcs.Combat.Official.FightStatusLifecycle]::LeftRound($timed, 0) -eq 2) 'Status left rounds at apply round mismatch'
Assert-Status ([Zjcs.Combat.Official.FightStatusLifecycle]::LeftRound($timed, 1) -eq 1) 'Status left rounds after one round mismatch'
Assert-Status ([Zjcs.Combat.Official.FightStatusLifecycle]::LeftRound($timed, 2) -eq 0) 'Status left rounds at the last round mismatch'
Assert-Status ([Zjcs.Combat.Official.FightStatusLifecycle]::IsExpired($timed, 1) -eq $false -and [Zjcs.Combat.Official.FightStatusLifecycle]::IsExpired($timed, 2)) 'Status expiry round mismatch'

$infinite = New-Status 5002 -1
[Zjcs.Combat.Official.FightStatusLifecycle]::Initialize($infinite, 0)
Assert-Status ([Zjcs.Combat.Official.FightStatusLifecycle]::LeftRound($infinite, 999) -eq -1 -and -not [Zjcs.Combat.Official.FightStatusLifecycle]::IsExpired($infinite, 999)) 'Infinite status expired'

$extended = New-Status 5003 2
$extended.IsAddDurationRound = $true
$extended.MaxAddDurationRound = 100
[Zjcs.Combat.Official.FightStatusLifecycle]::Initialize($extended, 0)
[Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($extended, 1)
Assert-Status ($extended.CurDurationRound -eq 3 -and $extended.StartRound -eq 1) 'Stack duration extension did not follow UpdateStatusRound'
$extended.MaxAddDurationRound = 2
[Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($extended, 1)
Assert-Status ($extended.CurDurationRound -eq 2) 'Stack duration extension ignored MaxAddDurationRound'

$stacked = New-Status 5004 1
$stacked.IsOpenStack = $true
$stacked.StackCount = 1
$stacked.MaxStackedCount = 3
$added = [Zjcs.Combat.Official.FightStatusLifecycle]::AddStack($stacked, 5)
Assert-Status ($added -eq 2 -and $stacked.StackCount -eq 3) 'Status stack cap mismatch'
Assert-Status ([Zjcs.Combat.Official.FightStatusLifecycle]::AddStack($stacked, 1) -eq 0) 'Status exceeded its maximum stacks'

$unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$unit.Id = 1; $unit.Hp = 100; $unit.MaxHp = 100
$parallel = New-Status 5010 2
$parallel.ActionBehOfSameStatus = 0
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($unit, $parallel, 1, 1) | Out-Null
$parallel.StackCount = 2
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($unit, $parallel, 1, 2) | Out-Null
Assert-Status ($unit.Statuses.Count -eq 2) 'Parallel same-status application did not keep both instances'

$replaceUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$replaceUnit.Id = 2; $replaceUnit.Hp = 100; $replaceUnit.MaxHp = 100
$immediate = New-Status 5011 2
$immediate.ActionBehOfSameStatus = 1
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($replaceUnit, $immediate, 1, 1) | Out-Null
$immediate.DurationRound = 5
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($replaceUnit, $immediate, 2, 2) | Out-Null
Assert-Status ($replaceUnit.Statuses.Count -eq 1 -and $replaceUnit.Statuses[0].DurationRound -eq 5 -and $replaceUnit.Statuses[0].StartRound -eq 2) 'Immediate same-status application did not replace the old instance'

$discardUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$discardUnit.Id = 3; $discardUnit.Hp = 100; $discardUnit.MaxHp = 100
$discard = New-Status 5012 2
$discard.ActionBehOfSameStatus = 2
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($discardUnit, $discard, 1, 1) | Out-Null
$discard.DurationRound = 9
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($discardUnit, $discard, 2, 2) | Out-Null
Assert-Status ($discardUnit.Statuses.Count -eq 1 -and $discardUnit.Statuses[0].DurationRound -eq 2) 'Discard same-status application replaced the old instance'

$stackUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$stackUnit.Id = 4; $stackUnit.Hp = 100; $stackUnit.MaxHp = 100
$open = New-Status 5013 3
$open.IsOpenStack = $true
$open.StackCount = 1
$open.MaxStackedCount = 5
$open.IsAddDurationRound = $true
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($stackUnit, $open, 0, 1) | Out-Null
$open.StackCount = 2
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($stackUnit, $open, 1, 2) | Out-Null
Assert-Status ($stackUnit.Statuses.Count -eq 1 -and $stackUnit.Statuses[0].StackCount -eq 3) 'Open-stack application did not add stacks to the existing instance'
Assert-Status ($stackUnit.Statuses[0].CurDurationRound -eq 5) 'Open-stack application did not extend the duration'

$tickUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$tickUnit.Id = 5; $tickUnit.Hp = 100; $tickUnit.MaxHp = 100
$ticking = New-Status 5014 1
$ticking.OwnerId = 5; $ticking.CreatorId = 6
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($tickUnit, $ticking, 0, 1) | Out-Null
$units = [Zjcs.Combat.Official.FightBattleUnitState[]]@($tickUnit)
[Zjcs.Combat.Official.FightStatusLifecycle]::TickTurn($tickUnit, $units, 1, 0) | Out-Null
Assert-Status ($tickUnit.Statuses.Count -eq 1) 'Status expired under the wrong round update timing'
$removed = [Zjcs.Combat.Official.FightStatusLifecycle]::TickTurn($tickUnit, $units, 1, 1)
Assert-Status ($removed -eq 1 -and $tickUnit.Statuses.Count -eq 0) 'Status did not expire at the configured round end'

$creator = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$creator.Id = 7; $creator.Hp = 0; $creator.MaxHp = 100
$owner = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$owner.Id = 8; $owner.Hp = 100; $owner.MaxHp = 100
$dependent = New-Status 5015 5
$dependent.RoundTarget = 1
$dependent.RemoveAtRoundTargetDestroy = $true
$dependent.CreatorId = 7
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $dependent, 0, 1) | Out-Null
$swept = [Zjcs.Combat.Official.FightStatusLifecycle]::RemoveDestroyedTargets(
    [Zjcs.Combat.Official.FightBattleUnitState[]]@($creator, $owner))
Assert-Status ($swept -eq 1 -and $owner.Statuses.Count -eq 0) 'Destroyed round target did not remove the dependent status'

$noneUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$noneUnit.Id = 9; $noneUnit.Hp = 100; $noneUnit.MaxHp = 100
$none = New-Status 5016 5
$none.RoundTarget = 0
$none.RemoveAtRoundTargetDestroy = $true
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($noneUnit, $none, 0, 1) | Out-Null
$noneSwept = [Zjcs.Combat.Official.FightStatusLifecycle]::RemoveDestroyedTargets(
    [Zjcs.Combat.Official.FightBattleUnitState[]]@($noneUnit))
Assert-Status ($noneSwept -eq 0 -and $noneUnit.Statuses.Count -eq 1) 'Status without a round target was removed as destroyed'

# Executor integration: a one-round status ends after the owner's turn.
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1; $player.Hp = 1000; $player.MaxHp = 1000
$player.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$player.Turn.Id = 1; $player.Turn.Hp = 1000; $player.Turn.Speed = 100
$player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 600; $skill.ItemId = 601; $skill.TargetType = 1
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = 602; $hit.DamageId = 603; $hit.DamageType = 8
$hit.HasCustomDamage = $true; $hit.CustomDamage = 0; $hit.TargetMode = 0
$skill.Hits.Add($hit)
$player.Skills.Add($skill)
$battleStatus = New-Status 5020 1
$battleStatus.OwnerId = 1; $battleStatus.CreatorId = 1
[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($player, $battleStatus, 0, 1) | Out-Null
$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2; $enemy.X = 1; $enemy.Y = 0
$enemy.Hp = 5000; $enemy.MaxHp = 5000
$enemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$enemy.Turn.Id = 2; $enemy.Turn.Hp = 5000; $enemy.Turn.Speed = 50
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.RandomSeed = 1
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$null = $executor.Step(0, 1)
Assert-Status ($player.Statuses.Count -eq 0) 'Executed turn did not expire the one-round status at the player round end'

Write-Output 'Battle status lifecycle checks passed: round duration, infinite duration, stack extension and caps, same-status merge rules, round-timing expiry, destroyed-target sweep and executor integration.'

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-SkillStart($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
function Get-Sha($path) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $path))
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}
function New-Unit([int]$id, [int]$side, [int]$speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($id -eq 1) { 1 } else { 2 }
    $unit.Hp = 1000; $unit.MaxHp = 1000; $unit.Level = 1; $unit.SubRank = 1
    $unit.Turn.Id = $id; $unit.Turn.Hp = 1000; $unit.Turn.Speed = $speed
    return $unit
}

$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')
$status = $catalog.CreateStatus(24476, 1, 2, 1)
$mapped = $status.HasSkillStartTrigger -and $status.SkillStartRulesSupported -and
    $status.SkillStartSource -eq 2 -and $status.SkillStartActionTypes.Count -eq 2 -and
    $status.SkillStartActionTypes -contains 2 -and $status.SkillStartActionTypes -contains 6 -and
    $status.SkillStartSkills.Count -eq 1 -and $status.SkillStartSkills[0].EntityId -eq 12580
Assert-SkillStart $mapped 'Official status 24476 skill-start configuration was not mapped'
$officialSkill = $catalog.CreateSkill(12860, 0, 12860, 1, 1, 0, $null)
Assert-SkillStart ($officialSkill.ActionType -eq 2) 'Official FightSkillComponent.ActionType was not assembled'

$player = New-Unit 1 1 100
$player.X = 1; $player.Y = 1
$player.Statuses.Add($status)
$enemy = New-Unit 2 2 1
$enemy.X = 2; $enemy.Y = 1
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 90000; $skill.ItemId = 90001
$skill.ActionType = 2; $skill.TargetType = 1; $skill.RangeVerified = $true
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$player.Skills.Add($skill)
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 23
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 8; $checkpoint.Grid.Height = 8; $checkpoint.Grid.DefaultGroundType = 2
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.Spawned = $true; $wave.UnitIds.Add(1); $wave.UnitIds.Add(2)
$checkpoint.Waves.Add($wave)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
$result = $runner.Step(0, 1)
$triggerCast = @($result.Event.SkillCasts | Where-Object {
    $_.ActorId -eq 2 -and $_.SkillEntityId -eq 12580 -and $_.TargetIds -contains 1
})
$parentCast = @($result.Event.SkillCasts | Where-Object {
    $_.ActorId -eq 1 -and $_.SkillEntityId -eq 90000
})
$executed = $result.Event.ActorId -eq 1 -and $triggerCast.Count -eq 1 -and
    $parentCast.Count -eq 1 -and $status.SkillStartTriggeredCount -eq 1
Assert-SkillStart $executed 'Skill-start status did not execute its configured creator-to-applicator skill before the parent cast'

$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint))
$savedStatus = $loaded.Units[0].Statuses | Where-Object { $_.ClassId -eq 24476 } | Select-Object -First 1
$persisted = $null -ne $savedStatus -and $savedStatus.SkillStartTriggeredCount -eq 1 -and
    $savedStatus.SkillStartSkills[0].EntityId -eq 12580
Assert-SkillStart $persisted 'Skill-start trigger state did not survive checkpoint persistence'

$thunder = $catalog.CreateStatus(24440, 2, 1, 1, 0, 0, 1, 1, 1, $null, 0)
$enemy.Statuses.Add($thunder)
$units = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($player); $units.Add($enemy)
$targets = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$targets.Add($player)
# Seed 30000 gives 0.674..., deliberately above the ordinary 50% threshold.
function Collect-Thunder {
    return [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectSkillStartSkills(
        $enemy, $skill, $targets, $units,
        [Zjcs.Combat.Official.FightRandomStream]::new(30000), $catalog)
}
Assert-SkillStart ($catalog.CalculateSkillStartTriggerRate($thunder, $enemy, 0.5) -eq 0.5) 'Dry Thunder Judgment base chance changed'
Assert-SkillStart (@(Collect-Thunder).Count -eq 0) 'Dry Thunder Judgment ignored its 50% chance'
$enemy.Statuses.Add($catalog.CreateStatus(24012, 2, 2, 1))
Assert-SkillStart ($catalog.CalculateSkillStartTriggerRate($thunder, $enemy, 0.5) -eq 1.0) 'Wet did not raise Thunder Judgment chance to the described 100%'
$wetCast = @(Collect-Thunder)
Assert-SkillStart ($wetCast.Count -eq 1 -and $wetCast[0].Trigger.EntityId -eq 12520 -and
    $wetCast[0].SourceUnitId -eq 1 -and $wetCast[0].TargetUnitId -eq 2) 'Wet Thunder Judgment was not invoked by the configured creator'
$enemy.Statuses.RemoveAt(1)
$enemy.Statuses.Add($catalog.CreateStatus(24441, 2, 2, 1))
Assert-SkillStart (@(Collect-Thunder).Count -eq 1) 'Electric shock did not satisfy the alternative condition'
$enemy.Statuses.Add($catalog.CreateStatus(24012, 2, 2, 1))
Assert-SkillStart ($catalog.CalculateSkillStartTriggerRate($thunder, $enemy, 0.5) -eq 1.0) 'HasAnyStatus alternatives were multiplied more than once'
$thunderCheckpoint = $checkpoint
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($thunderCheckpoint))
$restoredEnemy = $restored.Units[1]
Assert-SkillStart ($catalog.CalculateSkillStartTriggerRate($restoredEnemy.Statuses[0], $restoredEnemy, 0.5) -eq 1.0) 'Conditional trigger chance did not survive checkpoint restoration'
$enemy.Statuses.RemoveRange(1, 2)
$player.Statuses.Add($catalog.CreateStatus(24012, 1, 1, 1))
Assert-SkillStart (@(Collect-Thunder).Count -eq 0) 'Trigger incorrectly tested the creator instead of its applicator'
$enemy.Statuses.Add($catalog.CreateStatus(24013, 2, 2, 1))
Assert-SkillStart (@(Collect-Thunder).Count -eq 0) 'An unrelated status satisfied HasAnyStatus'

Write-Output 'Battle status skill-start checks passed: 24476 timing/targeting/persistence; Thunder Judgment dry/wet/electric, any-condition semantics, applicator ownership and restored chance.'

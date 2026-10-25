$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
Add-Type -Path (Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
function Require($condition, $message) { if (-not $condition) { throw $message } }
function Read-Source($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Source($name) {
    $raw = [IO.File]::ReadAllBytes((Join-Path $root "unity/Assets/Official/Combat/$name.json"))
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw)).ToLowerInvariant()
}
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Source 'skill-rules'), (Hash-Source 'skill-rules'), '854_156383',
    (Read-Source 'battle-rules'), (Hash-Source 'battle-rules'), '854_156383')
function New-Unit([int]$id, [int]$side, [int]$speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.Hp = 1000; $unit.MaxHp = 1000
    $unit.Level = 1; $unit.SubRank = 1; $unit.X = $id; $unit.Y = 1
    $unit.CharacterType = if ($id -eq 1) { 1 } else { 2 }
    $unit.Turn.Id = $id; $unit.Turn.Hp = 1000; $unit.Turn.Speed = $speed
    return $unit
}
function New-Mark($owner) {
    $status = $catalog.CreateStatus(24062, $owner.Id, 1, 1)
    $status.StartRound = 0
    $owner.Statuses.Add($status)
    return $status
}
function Record($owner, [int]$source, [int]$skill, [long]$loss, [int]$kind = 1,
    [bool]$trueDamage = $false, [bool]$cure = $false, [bool]$cost = $false) {
    $hit = [Zjcs.Combat.Official.FightBattleDamageEvent]::new()
    $hit.SourceId = $source; $hit.TargetId = $owner.Id; $hit.SkillEntityId = $skill
    $hit.SkillItemId = $skill + 100; $hit.DamageType = $kind
    $hit.BeforeHp = 1000; $hit.AfterHp = 1000 - $loss; $hit.Damage = -$loss
    $hit.AbsoluteDamage = -1000; $hit.ShieldDamage = 999
    $hit.TrueDamage = $trueDamage; $hit.IsCure = $cure; $hit.IsSkillCost = $cost
    [Zjcs.Combat.Official.FightStatusAccumulatedDamageRuntime]::RecordHit($owner, $hit)
}
function New-Checkpoint($owner) {
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 23
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 8; $checkpoint.Grid.Height = 8; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add((New-Unit 1 1 1)); $checkpoint.Units.Add($owner)
    return $checkpoint
}
function Begin($checkpoint) {
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
}
function Restore($checkpoint) {
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    [Zjcs.Combat.Official.FightBattleExecutor]::Restore($checkpoint, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
}

$owner = New-Unit 2 2 100
$mark = New-Mark $owner
Require ($mark.AccumulatedDamage.RulesSupported -and $mark.AccumulatedDamage.SkillEntityId -eq 13148 -and
    $mark.AccumulatedDamage.AccumulationDamagePer -eq 3000 -and
    $mark.AccumulatedDamage.HitDamageTypes.Count -eq 7) 'Official accumulated-damage configuration was not assembled'
Record $owner 1 90001 60
Record $owner 3 90002 40
Record $owner 1 90001 80 1 $true
Record $owner 1 90001 80 1 $false $true
Record $owner 1 90001 80 1 $false $false $true
Record $owner 1 90001 80 8
Record $owner 1 90001 0
Require ($mark.AccumulatedDamage.HpDamage -eq 100 -and $mark.AccumulatedDamage.Contributions.Count -eq 2) 'Accumulator included true damage, cure, skill cost, custom type 8 or shields'
Require ([Zjcs.Combat.Official.FightStatusAccumulatedDamageRuntime]::CustomDamage($mark) -eq 30) 'Mark did not use the described 30% of actual life loss'
$mark.AccumulatedDamage.OnlyDamageByCreator = $true
Record $owner 3 90002 20
Require ($mark.AccumulatedDamage.HpDamage -eq 100) 'Creator-only damage filter was ignored'
$mark.AccumulatedDamage.OnlyDamageByCreator = $false

$checkpoint = New-Checkpoint $owner
$runner = Begin $checkpoint
$saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($runner.State))
$restoredMark = $saved.Units[1].Statuses[0]
Require ($restoredMark.AccumulatedDamage.HpDamage -eq 100 -and
    $restoredMark.AccumulatedDamage.Contributions[1].HpDamage -eq 40) 'Accumulated damage/source contributions did not survive persistence'
$result = $runner.Step(0, 1)
$replayed = (Restore $saved).Step(0, 1)
Require ($result.Event.ActorId -eq 2 -and $result.Event.DamageEvents.Count -eq 1 -and
    $result.Event.DamageEvents[0].HitEntityId -eq 13149 -and
    $result.Event.DamageEvents[0].SourceId -eq 1 -and $result.Event.DamageEvents[0].TargetId -eq 2 -and
    $result.Event.DamageEvents[0].Damage -eq -30 -and $owner.Statuses.Count -eq 0) 'Mark expiration did not cast its official skill exactly once'
Require ($replayed.Event.DamageEvents[0].Damage -eq -30 -and
    $replayed.Checkpoint.RandomSeed -eq $result.Checkpoint.RandomSeed) 'Restored accumulated damage diverged'
$duplicate = $runner.Step(0, 1)
Require ($duplicate.Event.DamageEvents.Count -eq 1 -and $runner.State.DamageEventSerial -eq 1 -and
    $owner.Hp -eq 970) 'A duplicate step request executed the mark explosion again'

$shielded = New-Unit 2 2 100
$shieldMark = New-Mark $shielded
Record $shielded 1 90001 100
$shield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ClassId = 99998; $shield.InstanceId = 2; $shield.DurationRound = -1; $shield.StartRound = 0
$shield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$shield.ShieldValue = 100; $shield.ShieldRemainValue = 100; $shield.ShieldInitialized = $true
$shielded.Statuses.Add($shield)
$shieldResult = (Begin (New-Checkpoint $shielded)).Step(0, 1)
Require ($shieldResult.Event.DamageEvents[0].Damage -eq 0 -and
    $shieldResult.Event.DamageEvents[0].ShieldDamage -eq 30 -and $shielded.Hp -eq 1000) 'Mark explosion bypassed the configured shield handling'

$replacement = New-Unit 2 2 100
$replacementRunner = Begin (New-Checkpoint $replacement)
$old = New-Mark $replacement
Record $replacement 1 90001 101
$template = $catalog.CreateStatus(24062, 2, 1, 2)
$new = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($replacement, $template, -1, 2)
Require ($replacement.EndedAccumulatedDamageStatuses.Count -eq 1 -and
    $new.AccumulatedDamage.HpDamage -eq 0 -and -not [Object]::ReferenceEquals($new.AccumulatedDamage, $template.AccumulatedDamage)) 'Status replacement lost the old accumulator or shared mutable template state'
$queuedRestore = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($replacementRunner.State))
Require ($queuedRestore.Units[1].EndedAccumulatedDamageStatuses.Count -eq 1 -and
    $queuedRestore.Units[1].EndedAccumulatedDamageStatuses[0].AccumulatedDamage.HpDamage -eq 101) 'Pending status-end settlement was lost on save'
$replacementResult = $replacementRunner.Step(0, 1)
Require ((Restore $queuedRestore).Step(0, 1).Event.DamageEvents[0].Damage -eq -30) 'A restored pending status end changed its damage'
Require ($replacementResult.Event.DamageEvents.Count -eq 1 -and $replacementResult.Event.DamageEvents[0].Damage -eq -30 -and
    $new.AccumulatedDamage.HpDamage -eq 0) 'Replaced mark did not explode once, truncation changed, or its type 8 damage fed the new mark'

$overkill = New-Unit 2 2 100
$overkillMark = New-Mark $overkill
Record $overkill 1 90001 100
$overkill.Hp = 10
$overkillResult = (Begin (New-Checkpoint $overkill)).Step(0, 1)
Require ($overkillResult.Event.DamageEvents[0].Damage -eq -10 -and $overkill.Hp -eq 0) 'Mark damage exceeded the remaining life during settlement'

# Accumulate a real executor hit before the owner's end event, rather than seeding the counter.
$hitOwner = New-Unit 2 2 100
$hitMark = New-Mark $hitOwner
$hitCheckpoint = New-Checkpoint $hitOwner
$attacker = $hitCheckpoint.Units[0]
$attacker.Turn.Speed = 100
$attack = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$attack.Slot = 0; $attack.EntityId = 90010; $attack.ItemId = 90110; $attack.TargetType = 1
$attack.HasCustomDamage = $true; $attack.CustomDamage = 60; $attack.RangeVerified = $true
$attack.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = 90011; $hit.DamageId = 90012; $hit.DamageType = 2; $hit.ActionType = 4
$attack.Hits.Add($hit)
$attacker.Skills.Add($attack)
$attacker.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$hitRunner = Begin $hitCheckpoint
$hitStep = $hitRunner.Step(0, 1)
Require ($hitStep.Event.ActorId -eq 1 -and $hitStep.Event.DamageEvents.Count -eq 1 -and
    $hitMark.AccumulatedDamage.HpDamage -eq 60 -and $hitOwner.Hp -eq 940) 'The executor did not accumulate its real configured hit before status end'
$endStep = $hitRunner.Step(0, 2)
Require ($endStep.Event.ActorId -eq 2 -and $endStep.Event.DamageEvents.Count -eq 1 -and
    $endStep.Event.DamageEvents[0].Damage -eq -18 -and $hitOwner.Hp -eq 922) 'The accumulated real hit did not become one 30% end explosion'
Write-Host 'Accumulated-damage checks passed: official chain, actual HP/type/source filters, expiry/replacement, shield/overkill, deep copies and checkpoint replay.'

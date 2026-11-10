. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-SkillEnd($condition, $message) { if (-not $condition) { throw $message } }
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
$status = $catalog.CreateStatus(25011, 1, 2, 1)
$creatorStatus = $catalog.CreateStatus(60840, 1, 2, 2)
$mapped = $status.HasSkillEndTrigger -and $status.SkillEndRulesSupported -and
    $status.SkillEndMetCount -eq 1 -and $status.SkillEndFilteredActionTypes -contains 1 -and
    $status.SkillEndStatuses.Count -eq 1 -and $status.SkillEndStatuses[0].EntityId -eq 24466 -and
    $status.SkillEndStatuses[0].Target -eq 2 -and
    $creatorStatus.SkillEndRulesSupported -and $creatorStatus.SkillEndStatuses.Count -eq 1 -and
    $creatorStatus.SkillEndStatuses[0].EntityId -eq 60841 -and
    $creatorStatus.SkillEndStatuses[0].Target -eq 3
Assert-SkillEnd $mapped 'Official status 25011/60840 skill-end configurations were not mapped'

$player = New-Unit 1 1 100
$player.X = 1; $player.Y = 1; $player.Statuses.Add($status); $player.Statuses.Add($creatorStatus)
$enemy = New-Unit 2 2 1
$enemy.X = 2; $enemy.Y = 1
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 90000; $skill.ItemId = 90001
$skill.ActionType = 2; $skill.TargetType = 1; $skill.RangeVerified = $true
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$player.Skills.Add($skill)
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 31
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
$triggeredStatus = @($player.Statuses | Where-Object { $_.ClassId -eq 24466 })
$creatorTriggeredStatus = @($enemy.Statuses | Where-Object { $_.ClassId -eq 60841 })
$executed = $result.Event.ActorId -eq 1 -and $triggeredStatus.Count -eq 1 -and
    $creatorTriggeredStatus.Count -eq 1 -and $status.SkillEndTriggeredCount -eq 1 -and
    $creatorStatus.SkillEndTriggeredCount -eq 1 -and $status.SkillEndMetCountProgress -eq 0
Assert-SkillEnd $executed 'Skill-end statuses were not applied to both configured applicator and creator targets'
$filteredSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$filteredSkill.ActionType = 1
$filteredActionApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses(
    $player, $filteredSkill, $checkpoint.Units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(37))
$filterPreserved = $filteredActionApplied -eq 0 -and $status.SkillEndTriggeredCount -eq 1
Assert-SkillEnd $filterPreserved 'Configured action-end filter did not suppress a Move action'

$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint))
$savedStatus = $loaded.Units[0].Statuses | Where-Object { $_.ClassId -eq 25011 } | Select-Object -First 1
$persisted = $null -ne $savedStatus -and $savedStatus.SkillEndTriggeredCount -eq 1 -and
    $savedStatus.SkillEndStatuses[0].EntityId -eq 24466
$savedCreatorStatus = $loaded.Units[0].Statuses | Where-Object { $_.ClassId -eq 60840 } | Select-Object -First 1
$persisted = $persisted -and $null -ne $savedCreatorStatus -and
    $savedCreatorStatus.SkillEndTriggeredCount -eq 1 -and
    $loaded.Units[1].Statuses[0].ClassId -eq 60841
Assert-SkillEnd $persisted 'Skill-end trigger state did not survive checkpoint persistence'

Write-Output 'Battle status skill-end checks passed: fixed 25011/60840 action filtering, MetCount, applicator/creator status targets and checkpoint persistence.'

# 10564's legacy direct status is a confirmed offline binding to its holder.
$player = New-Unit 1 1 100
$enemy = New-Unit 2 2 1
$player.X=1; $player.Y=1; $enemy.X=2; $enemy.Y=1
$passiveProps=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
foreach($entry in $catalog.CalculateSkillProps(25029,2,20,1).GetEnumerator()) {
    $passiveProps.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$entry.Key;Value=$entry.Value})
}
$passive=$catalog.CreateStatus(25029,1,1,1,25029,10564,2,20,1,$passiveProps)
$passive.BlessPropertyScales=[Collections.Generic.List[Zjcs.Combat.Official.FightStatusPropertyScale]]::new()
$passive.BlessPropertyScales.Add([Zjcs.Combat.Official.FightStatusPropertyScale]@{EntityId=24221;PropertyId=1;Factor=.25})
$player.Statuses.Add($passive)
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($player); $units.Add($enemy)
$random=[Zjcs.Combat.Official.FightRandomStream]::new(73)
$ordinary=[Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$ordinary.EntityId=90000; $ordinary.ItemId=90001; $ordinary.ActionType=2
Assert-SkillEnd ($passive.SkillEndRulesSupported -and $passive.SkillEndLegacyStatusId -eq 24221) 'Legacy passive definition was not normalized'
foreach($action in @(1,0,68,10)) {
    $ordinary.ActionType=$action
    [void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses($player,$ordinary,$units,$catalog,$random)
}
Assert-SkillEnd ($passive.SkillEndMetCountProgress -eq 0) 'Excluded actions accumulated passive progress'
$ordinary.ActionType=2; $passive.ForbiddenType=1
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses($player,$ordinary,$units,$catalog,$random)
Assert-SkillEnd ($passive.SkillEndMetCountProgress -eq 0) 'Forbidden passive accumulated progress'
$passive.ForbiddenType=0; $ordinary.CanTriggerChild=$false
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses($player,$ordinary,$units,$catalog,$random)
Assert-SkillEnd ($passive.SkillEndMetCountProgress -eq 0) 'Child-trigger gate accumulated progress'
$ordinary.CanTriggerChild=$true
for($i=0;$i -lt 3;$i++) {
    [void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses($player,$ordinary,$units,$catalog,$random)
}
Assert-SkillEnd ($passive.SkillEndMetCountProgress -eq 3 -and @($player.Statuses | Where-Object ClassId -eq 24221).Count -eq 0) 'Passive fired before four skill ends'
$checkpoint=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId=1; $checkpoint.MaxRound=10; $checkpoint.RandomSeed=73
$checkpoint.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=8;Height=8;DefaultGroundType=2}
$checkpoint.Units.AddRange($units)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
$ordinary.Slot=0; $ordinary.TargetType=1; $ordinary.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$player.Skills.Add($ordinary)
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=1})
$initial=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
# Simulate a legacy checkpoint whose definition was blocked, retaining its existing counter.
$passive.SkillEndRulesSupported=$false; $passive.SkillEndLegacyStatusId=0; $passive.SkillEndStatuses.Clear()
$loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($loaded,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$result=$runner.Step(0,1)
$passive=$loaded.Units[0].Statuses | Where-Object ClassId -eq 25029 | Select-Object -First 1
$buff=$loaded.Units[0].Statuses | Where-Object ClassId -eq 24221 | Select-Object -First 1
Assert-SkillEnd ($null -ne $buff -and $passive.SkillEndMetCountProgress -eq 0 -and $passive.SkillEndTriggeredCount -eq 1) 'Fourth actual zero-damage cast did not trigger the saved passive counter'
Assert-SkillEnd ($result.Event.SkillCasts.Count -eq 1 -and $result.Event.DamageEvents.Count -eq 0 -and $loaded.Units[0].SkillState.Slots[0].ReleasedTimes -eq 1) 'Passive spent an extra release or required damage'
Assert-SkillEnd ($buff.SourceSkillRank -eq 2 -and $buff.SourceSkillLevel -eq 20 -and $buff.SourceSubRank -eq 1 -and $buff.RootSkillId -eq 25029 -and $buff.ApplyingSkillItemId -eq 10564 -and $buff.Origin.SkillItemId -eq 10564) 'Buff inherited the active skill instead of the passive cultivation/source'
Assert-SkillEnd ($buff.BlessPropertyScales.Count -eq 1 -and $buff.BlessPropertyScales[0].Factor -eq .25) 'Buff lost the passive blessing after checkpoint restoration'
for($i=0;$i -lt 12;$i++) { [void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses($loaded.Units[0],$ordinary,$loaded.Units,$catalog,$random) }
$buffs=@($loaded.Units[0].Statuses | Where-Object ClassId -eq 24221)
Assert-SkillEnd ($buffs.Count -eq 1 -and $buffs[0].StackCount -eq 3 -and $passive.SkillEndTriggeredCount -eq 4) 'Repeated thresholds did not obey the original three-stack cap'
$copyOwner=New-Unit 3 1 1
$copy=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($copyOwner,$passive,-1,4,$random)
$copy.SkillEndStatuses.Clear()
Assert-SkillEnd ($passive.SkillEndStatuses.Count -eq 1) 'Status copy shared the normalized trigger list'
Write-Output 'Passive 10564 passed excluded/forbidden/child gates, four-end reset, zero-damage actual cast, passive source/cultivation, original three-stack cap and legacy checkpoint recovery.'

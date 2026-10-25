$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
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

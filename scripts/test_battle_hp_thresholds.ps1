. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Get-FightCombatSources)

function Assert-Hp($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { [IO.File]::ReadAllText((Join-Path $root $path)) }
function Hash-Text($text) {
    ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($text))) -replace '-', '').ToLowerInvariant()
}

$skillJson = Read-Text 'unity/Assets/Official/Combat/skill-rules.json'
$battleJson = Read-Text 'unity/Assets/Official/Combat/battle-rules.json'
$sourceVersion = (Read-Text 'unity/Assets/Official/Combat/battle-rules-source-manifest.json' |
    ConvertFrom-Json).manifest
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash-Text $skillJson), $sourceVersion,
    $battleJson, (Hash-Text $battleJson), $sourceVersion)

function Unit($id, $hp, $characterType = 1) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $id; $u.Hp = $hp; $u.MaxHp = 100; $u.CharacterType = $characterType
    $u.Targetable = $true; $u.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    return $u
}

# 60410 is the shipped death threshold status: at HP <= 2 it applies 60413.
$death = $catalog.CreateStatus(60410, 1, 1, 1)
$owner = Unit 1 1
$owner.Statuses.Add($death)
$units = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($owner)
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(17)
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, $rng, $null, $units)
Assert-Hp ($owner.Statuses.ClassId -contains 60413) 'HP-less-than status trigger did not apply 60413.'

# 60600 exposes the same death threshold through a configured skill invocation.
$skillStatus = $catalog.CreateStatus(60600, 1, 1, 2)
$owner.Statuses.Clear(); $owner.Statuses.Add($skillStatus)
$invocations = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusSkillInvocation]]::new()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units, $invocations)
Assert-Hp ($invocations.Count -eq 1 -and $invocations[0].Trigger.EntityId -eq 60601) `
    'HP-less-than configured skill invocation was not collected.'

$owner.Hp = 0
$skillStatus.HpLessThanTriggeredCount = 0
$invocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units, $invocations)
Assert-Hp ($invocations.Count -eq 1 -and $invocations[0].AllowDeadSource) `
    'IsInvokeOnDie did not preserve a dead-source skill invocation.'
$owner.Hp = 1

# 60050 is an HP-limit status that releases 60051 at the lower limit.
$limitStatus = $catalog.CreateStatus(60050, 1, 1, 3)
$owner.Statuses.Clear(); $owner.Statuses.Add($limitStatus)
$invocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units, $invocations)
Assert-Hp ($invocations.Count -eq 1 -and $invocations[0].Trigger.EntityId -eq 60051) `
    'HP-limit configured skill invocation was not collected.'

# 25296 exercises ApplyByAroundUnitCount: its configured grid-item neighbour 3051
# is discovered from the owner position and contributes one configured skill invocation.
$around = $catalog.CreateStatus(25296, 1, 1, 4)
$aroundTarget = Unit 9 100 6
$aroundTarget.MonsterId = 3051; $aroundTarget.X = 1; $aroundTarget.Y = 0
$units.Clear(); $units.Add($owner); $units.Add($aroundTarget)
$owner.Statuses.Clear(); $owner.Statuses.Add($around)
$invocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units, $invocations)
Assert-Hp ($invocations.Count -eq 2 -and ($invocations.Trigger.EntityId -contains 13548) `
    -and $invocations[0].SourceUnitId -eq $owner.Id) `
    'ApplyByAroundUnitCount did not execute its configured neighbour skill.'

# 25030 releases 11330 at the end of an ActionType 60 skill.
$actionEnd = $catalog.CreateStatus(25030, 1, 1, 5)
$owner.Statuses.Clear(); $owner.Statuses.Add($actionEnd)
$actionSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]@{
    EntityId = 999901; ActionType = 60; Rank = 1; Level = 1; SubRank = 0
}
$actionInvocations = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusSkillInvocation]]::new()
$actionApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses(
    $owner, $actionSkill, $units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(17), $actionInvocations)
Assert-Hp ($actionApplied -eq 1 -and $actionInvocations.Count -eq 1 `
    -and $actionInvocations[0].Trigger.EntityId -eq 11330 `
    -and $actionInvocations[0].SourceUnitId -eq $owner.Id `
    -and $actionInvocations[0].TargetUnitId -eq $owner.Id) `
    'ActionEnd did not collect the configured 11330 skill invocation.'
$actionInvocations.Clear()
$actionSkill.ActionType = 1
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses(
    $owner, $actionSkill, $units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(17), $actionInvocations)
Assert-Hp ($actionInvocations.Count -eq 0) `
    'ActionEnd ignored its configured action-type filter.'
$unsupportedActionEnd = $catalog.CreateStatus(25144, 1, 1, 6)
Assert-Hp (-not $unsupportedActionEnd.ActionEndRulesSupported) `
    'ActionEnd accepted a trigger with unsupported custom damage parameters.'

# MoveNear triggers collect range targets at start and after movement.
$nearSource = Unit 20 100
$nearTarget = Unit 21 100 1
$nearSource.X = 0; $nearSource.Y = 0; $nearSource.Side = 0
$nearTarget.X = 1; $nearTarget.Y = 0; $nearTarget.Side = 1
$nearStatus = $catalog.CreateStatus(12353, $nearSource.Id, $nearSource.Id, 1)
$nearSource.Statuses.Add($nearStatus)
$nearUnits = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$nearUnits.Add($nearSource); $nearUnits.Add($nearTarget)
$nearInvocations = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectMoveNearSkills(
        $nearSource, $nearUnits,
        [Zjcs.Combat.Official.FightStatusTriggerRuntime]::MoveNearEventStart,
        $null, [Zjcs.Combat.Official.FightRandomStream]::new(17))
Assert-Hp ($nearInvocations.Count -eq 1 -and $nearInvocations[0].Trigger.EntityId -eq 12570 `
    -and $nearInvocations[0].SourceUnitId -eq $nearTarget.Id `
    -and $nearInvocations[0].TargetUnitId -eq $nearSource.Id) `
    'MoveNear start trigger did not preserve its configured range source and target.'
$nearInvocations = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectMoveNearSkills(
        $nearSource, $nearUnits,
        [Zjcs.Combat.Official.FightStatusTriggerRuntime]::MoveNearEventStart,
        $null, [Zjcs.Combat.Official.FightRandomStream]::new(17))
Assert-Hp ($nearInvocations.Count -eq 0) 'MoveNear start trigger ignored its one-shot guard.'

$nearEnter = $catalog.CreateStatus(12353, $nearSource.Id, $nearSource.Id, 2)
$nearSource.Statuses.Clear(); $nearSource.Statuses.Add($nearEnter)
$nearTarget.X = 1; $nearTarget.Y = 0
$nearMovement = [Zjcs.Combat.Official.FightBattleMovementEvent]@{
    UnitId = $nearTarget.Id; Path = [Collections.Generic.List[Zjcs.Combat.Official.FightGridPoint]]@(
         [Zjcs.Combat.Official.FightGridPoint]@{ X = 3; Y = 0 },
        [Zjcs.Combat.Official.FightGridPoint]@{ X = 1; Y = 0 })
}
$nearInvocations = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectMoveNearSkills(
    $nearSource, $nearUnits,
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::MoveNearEventEnterRange,
    $nearMovement, [Zjcs.Combat.Official.FightRandomStream]::new(17))
Assert-Hp ($nearInvocations.Count -eq 1 -and $nearInvocations[0].Trigger.EntityId -eq 12570) `
    'MoveNear enter-range trigger did not detect the outside-to-inside transition.'

# KillSkill status and skill triggers use the killer as source and honor filters/counts.
$killStatus = $catalog.CreateStatus(25028, 1, 1, 7)
$defeated = Unit 10 0
$units.Clear(); $units.Add($owner); $units.Add($defeated)
$owner.Hp = 100
$owner.Statuses.Clear(); $owner.Statuses.Add($killStatus)
$killApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyKillSkills(
    $owner, $defeated, $null, $units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(17))
Assert-Hp ($killApplied -eq 1 -and ($owner.Statuses.ClassId -contains 24220)) `
    'KillSkill did not apply its configured self status after a kill.'

$killSkillStatus = $catalog.CreateStatus(60590, 1, 1, 8)
$defeated.MonsterId = 3051
$owner.Statuses.Clear(); $owner.Statuses.Add($killSkillStatus)
$killInvocations = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusSkillInvocation]]::new()
$killApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyKillSkills(
    $owner, $defeated, $null, $units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(17), $killInvocations)
Assert-Hp ($killApplied -eq 1 -and $killInvocations.Count -eq 1 `
    -and $killInvocations[0].Trigger.EntityId -eq 60591 `
    -and $killInvocations[0].SourceUnitId -eq $owner.Id `
    -and $killInvocations[0].TargetUnitId -eq $owner.Id) `
    'KillSkill did not collect its filtered skill invocation.'
$killInvocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyKillSkills(
    $owner, $defeated, $null, $units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(17), $killInvocations)
Assert-Hp ($killInvocations.Count -eq 0) `
    'KillSkill ignored its configured maximum trigger count.'

$deathPositionStatus = $catalog.CreateStatus(25152, 1, 1, 9)
$owner.Statuses.Clear(); $owner.Statuses.Add($deathPositionStatus)
$killInvocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyKillSkills(
    $owner, $defeated, $null, $units, $catalog,
    [Zjcs.Combat.Official.FightRandomStream]::new(17), $killInvocations)
Assert-Hp ($killInvocations.Count -eq 1 `
    -and $killInvocations[0].Trigger.EntityId -eq 12450 `
    -and $killInvocations[0].TargetUnitId -eq $defeated.Id `
    -and $killInvocations[0].AllowDeadTarget) `
    'KillSkill did not preserve the configured death-position target.'

# 25067 removes its configured 24315 status when the HP condition is no longer met.
$notMeet = $catalog.CreateStatus(25067, 1, 1, 5)
$units.Clear(); $units.Add($owner)
$owner.Hp = 30
$owner.Statuses.Clear(); $owner.Statuses.Add($notMeet)
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units)
Assert-Hp ($owner.Statuses.ClassId -contains 24315) `
    'NotMeetRemoveStatus source did not apply its configured status.'
$owner.Hp = 100
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units)
Assert-Hp (-not ($owner.Statuses.ClassId -contains 24315)) `
    'NotMeetRemoveStatus did not remove its configured status after the condition cleared.'

# 24202 skips its start check while an official ActionType 70 forbidden status exists.
$forbiddenSource = $catalog.CreateStatus(24202, 1, 1, 6)
$forbidden = $catalog.CreateStatus(24003, 1, 1, 7)
$owner.Hp = 30
$owner.Statuses.Clear(); $owner.Statuses.Add($forbiddenSource); $owner.Statuses.Add($forbidden)
$invocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $true,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units, $invocations)
Assert-Hp ($invocations.Count -eq 0 -and ($owner.Statuses.ClassId -contains 24202)) `
    'IsCheckExsitForbidden did not suppress the forbidden start check.'
$owner.Statuses.Remove($forbidden)
$invocations.Clear()
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $true,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units, $invocations)
Assert-Hp ($invocations.Count -eq 1 -and $invocations[0].Trigger.EntityId -eq 10170 `
    -and -not ($owner.Statuses.ClassId -contains 24202)) `
    'IsCheckExsitForbidden did not resume the configured skill after the forbidden status ended.'

$cooldown = $catalog.CreateStatus(25065, 1, 1, 5)
$owner.Hp = 1
$owner.Statuses.Clear(); $owner.Statuses.Add($cooldown)
$owner.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 0; Cooldown = 5; LastRound = 0
})
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units)
Assert-Hp ($owner.SkillState.Slots[0].LastRound -eq -2) `
    'HP-limit ChangeSkillCD invoke did not apply its configured delta.'

# Continuous checks stop at the configured count and automatic removal ends the source status.
$repeat = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$repeat.ClassId = 999001; $repeat.InstanceId = 4; $repeat.OwnerId = 1; $repeat.CreatorId = 1
$repeat.DurationRound = -1; $repeat.CurDurationRound = -1; $repeat.HasHpLessThan = $true
$repeat.HpLessThan = 2; $repeat.HpLessThanMaxCount = 2; $repeat.HpLessThanTryAfterInvoke = $true
$repeat.HpLessThanAutoDestroy = $true
$owner.Statuses.Clear(); $owner.Statuses.Add($repeat)
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner, $catalog, $false,
    $null, [Zjcs.Combat.Official.FightRandomStream]::new(17), $null, $units)
Assert-Hp ($repeat.HpLessThanTriggeredCount -eq 1 -and $owner.Statuses.Count -eq 0) `
    'HP-less-than automatic removal or count handling failed.'

Write-Output 'Battle HP-threshold checks passed: official death status, configured skill, HP limit, count and auto-removal.'

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

$cooldown = $catalog.CreateStatus(25065, 1, 1, 5)
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

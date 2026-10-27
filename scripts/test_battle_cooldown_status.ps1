. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Cooldown($condition, $message) {
    if (-not $condition) { throw $message }
}

function Read-Text($path) {
    return [System.IO.File]::ReadAllText((Join-Path $root $path))
}

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

foreach ($statusId in @(24010, 24011, 24407)) {
    Assert-Cooldown ($catalog.StatusStopsSkillCooldown($statusId)) `
        "Official status $statusId did not resolve FightStatusSkillStopCdComponent."
}
Assert-Cooldown (-not $catalog.StatusStopsSkillCooldown(24278)) `
    'A status without the stop-CD component was classified as a cooldown stop.'

$modify = $catalog.CreateStatus(60801, 1, 1, 1)
Assert-Cooldown ($modify.HasSkillCooldownModify -and $modify.SkillCooldownModify -eq -1 `
    -and $modify.SkillCooldownModifyIsRandom `
    -and $modify.SkillCooldownModifyOffset -eq 1 -and $modify.SkillCooldownModifyCount -eq 1) `
    'The official ModifyCD component fields were not loaded.'
Assert-Cooldown ($modify.SkillCooldownModifySkills.Count -eq 1 `
    -and $modify.SkillCooldownModifySkills[0].EntityId -eq 10170) `
    'The official ModifyCD status-trigger skill was not loaded.'
$permanent = $catalog.CreateStatus(24852, 1, 1, 2)
Assert-Cooldown ($permanent.HasPermanentSkillCooldownReduction `
    -and $permanent.PermanentSkillCooldownReduction -eq 1) `
    'The official permanent cooldown reduction was not loaded.'

$cooldownOwner = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$cooldownOwner.Id = 10; $cooldownOwner.Side = 1; $cooldownOwner.Hp = 100
$cooldownOwner.MaxHp = 100; $cooldownOwner.SkillState.Round = 1
$cooldownOwner.SkillState.IsDriving = $true
$cooldownOwner.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 0; Cooldown = 3; LastRound = 1; HasInitializedLastRound = $true
})
$cooldownOwner.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 1; Cooldown = 3; LastRound = 1; HasInitializedLastRound = $true
})
$cooldownOwner.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 2; Cooldown = 3; LastRound = -5; HasInitializedLastRound = $true
})
$applyRandom = [Zjcs.Combat.Official.FightRandomStream]::new(300000)
$appliedModify = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply(
    $cooldownOwner, $modify, 1, 1, $applyRandom)
Assert-Cooldown ($appliedModify.SkillCooldownActionPendingCount -eq 1) `
    'Applying a ModifyCD status did not queue the official CDModify custom action.'
[Zjcs.Combat.Official.FightStatusCooldownRuntime]::ProcessPending(
    $cooldownOwner, $appliedModify, $applyRandom)
Assert-Cooldown ($cooldownOwner.SkillState.Slots[0].LastRound -eq 1 `
    -and $cooldownOwner.SkillState.Slots[1].LastRound -eq 0) `
    'Random ModifyCD did not select the configured deterministic eligible skill slot.'
Assert-Cooldown ($cooldownOwner.SkillState.Slots[2].LastRound -eq -5) `
    'ModifyCD selected a skill outside the configured cooldown window.'
$cooldownUnits = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$cooldownUnits.Add($cooldownOwner)
$modifySkills = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectCooldownModifySkills(
    $cooldownUnits, $applyRandom)
Assert-Cooldown ($modifySkills.Count -eq 1 -and $modifySkills[0].Trigger.EntityId -eq 10170) `
    'The configured status-trigger skill was not queued when ModifyCD was invoked.'

$permanentOwner = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$permanentOwner.Id = 11
$permanentOwner.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 0; Cooldown = 3; LastRound = 1; HasInitializedLastRound = $true
})
$permanentApplied = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply(
    $permanentOwner, $permanent, 0, 2, $applyRandom)
Assert-Cooldown ($permanentOwner.SkillState.Slots[0].Cooldown -eq 2) `
    'The permanent cooldown status did not reduce the configured skill cooldown.'
[void][Zjcs.Combat.Official.FightStatusLifecycle]::Apply(
    $permanentOwner, $permanent, 0, 3, $applyRandom)
Assert-Cooldown ($permanentOwner.SkillState.Slots[0].Cooldown -eq 2) `
    'A discarded duplicate permanent cooldown status applied the reduction twice.'

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1
$player.Hp = 100; $player.MaxHp = 100; $player.MoveDist = 0
$player.Turn.Id = 1; $player.Turn.Hp = 100; $player.Turn.Speed = 100
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 0; Cooldown = 3; LastRound = 2; HasInitializedLastRound = $true
})
$player.Statuses.Add($catalog.CreateStatus(24010, $player.Id, $player.Id, 1))
$player.Statuses.Add($catalog.CreateStatus(24852, $player.Id, $player.Id, 2))

$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2
$enemy.Hp = 100; $enemy.MaxHp = 100; $enemy.MoveDist = 0
$enemy.Turn.Id = 2; $enemy.Turn.Hp = 100; $enemy.Turn.Speed = 1
$enemy.Statuses.Add($catalog.CreateStatus(60801, $enemy.Id, $enemy.Id, 1))

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 4; $checkpoint.RandomSeed = 3
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 4; $checkpoint.Grid.Height = 4; $checkpoint.Grid.DefaultGroundType = 2
$player.X = 0; $player.Y = 0; $enemy.X = 1; $enemy.Y = 0
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add($enemy.Id); $checkpoint.Waves.Add($wave)

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    $schema, [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
$stepResult = $executor.Step(0, 1)

Assert-Cooldown ($player.SkillState.Round -eq 1) 'The test unit did not take its first personal turn.'
Assert-Cooldown ($player.SkillState.Slots[0].LastRound -eq 3) `
    'The active stop-CD status did not advance each skill LastRound at personal-round start.'
Assert-Cooldown ($player.SkillState.Slots[0].Cooldown -eq 2) `
    'Battle-start initialization did not apply the permanent cooldown reduction.'
Assert-Cooldown (@($stepResult.Event.SkillCasts | Where-Object {
    $_.ActorId -eq $enemy.Id -and $_.SkillEntityId -eq 10170
}).Count -eq 1) `
    'The configured ModifyCD status-trigger skill was not executed in the battle step.'
Assert-Cooldown (@($stepResult.Event.StatusCustomActions | Where-Object {
    $_.UnitId -eq $enemy.Id -and $_.Action -eq 3
}).Count -eq 1) `
    'The official CDModify custom action was not included in the battle step event.'
$replayed = $executor.Step(0, 1)
Assert-Cooldown ($replayed.Replayed -and @($replayed.Event.StatusCustomActions | Where-Object {
    $_.UnitId -eq $enemy.Id -and $_.Action -eq 3
}).Count -eq 1) `
    'Replaying a saved battle step changed or duplicated its CDModify custom action.'

Write-Output 'Battle cooldown status checks passed: fixed component fields, random eligible-slot selection, status-trigger skill execution, CDModify event replay, permanent reduction and personal-round stop-CD.'

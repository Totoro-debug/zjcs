$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
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

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1
$player.Hp = 100; $player.MaxHp = 100; $player.MoveDist = 0
$player.Turn.Id = 1; $player.Turn.Hp = 100; $player.Turn.Speed = 100
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 0; Cooldown = 3; LastRound = 2; HasInitializedLastRound = $true
})
$player.Statuses.Add($catalog.CreateStatus(24010, $player.Id, $player.Id, 1))

$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2
$enemy.Hp = 100; $enemy.MaxHp = 100; $enemy.MoveDist = 0
$enemy.Turn.Id = 2; $enemy.Turn.Hp = 100; $enemy.Turn.Speed = 1

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
[void]$executor.Step(0, 1)

Assert-Cooldown ($player.SkillState.Round -eq 1) 'The test unit did not take its first personal turn.'
Assert-Cooldown ($player.SkillState.Slots[0].LastRound -eq 3) `
    'The active stop-CD status did not advance each skill LastRound at personal-round start.'

Write-Output 'Battle cooldown status checks passed: fixed component lookup and personal-round LastRound preservation.'

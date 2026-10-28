. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)

function Assert-DamageTest($condition, [string]$message) { if (-not $condition) { throw $message } }
function New-DamageTestUnit([int]$id, [int]$side) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = $side
    $unit.Hp = 100; $unit.MaxHp = 100; $unit.LastMaxHp = 100
    $unit.Level = 1; $unit.SubRank = 1
    $unit.Turn.Id = $id; $unit.Turn.Hp = 100; $unit.Turn.Speed = 10
    return $unit
}
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.ActionType = 1; $hit.DamageType = 8; $hit.DamageParam = 500
$settings = [Zjcs.Combat.Official.FightDamageTestRules]::new()
$settings.Difficulty = 1; $settings.EnableMonsterOneDamage = $true; $settings.EnableMonsterUnlimitHp = $true
$executor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new($formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $settings)
$player = New-DamageTestUnit 1 1
$monster = New-DamageTestUnit 2 2
$random = [Zjcs.Combat.Official.FightRandomStream]::new(1)
for ($index = 0; $index -lt 2; $index++) {
    $result = $executor.Apply($random, $index + 1, $player, $monster, $null, $hit, 43, $false)
    Assert-DamageTest ($monster.Hp -eq 100 -and $result.Settlement.RealDamageValue -eq -500 -and
        -not $result.Settlement.KilledTarget) 'Infinite HP must retain full reported damage across repeated lethal hits.'
}
$result = $executor.Apply($random, 3, $monster, $player, $null, $hit, 43, $false)
Assert-DamageTest ($player.Hp -eq 99 -and $result.Settlement.DamageValue -eq -1) 'Monster fixed damage must be exactly one.'
$player.Hp = 100
$result = $executor.Apply($random, 4, $monster, $player, $null, $hit, 1, $false)
Assert-DamageTest ($player.Hp -eq 0 -and $result.Settlement.RealDamageValue -eq -100 -and
    $result.Settlement.KilledTarget) 'Damage-test settings must not affect other stage types.'
$settings.EnableMonsterOneDamage = $false; $settings.EnableMonsterUnlimitHp = $false
$player.Hp = 100; $monster.Hp = 100
$result = $executor.Apply($random, 5, $player, $monster, $null, $hit, 43, $false)
Assert-DamageTest ($monster.Hp -eq 0 -and $result.Settlement.KilledTarget) 'Normal HP must retain ordinary lethal settlement.'

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 72000; $checkpoint.StageType = 43; $checkpoint.RandomSeed = 1
$checkpoint.DamageTest = $settings
$player.Hp = 100; $monster.Hp = 100
$checkpoint.Units.Add($player); $checkpoint.Units.Add($monster)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2)
$checkpoint.Waves.Add($wave)
$battle = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new())
$settings.EnableMonsterUnlimitHp = $true
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
Assert-DamageTest ($restored.DamageTest.Difficulty -eq 1 -and $restored.DamageTest.EnableMonsterUnlimitHp -and
    -not $restored.DamageTest.EnableMonsterOneDamage) 'Damage-test settings must survive checkpoint replay.'
Write-Output 'Skill damage-test checks passed: repeated lethal hits, full damage statistics, fixed damage, ordinary stages, disabled options and checkpoint replay.'

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-StatusApp($condition, $message) {
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

# Official damage status list mapping.
$official = $catalog.CreateSkill(12860, 1, 1, 1, 1, 0, $null)
$mapped = $official.HitGraphEntities | Where-Object { $_.EntityId -eq 12861 } | Select-Object -First 1
Assert-StatusApp ($null -ne $mapped -and $mapped.AppliedStatuses.Count -ge 1) 'Official hit did not map its damage status list'
$mappedStatus = $mapped.AppliedStatuses | Where-Object { $_.StatusId -eq 24078 } | Select-Object -First 1
Assert-StatusApp ($null -ne $mappedStatus -and $mappedStatus.TargetType -eq 2 -and [Math]::Abs($mappedStatus.Chance - 1) -lt 0.0001) 'Official damage status entry mismatch'

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1; $player.Hp = 1000; $player.MaxHp = 1000
$player.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $player.Turn.Id = 1; $player.Turn.Hp = 1000; $player.Turn.Speed = 100
$player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2; $enemy.X = 1; $enemy.Y = 0; $enemy.Hp = 5000; $enemy.MaxHp = 5000
$enemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $enemy.Turn.Id = 2; $enemy.Turn.Hp = 5000; $enemy.Turn.Speed = 1

function Invoke-StatusBattle($skill, $player, $enemy) {
    $skill.Slot = 0; $skill.EntityId = 12860; $skill.ItemId = 16000; $skill.TargetType = 1
    $player.Skills.Clear()
    $player.Skills.Add($skill)
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $enemy.Statuses.Clear()
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 9
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 10; $checkpoint.Grid.Height = 10; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2)
    $checkpoint.Waves.Add($wave)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    return [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog).Step(0, 1)
}

# A long-lived status survives the turn-end lifecycle tick after being applied.
$longSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$longSkill.TargetType = 1
$longSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$longHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$longHit.EntityId = 8001; $longHit.DamageId = 8002; $longHit.DamageType = 8
$longHit.HasCustomDamage = $true; $longHit.CustomDamage = 1; $longHit.TargetMode = 0
$longHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 1.0; LessThanHpPer = -1 })
$longSkill.Hits.Add($longHit)
$result = Invoke-StatusBattle $longSkill $player $enemy
Assert-StatusApp (($enemy.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'Damage status was not applied to the target'
Assert-StatusApp (($player.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 0) 'Damage status leaked to the source'

# Probability, HP condition and source targeting.
$chanceSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$chanceSkill.TargetType = 1
$chanceSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$chanceHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$chanceHit.EntityId = 7001; $chanceHit.DamageId = 7002; $chanceHit.DamageType = 8
$chanceHit.HasCustomDamage = $true; $chanceHit.CustomDamage = 1; $chanceHit.TargetMode = 0
$chanceHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 0.0; LessThanHpPer = -1 })
$chanceSkill.Hits.Add($chanceHit)
$chanceResult = Invoke-StatusBattle $chanceSkill $player $enemy
Assert-StatusApp ($enemy.Statuses.Count -eq 0) 'Zero-chance damage status was applied'

$conditionHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$conditionHit.EntityId = 7003; $conditionHit.DamageId = 7004; $conditionHit.DamageType = 8
$conditionHit.HasCustomDamage = $true; $conditionHit.CustomDamage = 1; $conditionHit.TargetMode = 0
$conditionHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 1.0; LessThanHpPer = 0.1 })
$conditionSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$conditionSkill.TargetType = 1
$conditionSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$conditionSkill.Hits.Add($conditionHit)
$conditionResult = Invoke-StatusBattle $conditionSkill $player $enemy
Assert-StatusApp ($enemy.Statuses.Count -eq 0) 'HP-conditioned damage status was applied above its threshold'

$sourceHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$sourceHit.EntityId = 7005; $sourceHit.DamageId = 7006; $sourceHit.DamageType = 8
$sourceHit.HasCustomDamage = $true; $sourceHit.CustomDamage = 1; $sourceHit.TargetMode = 0
$sourceHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 1; Chance = 1.0; LessThanHpPer = -1 })
$sourceSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$sourceSkill.TargetType = 1
$sourceSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$sourceSkill.Hits.Add($sourceHit)
$sourceResult = Invoke-StatusBattle $sourceSkill $player $enemy
Assert-StatusApp (($player.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'Source-targeted damage status was not applied to the skill source'

Write-Output 'Battle damage status checks passed: official damage status mapping, target/source targeting, probability and HP condition.'

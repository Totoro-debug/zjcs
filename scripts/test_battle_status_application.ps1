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

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$effectPropertyNames = @('BaseEffectRate', 'EffectRate', 'BaseEffectDodge', 'EffectDodge',
    'EffectRateValue', 'EffectDodgeValue', 'AbnormalDebuffRateScale', 'DebuffRateScale')
$floatEffectPropertyNames = @('EffectRateValue', 'EffectDodgeValue',
    'AbnormalDebuffRateScale', 'DebuffRateScale')
foreach ($name in $effectPropertyNames) {
    $id = $catalog.PropertyType($name)
    Assert-StatusApp ($id -gt 0) "Official effect-rate property is missing: $name"
    $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name = $name; Id = $id })
    if ($floatEffectPropertyNames -contains $name) { $schema.FloatPropertyIds.Add($id) }
}

function Set-EffectProperty($properties, $name, $value) {
    $id = $catalog.PropertyType($name)
    foreach ($property in $properties) {
        if ($property.Id -eq $id) { $property.Value = $value; return }
    }
    $properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $id; Value = $value })
}

$sourceProps = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$targetProps = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
Set-EffectProperty $sourceProps 'BaseEffectRate' 100
Set-EffectProperty $sourceProps 'EffectRate' 50
Set-EffectProperty $sourceProps 'EffectRateValue' 1000
Set-EffectProperty $sourceProps 'DebuffRateScale' 2500
Set-EffectProperty $sourceProps 'AbnormalDebuffRateScale' 2000
Set-EffectProperty $targetProps 'BaseEffectDodge' 100
Set-EffectProperty $targetProps 'EffectDodge' 50
Set-EffectProperty $targetProps 'EffectDodgeValue' 500
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new($schema,
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
Assert-StatusApp ([Math]::Abs($formula.CalculateStatusEffectRate($sourceProps, $targetProps, 0.2, 2) - 0.3125) -lt 0.000001) 'Official debuff effect-rate formula mismatch'
Set-EffectProperty $targetProps 'BaseEffectDodge' 200
Set-EffectProperty $targetProps 'EffectDodge' 100
Assert-StatusApp ([Math]::Abs($formula.CalculateStatusEffectRate($sourceProps, $targetProps, 0.2, 3) - 0.0375) -lt 0.000001) 'Official abnormal-debuff effect-rate formula mismatch'

# Official damage status list mapping.
$official = $catalog.CreateSkill(12860, 1, 1, 1, 1, 0, $null)
$mapped = $official.HitGraphEntities | Where-Object { $_.EntityId -eq 12861 } | Select-Object -First 1
Assert-StatusApp ($null -ne $mapped -and $mapped.AppliedStatuses.Count -ge 1) 'Official hit did not map its damage status list'
$mappedStatus = $mapped.AppliedStatuses | Where-Object { $_.StatusId -eq 24078 } | Select-Object -First 1
Assert-StatusApp ($null -ne $mappedStatus -and $mappedStatus.TargetType -eq 2 -and [Math]::Abs($mappedStatus.Chance - 1) -lt 0.0001) 'Official damage status entry mismatch'
$overrideSkill = $catalog.CreateSkill(12760, 1, 1, 1, 1, 0, $null)
$overrideHit = $overrideSkill.HitGraphEntities | Where-Object { $_.EntityId -eq 12761 } | Select-Object -First 1
$overrideStatus = $overrideHit.AppliedStatuses | Where-Object { $_.StatusId -eq 24216 } | Select-Object -First 1
Assert-StatusApp ($null -ne $overrideStatus -and $overrideStatus.CharacterTypeRateOverrides.Count -eq 3 -and $catalog.StatusTypeFor(24216) -eq 2) 'Official status rate override or status type was not mapped'
Assert-StatusApp (($overrideStatus.CharacterTypeRateOverrides | Where-Object { $_.CharacterType -eq 2 -and $_.Rate -eq 1 -and -not $_.AffectedByProp }).Count -eq 1) 'Monster status-rate override mismatch'

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
$longHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 1.0; AffectedByProp = $false; LessThanHpPer = -1 })
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
$chanceHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 0.0; AffectedByProp = $false; LessThanHpPer = -1 })
$chanceSkill.Hits.Add($chanceHit)
$chanceResult = Invoke-StatusBattle $chanceSkill $player $enemy
Assert-StatusApp ($enemy.Statuses.Count -eq 0) 'Zero-chance damage status was applied'

$conditionHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$conditionHit.EntityId = 7003; $conditionHit.DamageId = 7004; $conditionHit.DamageType = 8
$conditionHit.HasCustomDamage = $true; $conditionHit.CustomDamage = 1; $conditionHit.TargetMode = 0
$conditionHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 2; Chance = 1.0; AffectedByProp = $false; LessThanHpPer = 0.1 })
$conditionSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$conditionSkill.TargetType = 1
$conditionSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$conditionSkill.Hits.Add($conditionHit)
$conditionResult = Invoke-StatusBattle $conditionSkill $player $enemy
Assert-StatusApp ($enemy.Statuses.Count -eq 0) 'HP-conditioned damage status was applied above its threshold'

$sourceHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$sourceHit.EntityId = 7005; $sourceHit.DamageId = 7006; $sourceHit.DamageType = 8
$sourceHit.HasCustomDamage = $true; $sourceHit.CustomDamage = 1; $sourceHit.TargetMode = 0
$sourceHit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId = 26004; TargetType = 1; Chance = 1.0; AffectedByProp = $false; LessThanHpPer = -1 })
$sourceSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$sourceSkill.TargetType = 1
$sourceSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$sourceSkill.Hits.Add($sourceHit)
$sourceResult = Invoke-StatusBattle $sourceSkill $player $enemy
Assert-StatusApp (($player.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'Source-targeted damage status was not applied to the skill source'

# The property-adjusted rate is used by hit execution, not only by the standalone formula.
Set-EffectProperty $player.Properties 'BaseEffectRate' 100
Set-EffectProperty $player.Properties 'EffectRateValue' 20000
Set-EffectProperty $enemy.Properties 'BaseEffectDodge' 100
Set-EffectProperty $enemy.Properties 'EffectDodge' 0
Set-EffectProperty $player.BaseProperties 'BaseEffectRate' 100
Set-EffectProperty $player.BaseProperties 'EffectRateValue' 20000
Set-EffectProperty $enemy.BaseProperties 'BaseEffectDodge' 100
Set-EffectProperty $enemy.BaseProperties 'EffectDodge' 0
$propertyApplication = [Zjcs.Combat.Official.FightBattleStatusApplication]::new()
$propertyApplication.StatusId = 26004; $propertyApplication.TargetType = 2
$propertyApplication.Chance = 0.1; $propertyApplication.AffectedByProp = $true
$propertySkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$propertySkill.TargetType = 1
$propertySkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$propertyHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$propertyHit.EntityId = 7007; $propertyHit.DamageId = 7008; $propertyHit.DamageType = 8
$propertyHit.HasCustomDamage = $true; $propertyHit.CustomDamage = 1; $propertyHit.TargetMode = 0
$propertyHit.AppliedStatuses.Add($propertyApplication)
$propertySkill.Hits.Add($propertyHit)
$propertyResult = Invoke-StatusBattle $propertySkill $player $enemy
Assert-StatusApp (($enemy.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'Effect-rate properties were not applied to damage status probability'

# Configured overrides replace both the base probability and its property scaling for the matching target type.
$overrideApplication = [Zjcs.Combat.Official.FightBattleStatusApplication]::new()
$overrideApplication.StatusId = 24216; $overrideApplication.TargetType = 2
$overrideApplication.Chance = 0.0; $overrideApplication.AffectedByProp = $true
foreach ($rateOverride in $overrideStatus.CharacterTypeRateOverrides)
    { $overrideApplication.CharacterTypeRateOverrides.Add($rateOverride) }
$overrideTestSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$overrideTestSkill.TargetType = 1
$overrideTestSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$overrideTestHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$overrideTestHit.EntityId = 7010; $overrideTestHit.DamageId = 7011; $overrideTestHit.DamageType = 8
$overrideTestHit.HasCustomDamage = $true; $overrideTestHit.CustomDamage = 1; $overrideTestHit.TargetMode = 0
$overrideTestHit.AppliedStatuses.Add($overrideApplication)
$overrideTestSkill.Hits.Add($overrideTestHit)
$overrideResult = Invoke-StatusBattle $overrideTestSkill $player $enemy
Assert-StatusApp (($enemy.Statuses | Where-Object { $_.ClassId -eq 24216 }).Count -eq 1) 'Target character-type probability override was not applied'

Write-Output 'Battle damage status checks passed: official status mapping, EffectRate formula, character-type overrides, target/source targeting, probability and HP condition.'

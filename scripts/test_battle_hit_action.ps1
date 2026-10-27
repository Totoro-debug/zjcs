. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Action($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Unit($id, $side, $x, $y, $hp, $maxHp) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $maxHp
    $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 100 })
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = 100 - $id
    return $unit
}

function Invoke-ActionBattle($skill, $player, $targets, $targetType) {
    $skill.Slot = 0; $skill.EntityId = 6001; $skill.ItemId = 6002; $skill.TargetType = $targetType
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.RandomSeed = 3
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 10; $checkpoint.Grid.Height = 10; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add($player)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0
    foreach ($unit in $targets) { $checkpoint.Units.Add($unit); $wave.UnitIds.Add($unit.Id) }
    if ($targetType -ne 1) {
        $dummy = New-Unit 99 2 9 9 1000 1000
        $checkpoint.Units.Add($dummy); $wave.UnitIds.Add($dummy.Id)
    }
    $checkpoint.Waves.Add($wave)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    return [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
        $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new()).Step(0, 1)
}

function New-Hit($actionType, $damageType, $customDamage) {
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 6010; $hit.DamageId = 6011; $hit.ActionType = $actionType
    $hit.HasCustomDamage = $true; $hit.CustomDamage = $customDamage
    $hit.DamageType = $damageType; $hit.TargetMode = 0
    $hit.DamageProperty = 1; $hit.FixedDamageProperty = 1
    return $hit
}

# ActionType 5 cures even inside a non-cure skill type.
$curePlayer = New-Unit 1 1 0 0 500 1000
$cureAlly = New-Unit 2 1 1 0 300 1000
$cureSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$cureSkill.SkillType = 0
$cureSkill.Hits.Add((New-Hit 5 1 0))
$cureResult = Invoke-ActionBattle $cureSkill $curePlayer @($cureAlly) 2
$cureEvent = $cureResult.Event.DamageEvents | Select-Object -First 1
Assert-Action ($null -ne $cureEvent -and $cureEvent.IsCure) 'Cure-action hit was not treated as a cure'
Assert-Action ($cureAlly.Hp -ge 300) 'Cure-action hit reduced the target HP'

# ActionType 0 applies no HP change and records no damage event.
$nonePlayer = New-Unit 1 1 0 0 500 1000
$noneTarget = New-Unit 2 2 1 0 800 1000
$noneSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$noneSkill.SkillType = 0
$noneSkill.Hits.Add((New-Hit 0 8 50))
$noneResult = Invoke-ActionBattle $noneSkill $nonePlayer @($noneTarget) 1
Assert-Action ($noneResult.Event.DamageEvents.Count -eq 0) 'None-action hit recorded a damage event'
Assert-Action ($noneTarget.Hp -eq 800) 'None-action hit changed the target HP'
Assert-Action ($noneResult.Event.SkillCasts.Count -eq 1) 'None-action hit prevented the skill cast'

# ActionType 4 keeps the normal damage path.
$damagePlayer = New-Unit 1 1 0 0 500 1000
$damageTarget = New-Unit 2 2 1 0 800 1000
$damageSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$damageSkill.SkillType = 0
$damageSkill.ElementType = 101
$damageSkill.Hits.Add((New-Hit 4 8 30))
$damageResult = Invoke-ActionBattle $damageSkill $damagePlayer @($damageTarget) 1
Assert-Action ($damageResult.Event.DamageEvents.Count -eq 1 -and -not $damageResult.Event.DamageEvents[0].IsCure) 'Damage-action hit was not treated as damage'
Assert-Action ($damageTarget.Hp -eq 770) 'Damage-action hit applied the wrong amount'
Assert-Action ($damageResult.Event.DamageEvents[0].ElementType -eq 101) 'Damage event lost the skill element type'

Write-Output 'Battle hit action checks passed: cure action, no-action hits and normal damage actions.'

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
Add-Type -Path (Get-FightCombatSources)

function Assert-Reduce($condition, $message) {
    if (-not $condition) { throw $message }
}

function Get-Sha($path) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $path))
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

$skillPath = 'unity/Assets/Official/Combat/skill-rules.json'
$battlePath = 'unity/Assets/Official/Combat/battle-rules.json'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [System.IO.File]::ReadAllText((Join-Path $root $skillPath)), (Get-Sha $skillPath), '854_156383',
    [System.IO.File]::ReadAllText((Join-Path $root $battlePath)), (Get-Sha $battlePath), '854_156383')

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$reduceProperty = $catalog.PropertyType('StatusDmgReducePer')
Assert-Reduce ($reduceProperty -gt 0) 'Official StatusDmgReducePer property is missing'
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name = 'StatusDmgReducePer'; Id = $reduceProperty })
$schema.FloatPropertyIds.Add($reduceProperty)
foreach ($name in @('BaseCureAdd', 'BaseBeCureAdd')) {
    $id = $catalog.PropertyType($name)
    if ($id -gt 0) {
        $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name = $name; Id = $id })
        $schema.FloatPropertyIds.Add($id)
    }
}
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    $schema, [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$hitExecutor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new(
    $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())

function New-Unit([int]$id, [int]$side, [long]$hp, [long]$maxHp) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.Hp = $hp; $unit.MaxHp = $maxHp; $unit.Level = 1
    return $unit
}

function Invoke-Damage($source, $target, $skill) {
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 99001; $hit.DamageId = 99002; $hit.DamageType = 9
    $hit.ActionType = 4
    $skill.HasCustomDamage = $true; $skill.CustomDamage = 1000
    return $hitExecutor.Apply([Zjcs.Combat.Official.FightRandomStream]::new(17),
        1, $source, $target, $skill, $hit, 0, $false)
}

function New-Skill([int]$skillType, [int]$targetType) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.SkillType = $skillType; $skill.TargetType = $targetType
    return $skill
}

function Add-StatusType($unit, [int]$id, [int]$statusType) {
    $status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $status.ClassId = $id; $status.StatusType = $statusType
    $unit.Statuses.Add($status)
}

$source = New-Unit 1 1 5000 5000
$target = New-Unit 2 2 5000 5000
$statusTemplate = $catalog.CreateStatus(24201, $target.Id, $source.Id, 10)
Assert-Reduce ($null -ne $statusTemplate.DamageReducePer -and $statusTemplate.DamageReducePer.Damage -and -not $statusTemplate.DamageReducePer.Cure -and [Math]::Abs($statusTemplate.DamageReducePer.NotActiveSkillScale - 1.2) -lt 0.0001) 'Official status 24201 damage-reduction fields were not mapped'
$applied = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target, $statusTemplate, 0, 10)
$result = Invoke-Damage $source $target (New-Skill 0 4)
Assert-Reduce ($result.Formula.Damage -eq -500 -and $applied.DamageReducePer.TriggeredCount -eq 1) 'Status 24201 did not halve active-skill damage'

$target = New-Unit 3 2 5000 5000
$stackReducer = $catalog.CreateStatus(24033, $target.Id, $source.Id, 11)
$appliedStackReducer = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target, $stackReducer, 0, 11)
$sourceStack = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$sourceStack.ClassId = 99010; $sourceStack.StatusType = 7
$sourceStack.ActionType = 64; $sourceStack.StackCount = 3
$source.Statuses.Add($sourceStack)
$stackResult = Invoke-Damage $source $target (New-Skill 0 1)
Assert-Reduce ($stackResult.Formula.Damage -eq -384 -and $appliedStackReducer.DamageReducePer.TriggeredCount -eq 1) 'Status 24033 did not scale reduction from source status stacks'

$target = New-Unit 4 2 5000 5000
$limited = $catalog.CreateStatus(24201, $target.Id, $source.Id, 12)
$appliedLimited = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target, $limited, 0, 12)
$appliedLimited.DamageReducePer.ConditionCount = 2
$appliedLimited.DamageReducePer.MaxCount = 1
$first = Invoke-Damage $source $target (New-Skill 0 4)
$second = Invoke-Damage $source $target (New-Skill 0 4)
$third = Invoke-Damage $source $target (New-Skill 0 4)
Assert-Reduce ($first.Formula.Damage -eq -1000 -and $second.Formula.Damage -eq -500 -and $third.Formula.Damage -eq -1000 -and $appliedLimited.DamageReducePer.TriggeredCount -eq 1) 'Status damage reduction condition/max-count handling mismatch'

$target = New-Unit 7 2 5000 5000
$source.Statuses.Clear()
Add-StatusType $source 99020 2
Add-StatusType $source 99021 3
$sourceFiltered = $catalog.CreateStatus(25048, $target.Id, $source.Id, 14)
$sourceFilteredApplied = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target,
    $sourceFiltered, 0, 14)
$sourceFilteredResult = Invoke-Damage $source $target (New-Skill 0 1)
Assert-Reduce ($sourceFilteredResult.Formula.Damage -eq -500 -and $sourceFilteredApplied.DamageReducePer.StatusType.Target -eq 1) 'Status 25048 source-side status filter or count cap mismatch'

$target = New-Unit 8 2 5000 5000
$targetFiltered = $catalog.CreateStatus(25273, $target.Id, $source.Id, 15)
$targetFilteredApplied = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target,
    $targetFiltered, 0, 15)
$targetFilteredApplied.StatusType = 99
Add-StatusType $target 99022 2
Add-StatusType $target 99023 3
$targetFilteredResult = Invoke-Damage $source $target (New-Skill 0 1)
Assert-Reduce ($targetFilteredResult.Formula.Damage -eq -333 -and $targetFilteredApplied.DamageReducePer.StatusType.Target -eq 2) 'Status 25273 target-side status filter or count scaling mismatch'

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.RandomSeed = 23
foreach ($unit in @($source, $target)) {
    $unit.Turn.Id = $unit.Id
    $checkpoint.Units.Add($unit)
}
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
$restoredStatus = $restored.Units[1].Statuses | Where-Object { $_.ClassId -eq 25273 } | Select-Object -First 1
Assert-Reduce ($null -ne $restoredStatus -and $restoredStatus.DamageReducePer.TriggeredCount -eq 1 -and $restoredStatus.DamageReducePer.StatusType.AnyStatusTypes.Count -eq 2) 'Status damage-reduction config/count did not survive checkpoint persistence'

$target = New-Unit 5 1 1000 5000
$cureStatus = $catalog.CreateStatus(24201, $target.Id, $target.Id, 13)
$cureApplied = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target, $cureStatus, 0, 13)
$cureApplied.DamageReducePer.Cure = $true
$healer = New-Unit 6 1 5000 5000
$healer.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
    Id = $catalog.PropertyType('BaseCureAdd'); Value = 10000
})
$target.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
    Id = $catalog.PropertyType('BaseBeCureAdd'); Value = 10000
})
$healSkill = New-Skill 3 4
$healSkill.HasCustomCure = $true; $healSkill.CustomCure = 1000
$healHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$healHit.EntityId = 99003; $healHit.DamageId = 99004; $healHit.ActionType = 5
$healResult = $hitExecutor.Apply([Zjcs.Combat.Official.FightRandomStream]::new(19),
    2, $healer, $target, $healSkill, $healHit, 0, $false)
Assert-Reduce ($healResult.Cure.Cure -eq 454 -and $target.Hp -eq 1454) 'Configured damage reduction did not scale a cure event'

Write-Host 'Battle damage-reduction status tests passed.'

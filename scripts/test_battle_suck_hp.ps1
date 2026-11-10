. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$referenceText = & python -B (Join-Path $PSScriptRoot 'prepare_battle_suck_hp_sources.py') --harness
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract the selected official absorption formulas.' }
$reference = $referenceText | ConvertFrom-Json
Add-Type -TypeDefinition $reference.code
Add-Type -Path (Get-FightCombatSources)
$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
foreach ($entry in $reference.names.psobject.Properties) {
    $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Id = [int]$entry.Name; Name = $entry.Value })
}
foreach ($id in $reference.floats) { $schema.FloatPropertyIds.Add($id) }
$config = [Zjcs.Combat.Official.FightDamageFormulaConfig]::new()
$config.CureAddSoftCapThreshold = $reference.threshold
$config.CureAddOverflowScale = $reference.overflow
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new($schema, $config)
$original = [PinnedSuckHp.Handler]::new()
function Props($value, $enum) {
    if ($enum) { $result = [Collections.Generic.Dictionary[PinnedSuckHp.PropType,long]]::new() }
    else { $result = [Collections.Generic.Dictionary[int,long]]::new() }
    foreach ($entry in $value.psobject.Properties) {
        if ($enum) { $result[[PinnedSuckHp.PropType][int]$entry.Name] = [long]$entry.Value }
        else { $result[[int]$entry.Name] = [long]$entry.Value }
    }
    return ,$result
}
$count = 0
foreach ($case in $reference.cases) {
    $request = [Zjcs.Combat.Official.FightSuckHpFormulaRequest]::new()
    $request.SourceProps = Props $case.source $false
    $request.SkillProps = Props $case.skill $false
    $request.DamageSkillProps = Props $case.damaging $false
    $request.SourceType = $case.kind; $request.SourceMasterType = $case.master
    $request.Damage = $case.damage
    $request.SuckPropType = $reference.ids.SuckHpByDamage
    $request.FixedSuckPropType = $reference.ids.FixedSuckHp
    $request.DamageHitPropType = $reference.ids.SkillAttack1
    $info = [PinnedSuckHp.BattleFormula+SuckHpInfo]::new()
    $info.SourceProp = Props $case.source $true
    $info.SkillProps = Props $case.skill $true
    $info.DamageSkillProps = Props $case.damaging $true
    $info.SourceType = $case.kind; $info.SourceMasterType = $case.master
    $info.Damage = $case.damage
    $info.SuckPropType = $request.SuckPropType
    $info.FixedSuckPropType = $request.FixedSuckPropType
    $info.DamageHitPropType = $request.DamageHitPropType
    $expected = $original.SuckHp($info)
    $actual = $formula.CalculateSuckHp($request)
    if ($actual -ne $expected) { throw "SuckHp $($case.name): expected $expected, got $actual" }
    $expected = $original.SuckHpByOnceHit($case.damage, $info.SourceProp, $info.SuckPropType)
    $actual = $formula.CalculateSuckHpByOnceHit($case.damage, $request.SourceProps, $request.SuckPropType)
    if ($actual -ne $expected) { throw "SuckHpByOnceHit $($case.name): expected $expected, got $actual" }
    $count += 2
}
Write-Output "Original absorption formula comparisons passed: $count, including source/master identity, distinct skill props, reduction floors, cure soft cap, signs, truncation and zero bases."

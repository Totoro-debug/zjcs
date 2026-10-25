$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{
    Name = 'BreakResilience'; Id = 113
})
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{
    Name = 'BreakResiliencePercent'; Id = 73
})
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{
    Name = 'Attack'; Id = 1
})
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{
    Name = 'MaxHp'; Id = 3
})
$schema.FloatPropertyIds.Add(73)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    $schema, [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$properties = [System.Collections.Generic.Dictionary[int,long]]::new()
$properties[113] = 2
$properties[73] = 5000

if ([Zjcs.Combat.Official.FightResilienceFormula]::CalculateChange(
        $formula, $properties, 1.0, 20000) -ne -6) {
    throw 'Official resilience break formula did not scale the source props and skill factor.'
}
if ([Zjcs.Combat.Official.FightResilienceFormula]::CalculateChange(
        $formula, $properties, 0.5, 50000) -ne -7) {
    throw 'Official resilience break formula did not truncate the calculated decrement.'
}
if ([Zjcs.Combat.Official.FightResilienceFormula]::CalculateChange(
        $formula, $properties, 0.0, 10000) -ne -1) {
    throw 'Official resilience break formula did not enforce the minimum decrement.'
}

$targetProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$targetProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 3; Value = 1000 })
$breakerProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$breakerProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 100 })
if ([Zjcs.Combat.Official.FightResilienceFormula]::CalculateBrokenDamage(
        $formula, $targetProperties, $breakerProperties, 500, 10000) -ne -50) {
    throw 'Official resilience broken-damage formula did not use max HP percent.'
}
$breakerProperties[0].Value = 20
if ([Zjcs.Combat.Official.FightResilienceFormula]::CalculateBrokenDamage(
        $formula, $targetProperties, $breakerProperties, 500, 10000) -ne -20) {
    throw 'Official resilience broken-damage formula did not enforce the breaker attack cap.'
}

Write-Output 'Resilience formula checks passed: source modifiers, 1/10000 skill factor, truncation, minimum decrement, max-HP damage, and breaker attack cap.'

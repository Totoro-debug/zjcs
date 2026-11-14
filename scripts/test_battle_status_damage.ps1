. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$referenceText = & python -B (Join-Path $PSScriptRoot 'prepare_battle_status_damage_sources.py') --harness
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract the pinned original status damage formula.' }
$reference = $referenceText | ConvertFrom-Json
Add-Type -TypeDefinition $reference.code
Add-Type -Path (Get-FightCombatSources)
$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
foreach ($entry in $reference.names.psobject.Properties) {
    $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{Id=[int]$entry.Name; Name=$entry.Value})
}
foreach ($id in $reference.floats) { $schema.FloatPropertyIds.Add($id) }
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new($schema, [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$original = [PinnedStatusDamage.Handler]::new()
$rateId = [int]$reference.ids.DamageByDamage
$fixedId = [int]$reference.ids.FixedDamageByDamage
$count = 0
foreach ($rate in @(-15000,0,3333,10000,17500)) {
    foreach ($fixed in @(-13,0,7,55)) {
        $props = [Collections.Generic.Dictionary[int,long]]::new()
        $pinned = [Collections.Generic.Dictionary[PinnedStatusDamage.PropType,long]]::new()
        $props[$rateId]=$rate; $props[$fixedId]=$fixed
        $pinned[[PinnedStatusDamage.PropType]$rateId]=$rate; $pinned[[PinnedStatusDamage.PropType]$fixedId]=$fixed
        foreach ($value in @(-1001,-3,0,3,1001,[long]::MaxValue)) {
            foreach ($cap in @(-1,0,1,97)) {
                $expected=$original.StatusDamage($pinned,$rateId,$fixedId,$value,$cap)
                $actual=$formula.CalculateStatusDamage($props,$rateId,$fixedId,$value,$cap)
                if ($actual -ne $expected) { throw "StatusDamage rate=$rate fixed=$fixed value=$value cap=$cap expected=$expected actual=$actual" }
                $count++
            }
        }
    }
}
foreach ($props in @($null,[Collections.Generic.Dictionary[int,long]]::new())) {
    if ($formula.CalculateStatusDamage($props,$rateId,$fixedId,-100) -ne 0) { throw 'Missing properties must preserve the original zero result.' }
}
try { $formula.CalculateStatusDamage($null,$rateId,$fixedId,[long]::MinValue); throw 'Expected original absolute-value overflow.' }
catch [System.OverflowException] { }
Write-Output "Original status-damage comparisons passed: $count; signs, percentages, fixed values, cap=0, positive caps, truncation, zero floor, missing properties and original overflow."

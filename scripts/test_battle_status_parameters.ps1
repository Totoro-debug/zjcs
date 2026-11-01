. (Join-Path $PSScriptRoot 'test_battle_status_replay.ps1')

function Parameter-Values($raw, $floating, $count = 2) {
    $values = [System.Collections.Generic.List[Zjcs.Combat.Official.FightStatusReplacementValue]]::new()
    $values.Add([Zjcs.Combat.Official.FightStatusReplacementValue]@{ PropertyId=5001; RawValue=$raw; IsFloat=$floating })
    $values.Add([Zjcs.Combat.Official.FightStatusReplacementValue]@{ PropertyId=5002; RawValue=$count; IsFloat=$false })
    return ,$values
}
function Parameter-Status($id, $values) {
    return $catalog.CreateStatus($id, 1, 1, 0, 0, 0, 1, 1, 0, $null, 0, $values)
}
function Parameter-Mapping($name, $id) {
    $mapping = [System.Collections.Generic.Dictionary[string,object]]::new()
    $mapping['PropertyName'] = $name; $mapping['SpecialParamProp'] = $id
    return $mapping
}

$values = Parameter-Values 6000 $true
$round = Parameter-Status 60170 $values
Assert-Replay ([math]::Abs($round.RoundStartRate - 0.6) -lt 0.000001 -and $round.RoundStartRulesSupported) 'Original equipment round-start probability was ignored or disabled.'
$limited = Parameter-Status 60470 $values
Assert-Replay ([math]::Abs($limited.RoundStartRate - 0.6) -lt 0.000001 -and $limited.RoundStartMaxCount -eq 2) 'Original equipment round-start probability/count replacements were lost.'
Assert-Replay ($catalog.CreateStatus(60170, 1, 1).RoundStartRate -eq 0) 'One status instance mutated the shared official defaults.'
Assert-Replay ([math]::Abs((Parameter-Status 60170 (Parameter-Values 2500 $true)).RoundStartRate - 0.25) -lt 0.000001) 'Independent instances shared replacement parameters.'

# Run the existing round-start collector with a real configured equipment status.
$owner = New-Unit 1 1 0 0 1000 100
$owner.Statuses.Add((Parameter-Status 60170 (Parameter-Values 10000 $true)))
$units = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($owner)
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(23)
$triggers = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartSkills($owner, $owner, $units, $rng)
Assert-Replay ($triggers.Count -eq 1 -and $triggers[0].EntityId -eq 60171) 'A replaced round-start probability did not produce the original child skill.'
$owner.Statuses.Clear(); $owner.Statuses.Add((Parameter-Status 60170 (Parameter-Values 0 $true)))
Assert-Replay ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartSkills($owner, $owner, $units, $rng).Count -eq 0) 'A zero replacement probability still triggered the child skill.'

# BasePercent is not an accessor of the original round-end component. Preserve
# this mapping for the unsupported-rule guard rather than silently dropping it.
Assert-Replay (-not (Parameter-Status 60531 $values).RoundEndRulesSupported) 'An unimplemented original mapping was silently accepted.'

# Controlled configuration fixtures exercise original overload boundary cases and
# duplicate lookup behavior through CreateStatus. Restore the real data afterward.
$required = $catalog.GetType().GetMethod('RequiredEntity', [Reflection.BindingFlags]'Instance,NonPublic')
$components = $required.Invoke($catalog, [object[]]@([int]70144))
$component = $components['FightStatusHpAccDecreaseComponent']
$savedMappings = $component['StatusReplacePropertyValues']
try {
    $mappings = [System.Collections.Generic.List[object]]::new()
    $mappings.Add((Parameter-Mapping 'UnitHp' 5001))
    $mappings.Add((Parameter-Mapping 'MaxCount' 5002))
    $component['StatusReplacePropertyValues'] = $mappings
    $longValue = Parameter-Status 70144 (Parameter-Values 4294967297 $false 4294967297)
    Assert-Replay ($longValue.HpAccUnitHp -eq 4294967297 -and $longValue.HpAccMaxCount -eq 1) 'Original long preservation / unchecked int narrowing changed.'
    $floatValue = Parameter-Status 70144 (Parameter-Values 19999 $true)
    Assert-Replay ($floatValue.HpAccUnitHp -eq 1) 'Original float-to-long overload did not truncate through int.'
} finally { $component['StatusReplacePropertyValues'] = $savedMappings }

$components = $required.Invoke($catalog, [object[]]@([int]60170))
$component = $components['FightStatusRoundStartComponent']
$savedMappings = $component['StatusReplacePropertyValues']
try {
    $mappings = [System.Collections.Generic.List[object]]::new()
    $mappings.Add((Parameter-Mapping 'Rate' 5999))
    $mappings.Add((Parameter-Mapping 'Rate' 5001))
    $component['StatusReplacePropertyValues'] = $mappings
    Assert-Replay ((Parameter-Status 60170 $values).RoundStartRate -eq 0) 'A later duplicate mapping bypassed the original first missing-property lookup.'
    $mappings.RemoveAt(0)
    Assert-Replay ((Parameter-Status 60170 (Parameter-Values 4294967297 $false)).RoundStartRate -eq 1) 'Original Long-to-float overload omitted int narrowing.'
} finally { $component['StatusReplacePropertyValues'] = $savedMappings }
Write-Output 'Status parameter checks passed: real round-start child trigger, independent defaults, unknown-field guard, original overload boundaries and first-match lookup.'

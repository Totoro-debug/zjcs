. (Join-Path $PSScriptRoot 'test_battle_status_parameters.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')

Invoke-ZjcsValidation -Name 'skills-status-property-rules' -ScriptBlock {
    param($workspace)
    & D:/miniconda3/envs/comm/python.exe -B (Join-Path $PSScriptRoot 'prepare_battle_status_property_reference.py') --output $workspace
    if ($LASTEXITCODE -ne 0) { throw 'Original status property reference preparation failed.' }
    Add-Type -Path (Join-Path $workspace 'status-property-reference.cs')
    $rulesPath = 'unity/Assets/Official/Combat/rules.json'
    $formula = $catalog.CreateDamageFormula((Read-Text $rulesPath), (Get-Sha $rulesPath), '854_156383')
    $rules = Read-Text $rulesPath | ConvertFrom-Json
    [SourceStatusPropertyReference.ReferenceState]::Configure(
        [int[]]@($rules.prop_cfg.PropType), [int[]]@($rules.prop_cfg | Where-Object Float | ForEach-Object PropType))

    function Property-Map($values) {
        $result = [System.Collections.Generic.Dictionary[int,long]]::new()
        if ($values -is [System.Collections.IDictionary]) {
            foreach ($key in $values.Keys) { $result[[int]$key] = [long]$values[$key] }
        } else { foreach ($value in $values) { $result[$value.Id] = $value.Value } }
        return ,$result
    }
    function Property-Values($values) {
        $result = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
        foreach ($key in $values.Keys) {
            $result.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=[int]$key; Value=[long]$values[$key] })
        }
        return ,$result
    }
    function Compare-Properties($actual, $expected, $label) {
        $map = Property-Map $actual
        Assert-Replay ($map.Count -eq $expected.Count) "$label property count differs: actual=$($map.Count), expected=$($expected.Count)"
        foreach ($key in $expected.Keys) {
            Assert-Replay ($map.ContainsKey($key) -and $map[$key] -eq $expected[$key]) "$label property $key differs: actual=$($map[$key]), expected=$($expected[$key])"
        }
    }
    function Check-PropertyState($state, $reference, $label) {
        [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($state.Units, $catalog, $formula)
        $status = $state.Units[1].Statuses[0]
        Compare-Properties $status.StatusConfigProperties $reference.Config "$label/config"
        Compare-Properties $status.StatusProperties $reference.Status "$label/status"
        if ($status.HasPropertyComponent -or $status.ShieldKind -ne 0 -or $status.HasCreatorDamageBoost) {
            Compare-Properties $status.ResolvedProperties $reference.Final "$label/projected"
        }
    }
    function Check-StatusProperties($id, $replacementMap = @{}, $blessMap = @{}, $stackCounts = @(), $customize = $null) {
        $source = Property-Map @{ 1=321; 2=173; 3=10000; 4=103; 26=155; 28=80 }
        $target = Property-Map @{ 1=77; 2=99; 3=10000; 4=50 }
        $replacements = [System.Collections.Generic.List[Zjcs.Combat.Official.FightStatusReplacementValue]]::new()
        foreach ($key in $replacementMap.Keys) {
            $replacements.Add([Zjcs.Combat.Official.FightStatusReplacementValue]@{
                PropertyId=[int]$key; RawValue=[long]$replacementMap[$key]; IsFloat=$true })
        }
        $blessings = [System.Collections.Generic.List[Zjcs.Combat.Official.FightStatusPropertyScale]]::new()
        foreach ($key in $blessMap.Keys) {
            $blessings.Add([Zjcs.Combat.Official.FightStatusPropertyScale]@{ EntityId=$id; PropertyId=[int]$key; Factor=$blessMap[$key] })
        }
        $blessings.Add([Zjcs.Combat.Official.FightStatusPropertyScale]@{ EntityId=$id+1; PropertyId=1; Factor=99 })
        $template = $catalog.CreateStatus($id, 2, 1, 0, 0, 0, 1, 1, 1, (Property-Values $source), 0, $replacements, $blessings)
        if ($customize) { & $customize $template $source $target }
        $state = [Zjcs.Combat.Official.FightBattleCheckpoint]@{ StageId=1; RandomSeed=7 }
        $creator = New-Unit 1 1 0 0 10000 100
        $owner = New-Unit 2 2 1 0 10000 50
        $creator.Properties = Property-Values $source; $owner.Properties = Property-Values $target
        $state.Units.Add($creator); $state.Units.Add($owner)
        $status = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $template, 0, 1)
        Assert-Replay ($null -eq $status.ResolvedProperties) 'A new application turned the unresolved cache into an empty resolved list.'
        if ($replacements.Count -gt 0) {
            $saved = $replacements[0].RawValue; $replacements[0].RawValue = -999
            Assert-Replay ($template.ReplacementProperties[0].RawValue -eq $saved -and $status.ReplacementProperties[0].RawValue -eq $saved) 'Replacement values were shared between caller, template and applied status.'
            $replacements[0].RawValue = $saved
        }
        $transitions = [System.Collections.Generic.List[int[]]]::new()
        foreach ($t in $status.PropertyTransitions) {
            $transitions.Add([int[]]@($t.PropertyId, $t.TargetType, $t.TargetPropertyId, $t.AdditionPropertyId, $t.FixedAdditionPropertyId))
        }
        $bless = [System.Collections.Generic.Dictionary[int,double]]::new()
        foreach ($key in $blessMap.Keys) { $bless[[int]$key] = $blessMap[$key] }
        $reference = [SourceStatusPropertyReference.ReferenceState]::Create($id,
            $catalog.HasEntityPropertyDefinition($id), $status.HasPropertyComponent,
            $status.IsOpenStack, $status.ScalePropWhenChangeStacked, $status.StackCount, $owner.Hp,
            (Property-Map $catalog.CalculateSkillProps($id, 1, 1, 1)), $source, $target,
            (Property-Map $replacementMap), $bless, $transitions.ToArray())
        Check-PropertyState $state $reference "status $id initial"
        foreach ($count in $stackCounts) {
            [void][Zjcs.Combat.Official.FightStatusLifecycle]::AddStack($status, $count - $status.StackCount)
            $reference.SetStackCount($status.StackCount)
            Check-PropertyState $state $reference "status $id stack $count"
            $json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state)
            $state = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
            Assert-Replay ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state) -eq $json) 'Initialized status properties did not round-trip exactly.'
            $status = $state.Units[1].Statuses[0]
            Check-PropertyState $state $reference "status $id restored $count"
        }
        return $state
    }

    # Use real configured statuses, including transitions and distinct shield forms.
    foreach ($id in @(12062, 12142, 12202, 60320, 60330, 60340, 60350, 60360, 60031, 60141, 60191, 24118, 60541, 70031, 24022, 24023)) {
        $null = Check-StatusProperties $id
    }
    $null = Check-StatusProperties 60320 @{ 2000=3333; 2010=17 } @{ 2000=1.5; 2010=1.25; 9999=4 }
    $null = Check-StatusProperties 60541 @{ 1=17 } @{ 1=1.5 } @(2, 4, 0, 3)

    # Controlled boundaries alter only the per-instance state; official config is unchanged.
    $scaled = Check-StatusProperties 12062 @{ 1=17 } @{ 1=1.5 } @(3, 0, 2) {
        param($s) $s.IsOpenStack=$true; $s.ScalePropWhenChangeStacked=$true; $s.MaxStackedCount=-1
    }
    Assert-Replay ((Property-Map $scaled.Units[1].Properties)[1] -eq 145) 'A projected stack change did not reach the live unit attack property.'
    $disabled = $scaled.Units[1].Statuses[0]
    [void][Zjcs.Combat.Official.FightStatusLifecycle]::SetForbidden($scaled.Units[1], $disabled, 2, $true)
    [void][Zjcs.Combat.Official.FightStatusLifecycle]::SetForbidden($scaled.Units[1], $disabled, 4, $true)
    [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($scaled.Units, $catalog, $formula)
    Assert-Replay ((Property-Map $scaled.Units[1].Properties)[1] -eq 77) 'Forbidden property state still contributed attack.'
    [void][Zjcs.Combat.Official.FightStatusLifecycle]::AddStack($disabled, 1)
    [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($scaled.Units, $catalog, $formula)
    Assert-Replay ($disabled.StackCount -eq 3 -and (Property-Map $disabled.ResolvedProperties)[1] -eq 153 -and (Property-Map $scaled.Units[1].Properties)[1] -eq 77) 'Disabled stacking lost its original two-stage projection or resumed its live contribution.'
    $savedDisabled = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($scaled)
    $scaled = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($savedDisabled)
    $disabled = $scaled.Units[1].Statuses[0]
    [void][Zjcs.Combat.Official.FightStatusLifecycle]::SetForbidden($scaled.Units[1], $disabled, 2, $false)
    [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($scaled.Units, $catalog, $formula)
    Assert-Replay ((Property-Map $scaled.Units[1].Properties)[1] -eq 77) 'Partial release after reload restored a still-forbidden contribution.'
    [void][Zjcs.Combat.Official.FightStatusLifecycle]::SetForbidden($scaled.Units[1], $disabled, 4, $false)
    [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($scaled.Units, $catalog, $formula)
    Assert-Replay ((Property-Map $scaled.Units[1].Properties)[1] -eq 230 -and $disabled.StackCount -eq 3) 'Full release did not retain and restore updated layers after reload.'
    $null = Check-StatusProperties 12062 @{ 1=17 } @{ 1=1.5 } @(2, 0, 3) {
        param($s) $s.IsOpenStack=$true; $s.ScalePropWhenChangeStacked=$true; $s.StackCount=0; $s.MaxStackedCount=-1
    }
    $null = Check-StatusProperties 12062 @{ 1=0 } @{ 1=2 } @(0, 2) {
        param($s) $s.IsOpenStack=$true; $s.ScalePropWhenChangeStacked=$true; $s.MaxStackedCount=-1
    }
    $null = Check-StatusProperties 60320 @{ 2000=2500; 2010=3 } @{} @() {
        param($s, $source)
        $source[$formula.PropertyId('StatusShieldAddPercent')]=5000
        $source[$formula.PropertyId('StatusFixedShieldAdd')]=7
        $source[$formula.PropertyId('FinalShieldScale')]=2500
        $s.SourceProperties=Property-Values $source
    }
    # Recover the missing initialization fields of an old checkpoint, including its
    # formerly empty projection, and continue using the original initialized values.
    $state = Check-StatusProperties 60541 @{ 1=17 } @{ 1=1.5 } @(2)
    $status = $state.Units[1].Statuses[0]
    $expected = Property-Map $status.ResolvedProperties
    $status.StatusConfigProperties=$null; $status.StatusProperties=$null
    $status.ResolvedProperties.Clear()
    [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($state.Units, $catalog, $formula)
    Compare-Properties $status.ResolvedProperties $expected 'legacy checkpoint recovery'
    Write-Output 'Status property checks passed: 16 configured states, original two-stage projection, raw replacements, blessing/stack order, zero stacks, transitions, shield factors, lifecycle copies and checkpoint continuation.'
}

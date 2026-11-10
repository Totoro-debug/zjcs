. (Join-Path $PSScriptRoot 'test_battle_hit_delay.ps1')

$payloads = @{}
foreach ($name in @('skill-rules', 'battle-rules', 'monster-hp-plies', 'rules')) {
    $payloads[$name] = @{
        Json = [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))
        Source = Get-Content (Join-Path $root "unity/Assets/Official/Combat/$name-source-manifest.json") -Raw | ConvertFrom-Json
    }
}
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $payloads['skill-rules'].Json, $payloads['skill-rules'].Source.output_sha256, $payloads['skill-rules'].Source.manifest,
    $payloads['battle-rules'].Json, $payloads['battle-rules'].Source.output_sha256, $payloads['battle-rules'].Source.manifest,
    $payloads['monster-hp-plies'].Json, $payloads['monster-hp-plies'].Source.output_sha256, $payloads['monster-hp-plies'].Source.manifest)
$formula = $catalog.CreateDamageFormula($payloads['rules'].Json, $payloads['rules'].Source.output_sha256, $payloads['rules'].Source.manifest)
$centre = [Zjcs.Combat.Official.FightGridPoint]::new(3, 0)
$source = New-Unit 1 1 0 0 5000

foreach ($skillId in @(12030, 13390, 14000)) {
    $skill = $catalog.CreateSkill($skillId, 0, 0, 1, 1, 1, $null)
    $hit = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
    $baseOffset = $hit.Trajectory.OffsetX
    $data = $null
    Assert-Delay ($catalog.TryGetComponent($skillId, 'FightHitExtDelayInfoOverriderByStatusStackComponent', [ref]$data)) 'Original override component is unavailable'
    for ($stack = 1; $stack -le 10; $stack++) {
        $cast = [Zjcs.Combat.Official.FightBattleSkillCastPresentation]::new()
        $cast.SourceSharedStacks.Add([Zjcs.Combat.Official.FightBattleSkillCastPresentation+SourceSharedStack]@{ShareId=24029;StackCount=$stack})
        $flight = [Zjcs.Combat.Official.FightBattleHitTiming]::ResolveOverride($catalog, $skill, $source, $cast, $hit)
        $offset = $data['Offsets'][$stack - 1]
        $expected = $hit.Delay + [Math]::Sqrt([Math]::Pow(3 - [single]$offset[0], 2) + [Math]::Pow([single]$offset[1], 2)) * $hit.DelayDistFactor
        $actual = [Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($hit, $cast, $centre, $flight)
        Assert-Delay ($flight.OverrideIndex -eq $stack - 1 -and [Math]::Abs($actual - $expected) -lt 0.00001) "Original $skillId stack $stack flight mismatch"
        Assert-Delay ($hit.Trajectory.OffsetX -eq $baseOffset) 'A cast override mutated its shared base definition'
    }
}

# Shared totals take precedence over the first individual status, even at zero.
$cast.SourceSharedStacks[0].StackCount = 2
$cast.SourceStatuses.Add([Zjcs.Combat.Official.FightBattleSkillCastPresentation+SourceStatus]@{ClassId=24029;StackCount=5})
$flight = [Zjcs.Combat.Official.FightBattleHitTiming]::ResolveOverride($catalog, $skill, $source, $cast, $hit)
Assert-Delay ($flight.OverrideIndex -eq 1) 'Shared stacks lost priority over an individual status'
$cast.SourceSharedStacks[0].StackCount = 0
Assert-Delay ($null -eq [Zjcs.Combat.Official.FightBattleHitTiming]::ResolveOverride($catalog, $skill, $source, $cast, $hit)) 'Zero shared total fell through to an individual status'
$cast.SourceSharedStacks.Clear()
$cast.SourceStatuses.Add([Zjcs.Combat.Official.FightBattleSkillCastPresentation+SourceStatus]@{ClassId=24029;StackCount=9})
$flight = [Zjcs.Combat.Official.FightBattleHitTiming]::ResolveOverride($catalog, $skill, $source, $cast, $hit)
Assert-Delay ($flight.OverrideIndex -eq 4) 'Duplicate status classes lost their first-applied order'
$frozenDelay = [Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($hit, $cast, $centre, $flight)
$cast.SourceStatuses[0].StackCount = 12
$outOfRange = [Zjcs.Combat.Official.FightBattleHitTiming]::ResolveOverride($catalog, $skill, $source, $cast, $hit)
Assert-Delay ($outOfRange.OverrideIndex -eq 11 -and [Math]::Abs([Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($hit, $cast, $centre, $outOfRange) - [Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($hit, $cast, $centre)) -lt 0.00001) 'Out-of-range list entries did not preserve original base parameters'
$hit.Trajectory.OffsetX = 100
Assert-Delay ([Math]::Abs([Zjcs.Combat.Official.FightBattleHitTiming]::EffectDelay($hit, $cast, $centre, $flight) - $frozenDelay) -lt 0.00001) 'Resolved flight changed with later status/base mutations'
$hit.Trajectory.OffsetX = $baseOffset
$source.RoleEntityId = 999999
Assert-Delay ($null -eq [Zjcs.Combat.Official.FightBattleHitTiming]::ResolveOverride($catalog, $skill, $source, $cast, $hit)) 'Missing source status/skill agents were ignored'
$source.RoleEntityId = 0

# The production executor must use the same resolved parameters in damage and receipts.
$skill = $catalog.CreateSkill(12030, 0, 0, 1, 1, 1, $null)
$source = New-Unit 1 1 0 0 5000
$source.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=3;Value=5000})
$source.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=4;Value=99})
$source.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{InstanceId=1;ClassId=24385;OwnerId=1;CreatorId=1;StackCount=2;IsOpenStack=$true;MaxStackedCount=10;DurationRound=-1;SharedStackId=24029;SharedStackLimit=10})
$source.Skills.Add($skill)
$source.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$enemy = New-Unit 2 2 3 0 5000
$enemy.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=3;Value=5000})
$enemy.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=4;Value=98})
$state = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$state.StageId=1; $state.RandomSeed=11
$state.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=12;DefaultGroundType=2}
$state.Units.Add($source); $state.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(2); $state.Waves.Add($wave)
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($state, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
$result = $executor.Step(0, 1)
$selection = $result.Event.SkillCasts[0].HitSelections[0]
$expected = [Math]::Sqrt(2.75 * 2.75 + 0.5 * 0.5) * [single]0.1
Assert-Delay ($result.Event.DamageEvents.Count -eq 1 -and [Math]::Abs($result.Event.DamageEvents[0].Delay - $expected) -lt 0.00001) 'Production damage did not use the second original stack offset'
Assert-Delay ($selection.HasDelayOverride -and $selection.DelayOverrideIndex -eq 1 -and [Math]::Abs($selection.EffectDelays[0] - $expected) -lt 0.00001) 'Production receipt and damage use different override parameters'
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
$receipt = $restored.Receipts[0].Event.SkillCasts[0].HitSelections[0]
Assert-Delay ($receipt.HasDelayOverride -and $receipt.DelayOverrideIndex -eq 1 -and [Math]::Abs($receipt.EffectDelays[0] - $expected) -lt 0.00001) 'Checkpoint lost the resolved stack flight'
Write-Output 'Hit status delay overrides passed: three original skills, ten offsets, shared priority, zero/first/overflow boundaries, frozen definitions, executor settlement and checkpoint receipts.'

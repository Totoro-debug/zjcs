$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-RoundRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-RoundRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function Assert-RoundStart($condition, $message) { if (-not $condition) { throw $message } }
$skills = Read-RoundRule 'skill-rules'; $battle = Read-RoundRule 'battle-rules'; $rules = Read-RoundRule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skills,(Hash-RoundRule $skills),'854_156383',$battle,(Hash-RoundRule $battle),'854_156383')
$formula = $catalog.CreateDamageFormula($rules,(Hash-RoundRule $rules),'854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-RoundRule $rules),'854_156383')
function New-RoundStartFixture($statusId, $restore, $legacy = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $ids = if ($statusId -in 25124,25127,25171,25172) { @(1,2,3) } else { @(1,2) }
    foreach ($id in $ids) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $u.Side = $id; $u.X = $id + 2; $u.Y = 2; $u.CharacterType = 1
        $u.Hp = $u.MaxHp = $u.LastMaxHp = 1000000
        $u.Level = $u.SubRank = 1
        foreach ($pair in @(@(1,1000),@(2,1000),@(3,1000000),@(4,$(if($id -eq 1){100}else{1})))) {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    if ($ids.Count -eq 3) { $cp.Units[0].MasterId = $cp.Units[0].Turn.MasterId = 3; $cp.Units[2].Side = 1 }
    $source = $catalog.CreateStatus($statusId,1,1,990000+$statusId,0,0,5,20,1,$null)
    $cp.Units[0].Statuses.Add($source)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    Assert-RoundStart ($cp.Counter -eq 0 -and $source.RoundStartTriggeredCount -eq 0) 'Preparation ran an unpresented round-start invocation'
    if ($restore -or $legacy) {
        $json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        if ($legacy) {
            $json = $json -replace '"RoundStartDefinitionResolved":true,','' -replace '"RoundStartStartChecked":false,','' -replace '"RoundStartRemoveWhenNotEnoughTimes":true,',''
            $json = $json -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
        }
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $source = $cp.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq $statusId})
    }
    $first = $runner.Step(0,1)
    Assert-RoundStart ($source.RoundStartTriggeredCount -eq 1 -and $cp.Units[0].SkillState.Round -eq 0) "Original $statusId did not invoke once at status start before the first personal round"
    $expected = switch($statusId) {24514 {@(24074,24075,24076,24077)} 24515 {@(24016,24017,24018,24019)} 25130 {@(24417,24004)}}
    foreach($id in $expected) { Assert-RoundStart ($cp.Units[0].Statuses.ClassId -contains $id) "Original $statusId lost startup status $id" }
    if ($source.RoundStartSkills.Count -gt 0) {
        $skillId = $source.RoundStartSkills[0].EntityId
        $cast = @($first.Event.SkillCasts | Where-Object SkillEntityId -eq $skillId)
        Assert-RoundStart ($cast.Count -eq 1 -and $cast[0].ActorId -eq 1) "Original $statusId did not execute startup skill $skillId from its applicator"
        if ($ids.Count -eq 3) { Assert-RoundStart ($cast[0].TargetIds -contains 3) "Original $statusId lost its listened-to master's target" }
    }
    if ($source.RoundStartMaxCount -eq 1) {
        Assert-RoundStart (($cp.Units[0].Statuses -contains $source) -ne $source.RoundStartRemoveWhenNotEnoughTimes) "Original $statusId did not honor exhausted removal immediately after invocation"
    }
    $saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    Assert-RoundStart ($runner.Step(0,1).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Replayed receipt repeated round-start startup'
    return @{ Saved=$saved; Checkpoint=$cp; Runner=$runner; Source=$source }
}
foreach($id in 24514,24515,25077,25121,25124,25127,25130,25167,25171,25172,25287) {
    $direct = New-RoundStartFixture $id $false
    $restored = New-RoundStartFixture $id $true
    Assert-RoundStart ($direct.Saved -eq $restored.Saved) "Original $id startup changed after preparation restore"
}
$finite = New-RoundStartFixture 25077 $false
$legacy = New-RoundStartFixture 25077 $true $true
Assert-RoundStart ($finite.Saved -eq $legacy.Saved) 'Legacy round-start definitions did not restore without repeating counters'
foreach ($fixture in $finite,$legacy) {
    $cp = $fixture.Checkpoint; $runner = $fixture.Runner; $source = $fixture.Source
    $casts = 1
    while ($source.RoundStartTriggeredCount -lt 4 -and -not $cp.Finished) {
        # Restore after every receipt; a persisted checked startup must never run again.
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $source = $cp.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq 25077})
        $result = $runner.Step($cp.Sequence,$cp.Counter+1)
        $casts += @($result.Event.SkillCasts | Where-Object SkillEntityId -eq 18860).Count
        Assert-RoundStart ($cp.Counter -lt 20) 'Original 25077 failed to reach its fourth invocation'
    }
    Assert-RoundStart ($casts -eq 4 -and $source.RoundStartTriggeredCount -eq 4 -and $cp.Units[0].SkillState.Round -eq 3 -and $cp.Units[0].Statuses.ClassId -notcontains 25077) 'Original 25077 did not finish four calls and remove immediately in personal round three'
}
# A state created by a real hit must start in that hit's timeline, rather than
# waiting for the next global step or consuming another personal turn.
$cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($finite.Saved)
$cp.Counter = 0; $cp.Receipts.Clear(); $cp.Units[0].Statuses.Clear()
$parent = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$parent.EntityId = $parent.ItemId = 990001; $parent.Slot = 0; $parent.TargetType = 1
$parent.ActionDuration = 1; $parent.Duration = 1.2
$parent.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = $hit.DamageId = 990002; $hit.DamageType = 8
$hit.HasCustomDamage = $true; $hit.CustomDamage = 5; $hit.Delay = 0.4
$hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId=25077; TargetType=2; AffectedByProp=$false })
$parent.Hits.Add($hit)
$cp.Units[0].Skills.Add($parent)
$cp.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
$result = $runner.Step(0,1)
$cast = @($result.Event.SkillCasts | Where-Object SkillEntityId -eq 18860)
Assert-RoundStart ($cast.Count -eq 1 -and [Math]::Abs($cast[0].Delay-0.4) -lt 0.0001 -and $cp.Units[0].SkillState.Round -eq 1) 'Hit-applied original 25077 lost its immediate startup time'
$endless = New-RoundStartFixture 24514 $false
$null = $endless.Runner.Step(0,2)
Assert-RoundStart ($endless.Source.RoundStartTriggeredCount -eq 2 -and $endless.Checkpoint.Units[0].Statuses.Contains($endless.Source)) 'Unlimited round-start state stopped after its startup call'
Write-Output 'Round-start startup passed: 11 supported original startup states, source/master targeting, configured effects, immediate finite removal, subsequent rounds, hit-local timing, receipt replay, legacy and deterministic checkpoint restore.'

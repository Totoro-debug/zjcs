$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-StatusSourceRule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-StatusSourceRule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-StatusSource($condition,$message){if(-not $condition){throw $message}}
$skills=Read-StatusSourceRule 'skill-rules';$battle=Read-StatusSourceRule 'battle-rules';$rules=Read-StatusSourceRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-StatusSourceRule $skills),'854_156383',$battle,(Hash-StatusSourceRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-StatusSourceRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-StatusSourceRule $rules),'854_156383')
function New-OriginStatus($classId,$creator=2,$skill=63280,$item=0,$equipment=0){
    $s=$catalog.CreateStatus($classId,1,$creator,10,$skill,$item,5,20,1,$null)
    if($equipment -gt 0){$s.Origin=[Zjcs.Combat.Official.FightBattleStatusOrigin]@{IsSet=$true;UnitId=$creator;EntityKind=1;EntityId=$equipment;SkillItemId=$item;SkillEntityId=$skill}}
    return $s
}
foreach($id in 24037,24038,24045,24222,24472,24473,24474,24486,24640){
    $owner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000000;MaxHp=1000000}
    $old=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus $id),0,1)
    $old.RoundStartTriggeredCount=1;$old.StackCount=4
    $latest=New-OriginStatus $id
    $latest.SourceProperties=[System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
    $latest.SourceProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=1;Value=777})
    $merged=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,$latest,1,2)
    Assert-StatusSource ([object]::ReferenceEquals($old,$merged) -and $owner.Statuses.Count -eq 1 -and $merged.InstanceId -eq 1 -and $merged.StackCount -eq 4 -and $merged.RoundStartTriggeredCount -eq 1) "Original $id lost its live instance, layers or actual calls"
    Assert-StatusSource ($merged.StartRound -eq 1 -and $merged.CurDurationRound -eq 5 -and [Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($merged) -eq 6 -and $merged.SourceProperties[0].Value -eq 777 -and $merged.SourceSkillRank -eq 5 -and $merged.SourceSkillLevel -eq 20) 'Original duration merge lost remaining time, extra quota or latest retained source'
    [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus $id 3),1,3)|Out-Null
    [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus $id 2 63290),1,4)|Out-Null
    Assert-StatusSource ($owner.Statuses.Count -eq 3 -and $owner.Statuses[1].RoundStartTriggeredCount -eq 0 -and $owner.Statuses[2].RoundStartTriggeredCount -eq 0) 'Different creator or skill was merged with the original state'
    # Legacy duplicate instances stay separate; only the first matching live instance is refreshed.
    $duplicate=New-OriginStatus $id;$duplicate.InstanceId=5;$duplicate.StartRound=1
    $owner.Statuses.Add($duplicate)
    $merged=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus $id),1,6)
    Assert-StatusSource ([object]::ReferenceEquals($merged,$old) -and $duplicate.RoundStartStackRoundCount -eq 1 -and $owner.Statuses.Count -eq 4) 'Renewal rewrote every legacy duplicate or selected a later instance'
}
# The confirmed identity rule applies even when the old class-only policy said replace/discard.
foreach($behaviour in 0..2){
    $owner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000;MaxHp=1000}
    $template=New-OriginStatus 24105
    $template.ActionBehOfSameStatus=$behaviour
    $old=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,$template,0,1)
    $old.StackCount=3;$old.ShieldInitialized=$true;$old.ShieldRemainValue=37;$old.ShieldRemainCount=2;$old.RoundStartTriggeredCount=1;$old.RoundStartMeetProgress=1
    $merged=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,$template,1,2)
    Assert-StatusSource ([object]::ReferenceEquals($old,$merged) -and $owner.Statuses.Count -eq 1 -and $merged.StartRound -eq 1 -and $merged.CurDurationRound -eq $template.DurationRound -and $merged.StackCount -eq 4 -and $merged.ShieldRemainValue -eq 37 -and $merged.ShieldRemainCount -eq 2 -and $merged.RoundStartTriggeredCount -eq 1 -and $merged.RoundStartMeetProgress -eq 1) 'Same-source renewal discarded accumulated effects or ignored its new clock'
    [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus 24105 3),1,3)|Out-Null
    Assert-StatusSource ($owner.Statuses.Count -eq 2) 'Class-only replace/discard erased a different creator'
}
$owner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000;MaxHp=1000}
foreach($gear in 101,102){[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus 24640 2 63280 6328 $gear),0,$gear)|Out-Null}
$first=$owner.Statuses[0];$second=$owner.Statuses[1]
$merged=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,(New-OriginStatus 24640 2 63280 6328 101),1,103)
Assert-StatusSource ($owner.Statuses.Count -eq 2 -and [object]::ReferenceEquals($merged,$first) -and $second.RoundStartStackRoundCount -eq 1) 'Distinct equipment instances collided or failed to renew independently'
$trigger=$first.RoundStartSkills[0].WithSource($first)
Assert-StatusSource ($trigger.Origin.EntityKind -eq 1 -and $trigger.Origin.EntityId -eq 101 -and -not [object]::ReferenceEquals($trigger.Origin,$first.Origin)) 'Triggered skill lost or aliased equipment ancestry'
$legacy=New-OriginStatus 24640;$legacy.Origin=$null
Assert-StatusSource ([Zjcs.Combat.Official.FightBattleStatusOrigin]::Matches([Zjcs.Combat.Official.FightBattleStatusOrigin]::Resolve($legacy),[Zjcs.Combat.Official.FightBattleStatusOrigin]::Resolve((New-OriginStatus 24640)))) 'Saved direct skill metadata could not recover legacy source identity'
$unknown=New-OriginStatus 24640 2 0
Assert-StatusSource (-not [Zjcs.Combat.Official.FightBattleStatusOrigin]::Matches([Zjcs.Combat.Official.FightBattleStatusOrigin]::Resolve($unknown),[Zjcs.Combat.Official.FightBattleStatusOrigin]::Resolve($unknown))) 'Missing skill identity was guessed from the status class'
$equipmentOwner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000;MaxHp=1000}
$passives=[System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialPassiveSkillBinding]]::new()
foreach($gear in 101,101,102){
    $passives.Add([Zjcs.Combat.Official.FightOfficialPassiveSkillBinding]@{ItemId=17052;Rank=1;Level=1;SubRank=1;Origin=[Zjcs.Combat.Official.FightBattleStatusOrigin]@{IsSet=$true;EntityKind=1;EntityId=$gear}})
}
$bindings=$catalog.CreatePassiveStatusBindings($equipmentOwner,$items,$passives)
$catalog.AssembleUnit($equipmentOwner,$null,$bindings)
Assert-StatusSource ($equipmentOwner.Statuses.Count -eq 2 -and $equipmentOwner.Statuses[0].Origin.EntityId -eq 101 -and $equipmentOwner.Statuses[1].Origin.EntityId -eq 102 -and $equipmentOwner.Statuses[0].Origin.UnitId -eq 1 -and $equipmentOwner.Statuses[0].Origin.SkillItemId -eq 17052) 'Passive assembly discarded equipment identity or its real skill'
Assert-StatusSource (@($equipmentOwner.Statuses|Where-Object {$_.StartRound -ne -1 -or $_.RoundStartTriggeredCount -ne 0}).Count -eq 0 -and $equipmentOwner.PendingStatusApplications.Count -eq 0) 'Repeated initial passive started its lifecycle or reported an application before battle startup'
$initialPassive=$equipmentOwner.Statuses[0]
$catalog.AssembleUnit($equipmentOwner,$null,$catalog.CreatePassiveStatusBindings($equipmentOwner,$items,$passives))
Assert-StatusSource ($equipmentOwner.Statuses.Count -eq 2 -and [object]::ReferenceEquals($initialPassive,$equipmentOwner.Statuses[0]) -and $initialPassive.StartRound -eq -1) 'Repeated assembly duplicated or started an existing passive instance'
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($equipmentOwner,$equipmentOwner,[Zjcs.Combat.Official.FightBattleUnitState[]]@($equipmentOwner),[Zjcs.Combat.Official.FightRandomStream]::new(23),$catalog)|Out-Null
$effects=@($equipmentOwner.Statuses|Where-Object {$_.ClassId -eq 60521 -or $_.ClassId -eq 60522})
Assert-StatusSource ($effects.Count -eq 2 -and @($effects.Origin.EntityId|Sort-Object -Unique).Count -eq 2 -and @($effects|Where-Object {$_.Origin.SkillItemId -ne 17052 -or $_.Origin.EntityKind -ne 1}).Count -eq 0) 'Original equipment passive status effects lost independent origins'
$summonOwner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000;MaxHp=1000}
foreach($gear in 101,102){$s=New-OriginStatus 60560 1 60560 17056 $gear;$s.InstanceId=$gear;$summonOwner.Statuses.Add($s)}
$summon=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;MasterId=1;Hp=1000;MaxHp=1000}
$summonUnits=[Zjcs.Combat.Official.FightBattleUnitState[]]@($summonOwner,$summon)
$summonRng=[Zjcs.Combat.Official.FightRandomStream]::new(23)
foreach($rebuild in 1..3){[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyRoleSummonCount($summonOwner,$summonUnits,$catalog,$summonRng)|Out-Null}
$effects=@($summonOwner.Statuses|Where-Object ClassId -eq 60561)
Assert-StatusSource ($effects.Count -eq 2 -and @($effects.Origin.EntityId|Sort-Object -Unique).Count -eq 2 -and @($effects|Where-Object StackCount -ne 1).Count -eq 0) 'Summon-count producer selected another equipment source or repeatedly added layers'
$summon.Ended=$true
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyRoleSummonCount($summonOwner,$summonUnits,$catalog,$summonRng)|Out-Null
Assert-StatusSource (@($summonOwner.Statuses|Where-Object ClassId -eq 60561).Count -eq 0 -and $summon.Hp -gt 0) 'Departed summon with retained HP was still counted as an active summon'
$summon.Ended=$false
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyRoleSummonCount($summonOwner,$summonUnits,$catalog,$summonRng)|Out-Null
Assert-StatusSource (@($summonOwner.Statuses|Where-Object ClassId -eq 60561).Count -eq 2) 'Live summons did not restore the independent equipment effects'
$summon.Hp=0
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyRoleSummonCount($summonOwner,$summonUnits,$catalog,$summonRng)|Out-Null
Assert-StatusSource (@($summonOwner.Statuses|Where-Object ClassId -eq 60561).Count -eq 0) 'Summon-count producer left another source contribution after all summons disappeared'
$rangeSource=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000;MaxHp=1000;Side=1;CharacterType=1}
$rangeTarget=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;Hp=1000;MaxHp=1000;Side=1;CharacterType=1;X=1}
foreach($gear in 101,102){$s=New-OriginStatus 24063 1 10500 10000 $gear;$s.InstanceId=$gear;$rangeSource.Statuses.Add($s)}
$rangeUnits=[Zjcs.Combat.Official.FightBattleUnitState[]]@($rangeSource,$rangeTarget)
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyMoveRange($rangeSource,$rangeUnits,$catalog,$summonRng,$false)|Out-Null
Assert-StatusSource (@($rangeTarget.Statuses|Where-Object ClassId -eq 24033).Count -eq 2) 'Independent equipment range effects collided on entering range'
$rangeSource.Statuses[0].MoveRangeOffsets.Clear()
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyMoveRange($rangeSource,$rangeUnits,$catalog,$summonRng,$false)|Out-Null
$remaining=@($rangeTarget.Statuses|Where-Object ClassId -eq 24033)
Assert-StatusSource ($remaining.Count -eq 1 -and $remaining[0].Origin.EntityId -eq 102) 'Leaving one source range removed another equipment contribution'
function Invoke-OriginBattle($restore,$legacy=$false){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.MaxRound=12;$cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
    foreach($id in 1..3){
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id;$u.Side=$(if($id -eq 1){1}else{2});$u.CharacterType=1;$u.X=$id+1;$u.Y=2
        $u.MaxHp=$u.LastMaxHp=$u.Hp=10000000;$u.Level=$u.SubRank=1
        foreach($pair in @(@(1,9000),@(2,100),@(3,$u.MaxHp),@(4,$(if($id -eq 1){100}else{1})))){$u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})}
        $u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    foreach($caster in 2,3){[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($cp.Units[0],(New-OriginStatus 24640 $caster),0,$caster)|Out-Null}
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    if($legacy){
        foreach($status in $cp.Units[0].Statuses){$status.RoundStartRulesSupported=$false;$status.Origin=$null}
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    foreach($round in 1..7){
        if($restore){$cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp));$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)}
        $runner.Step(0,$cp.Counter+1)|Out-Null
        $event=$cp.Receipts[$cp.Receipts.Count-1].Event
        foreach($caster in 2,3){
            $limit=$(if($caster -eq 2){6}else{3})
            $damage=@($event.DamageEvents|Where-Object {$_.SkillEntityId -eq 63280 -and $_.SourceId -eq $caster})
            Assert-StatusSource ($damage.Count -eq [int]($round -le $limit)) 'Independent source damage stopped early or shared another source duration/quota'
            if($damage.Count){Assert-StatusSource ($damage[0].TargetId -eq 1 -and $damage[0].Damage -lt 0 -and $damage[0].AfterHp -lt $damage[0].BeforeHp) 'Independent source lost its actual original damage'}
        }
        if($round -eq 1){[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($cp.Units[0],(New-OriginStatus 24640),$cp.Units[0].SkillState.Round,9)|Out-Null}
        $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        Assert-StatusSource ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Duplicate request reapplied source renewal'
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
$continuous=Invoke-OriginBattle $false
Assert-StatusSource ($continuous -eq (Invoke-OriginBattle $true)) 'Independent source renewal changed after per-round checkpoint restore'
# Finite initial statuses can use a different unit's clock. Merge their configured
# duration before that clock starts, and retain it across a prepared checkpoint.
$prepared=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($continuous)
$preparedOwner=$prepared.Units[0];$preparedOwner.Statuses.Clear()
$initialBindings=[System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialStatusBinding]]::new()
foreach($instance in 9001,9002){$initialBindings.Add([Zjcs.Combat.Official.FightOfficialStatusBinding]@{ClassId=24640;InstanceId=$instance;CreatorId=2;RootSkillId=63280;SkillRank=5;SkillLevel=20;SourceSubRank=1})}
$catalog.AssembleUnit($preparedOwner,$null,$initialBindings)
$preparedStatus=$preparedOwner.Statuses[0]
Assert-StatusSource ($preparedOwner.Statuses.Count -eq 1 -and $preparedStatus.StartRound -eq -1 -and $preparedStatus.CurDurationRound -eq 6 -and [Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($preparedStatus) -eq 6) 'Initial finite duration merge lost its duration or quota before the actual clock started'
$prepared=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($prepared))
$preparedStatus=$prepared.Units[0].Statuses[0]
[Zjcs.Combat.Official.FightStatusLifecycle]::Initialize($preparedStatus,8)
Assert-StatusSource ($preparedStatus.StartRound -eq 8 -and $preparedStatus.CurDurationRound -eq 6 -and -not $preparedStatus.DurationPreparedBeforeStart) 'Prepared checkpoint startup discarded the already merged duration'
$preparedOwner.Statuses.Clear()
foreach($binding in $initialBindings){$binding.ClassId=12202}
$catalog.AssembleUnit($preparedOwner,$null,$initialBindings)
Assert-StatusSource ($preparedOwner.Statuses.Count -eq 1 -and $preparedOwner.Statuses[0].StartRound -eq -1 -and $preparedOwner.Statuses[0].RoundTarget -eq 1) 'Initial creator-clock renewal required or fabricated a clock before battle startup'
# Missing legacy origin fields remain absent on untouched instances, but the formerly blocked
# definition must become executable without resetting its counters or duration.
$legacyRun=Invoke-OriginBattle $true $true
Assert-StatusSource ($legacyRun -eq $continuous) 'Previously blocked merged definition did not resume the same actual battle'
# Actual hit reapplication must use the creator clock, rather than the victim's zero rounds.
$cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($continuous)
$actor=$cp.Units[0];$actor.Skills.Clear();$actor.SkillState.Slots.Clear()
$skill=[Zjcs.Combat.Official.FightBattleSkillDefinition]@{EntityId=9900500;Slot=0;Rank=5;Level=20;SubRank=1;TargetType=1}
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$hit=[Zjcs.Combat.Official.FightBattleHitDefinition]@{EntityId=9900501;DamageId=9900502;DamageType=1;HasCustomDamage=$true;CustomDamage=0}
$hit.AppliedStatuses=[System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusApplication]]::new()
$hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{StatusId=12202;Chance=1;AffectedByProp=$false})
$skill.Hits.Add($hit)
$actor.Skills.Add($skill);$actor.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,0,0,0))
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
$first=$null
foreach($round in 8..9){
    $runner.Step(0,$cp.Counter+1)|Out-Null
    $victim=$cp.Units|Where-Object {$_.Statuses.ClassId -contains 12202}|Select-Object -First 1
    $state=$victim.Statuses|Where-Object ClassId -eq 12202|Select-Object -First 1
    Assert-StatusSource ($state -ne $null -and $state.StartRound -eq $round -and $actor.SkillState.Round -eq $round -and $victim.SkillState.Round -eq 0) 'Actual hit renewal used the victim clock instead of its configured creator clock'
    if($first -ne $null){Assert-StatusSource ([object]::ReferenceEquals($state,$first)) 'Actual hit renewal replaced its live instance'}
    $first=$state
}
# Original self-target child 63433 -> 63434 -> 63436 -> 24658 retains the parent equipment entity.
$cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($continuous)
$actor=$cp.Units[0];$actor.Skills.Clear();$actor.SkillState.Slots.Clear()
$skill=$catalog.CreateSkill(63430,0,0,5,20,1,$null)
$actor.Skills.Add($skill);$actor.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,0,0,0))
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
foreach($equipmentId in 101,102){
    $skill.StatusOrigin=[Zjcs.Combat.Official.FightBattleStatusOrigin]@{IsSet=$true;UnitId=1;EntityKind=1;EntityId=$equipmentId;SkillEntityId=63430}
    $runner.Step(0,$cp.Counter+1)|Out-Null
    Assert-StatusSource (@($cp.Receipts[$cp.Receipts.Count-1].Event.SkillCasts|Where-Object SkillEntityId -eq 63434).Count -gt 0) 'Original damage child did not execute'
}
$effects=@($actor.Statuses|Where-Object ClassId -eq 24658)
Assert-StatusSource ($effects.Count -eq 2 -and @($effects.Origin.EntityId|Sort-Object -Unique).Count -eq 2 -and @($effects|Where-Object {$_.Origin.EntityKind -ne 1 -or $_.Origin.SkillEntityId -ne 63430 -or $_.Origin.UnitId -ne 1 -or $_.StackCount -ne 1}).Count -eq 0) 'Actual child status lost its parent equipment identity or merged independent equipment effects'
Write-Output 'Status source checks passed: nine original merges, independent creators/skills/equipment, summon-count and range removal isolation, stable cultivation, preserved live effects, legacy identity, actual creator clocks, real original child status ancestry and exact per-round continuation.'

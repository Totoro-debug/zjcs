$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path @(Get-FightCombatSources)
$skillPath=$root+'/unity/Assets/Official/Combat/skill-rules.json';$battlePath=$root+'/unity/Assets/Official/Combat/battle-rules.json'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load([IO.File]::ReadAllText($skillPath),(Get-FileHash $skillPath).Hash.ToLowerInvariant(),'854_156383',[IO.File]::ReadAllText($battlePath),(Get-FileHash $battlePath).Hash.ToLowerInvariant(),'854_156383')
function Assert-Finish($value,$label){if(-not $value){throw $label}}
function New-Unit($id,$side,$x,$y){
 $u=[Zjcs.Combat.Official.FightBattleUnitState]::new();$u.Id=$id;$u.Side=$side;$u.CharacterType=1;$u.X=$x;$u.Y=$y;$u.Hp=$u.MaxHp=$u.LastMaxHp=100000;$u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=100-$id;return $u
}
function Add-Prop($unit,$name,$value){
 $prop=[Zjcs.Combat.Official.FightPropertyValue]::new();$prop.Id=$catalog.PropertyType($name);$prop.Value=$value;$unit.Properties.Add($prop)
}
function Add-Eligibility($unit){
 $status=$catalog.CreateStatus(24721,$unit.Id,$unit.Id,1,13960,0,1,1,1,$null);$unit.Statuses.Add($status);return $status
}

$rulesPath=$root+'/unity/Assets/Official/Combat/rules.json'
$formula=$catalog.CreateDamageFormula([IO.File]::ReadAllText($rulesPath),(Get-FileHash $rulesPath).Hash.ToLowerInvariant(),'854_156383')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$logicType=[Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime',$true)
$create=$logicType.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$start=$logicType.GetMethod('OnConfiguredSkillStart',$flags)
$transient=$logicType.GetMethod('OnConfiguredTransient',$flags)
$collect=$logicType.GetMethod('CollectChildSkills',$flags)
function New-ExplicitLogic($actor,$definition,$units,$random){$create.Invoke($null,@($actor,$definition,$units,$catalog,$random,$null))}
function Field($value,$name){$value.GetType().GetField($name,$flags).GetValue($value)}

$owner=New-Unit 1 1 5 5;$pet=New-Unit 2 1 5 5;$enemy=New-Unit 3 2 5 6
$pet.MasterId=$pet.Turn.MasterId=1;$pet.CharacterType=7
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($owner);$units.Add($pet);$units.Add($enemy)
$random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$definition=$catalog.CreateSkill(50213,0,16020,5,20,3,$null)
Assert-Finish ($definition.HitGraphEntities.Count -eq 0 -and -not $definition.CanTriggerChild) 'The original pure start skill was not preserved.'
$logic=New-ExplicitLogic $pet $definition $units $random
Assert-Finish ($start.Invoke($logic,@($owner))) 'Pure start graph did not apply its explicit status.'
$status=@($owner.Statuses|Where-Object ClassId -eq 24825)
Assert-Finish ($status.Count -eq 1 -and $status[0].OwnerId -eq 1 -and $status[0].CreatorId -eq 2 -and $status[0].SourceSkillRank -eq 5 -and $status[0].Origin.SkillEntityId -eq 50213 -and $pet.Statuses.Count -eq 0) 'Pure start status lost the master target or skill source.'
Assert-Finish (-not $start.Invoke($logic,@($owner)) -and @($owner.Statuses|Where-Object ClassId -eq 24825).Count -eq 1) 'Pure start graph ran more than once.'
foreach($fault in @('port','rate','status','affected','filter','missing','ended','wrongTarget')){
 $owner.Statuses.Clear();$owner.Ended=$false;$pet.MasterId=1
 $bad=$catalog.CreateSkill(50213,0,16020,5,20,3,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 $target=$owner
 switch($fault){
 'port'{$json.OnSkillStart.HandleCfgs[0].TargetEntityKey.Id=2002}
 'rate'{$json.OnSkillStart.HandleCfgs[0].TriggerStatusCfgs[0].BasePercent=.5}
 'status'{$json.OnSkillStart.HandleCfgs[0].TriggerStatusCfgs[0].StatusId=24823}
 'affected'{$json.OnSkillStart.HandleCfgs[0].TriggerStatusCfgs[0].AffectedByProp=$true}
 'filter'{$json.OnSkillStart.FilterCondition=@{_type='Unknown'}}
 'missing'{$pet.MasterId=0}
 'ended'{$owner.Ended=$true}
 'wrongTarget'{$target=$enemy}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 40 -Compress
 $logic=New-ExplicitLogic $pet $bad $units $random
 Assert-Finish (-not $start.Invoke($logic,@($target)) -and $owner.Statuses.Count -eq 0) ('Unsupported pure-start binding applied status: '+$fault)
}
$owner.Ended=$false;$pet.MasterId=1
Write-Output '50213 original empty-hit start graph: explicit master status, source/cultivation, once per cast and malformed/unbound input passed.'

$definition=$catalog.CreateSkill(50230,0,16021,5,20,3,$null)
$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($definition)[0]
$logic=New-ExplicitLogic $pet $definition $units $random
$request=$collect.Invoke($logic,@($hit,$enemy,$null))
Assert-Finish (-not $definition.CanTriggerChild -and $request.Count -eq 1 -and (Field $request[0] 'SkillId') -eq 50235 -and (Field $request[0] 'Target') -eq $owner -and [Math]::Abs((Field $request[0] 'Delay')-.15) -lt .0001) 'Explicit 50230 master recovery was suppressed or rebound.'
foreach($fault in @('port','rate','filter','missing','ended','wrongHit')){
 $owner.Ended=$false;$pet.MasterId=1;$selectedHit=$hit
 $bad=$catalog.CreateSkill(50230,0,16021,5,20,3,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault){
 'port'{$json.OnDoHit.HandleCfgs[1].TargetEntityKey.Id=2002}
 'rate'{$json.OnDoHit.HandleCfgs[1].Rate=.5}
 'filter'{$json.OnDoHit.FilterCondition=@{_type='Unknown'}}
 'missing'{$pet.MasterId=0}
 'ended'{$owner.Ended=$true}
 'wrongHit'{$selectedHit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new();$selectedHit.EntityId=13961}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 40 -Compress
 $logic=New-ExplicitLogic $pet $bad $units $random
 Assert-Finish ($collect.Invoke($logic,@($selectedHit,$enemy,$null)).Count -eq 0) ('Unsupported recovery binding generated a child: '+$fault)
}
$owner.Ended=$false;$pet.MasterId=1
Write-Output '50230 explicit child exception: actual master, original delay, missing/ended bindings and malformed graphs passed.'

$definition=$catalog.CreateSkill(64450,0,0,5,20,3,$null)
$hits=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($definition)
Assert-Finish (-not $definition.CanTriggerChild -and $hits.Count -gt 1) 'Original chained explicit status skill is missing.'
$normal=[Zjcs.Combat.Official.FightDamageTypeData]::new()
$random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$expect=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$logic=New-ExplicitLogic $pet $definition $units $random
$success=0;$failure=0
for($i=0;$i -lt 64;$i++){
 $enemy.Statuses.Clear();$expected=$expect.Next() -le .5
 $actual=[bool]$transient.Invoke($logic,@($enemy,$hits[$i%$hits.Count],$normal))
 Assert-Finish ($actual -eq $expected -and @($enemy.Statuses|Where-Object ClassId -eq 24772).Count -eq [int]$expected -and $random.Seed -eq $expect.Seed) '64450 did not use exactly one original 50% roll for each chained segment.'
 if($actual){$success++;$applied=$enemy.Statuses[0];Assert-Finish ($applied.OwnerId -eq 3 -and $applied.CreatorId -eq 2 -and $applied.Origin.SkillEntityId -eq 64450) '64450 status source differs.'}else{$failure++}
}
Assert-Finish ($success -gt 0 -and $failure -gt 0) 'The explicit probability fixture never exercised both outcomes.'
foreach($fault in @('marked','dodge','blind','port','status','relation','rate','affected','filter')){
 $enemy.Statuses.Clear();$type=[Zjcs.Combat.Official.FightDamageTypeData]::new()
 $bad=$catalog.CreateSkill(64450,0,0,5,20,3,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault){
 'marked'{$enemy.Statuses.Add($catalog.CreateStatus(24773,3,3,1))}
 'dodge'{$type.Dodge=$true}
 'blind'{$type.Blinding=$true}
 'port'{$json.OnDoHitTransient.HandleCfgs[0].TargetEntityKey.Id=2003}
 'status'{$json.OnDoHitTransient.FilterCondition.TargetFilter.StatusId=24772}
 'relation'{$json.OnDoHitTransient.FilterCondition.TargetFilter.Relation=0}
 'rate'{$json.OnDoHitTransient.HandleCfgs[0].TriggerStatusCfgs[0].BasePercent=1}
 'affected'{$json.OnDoHitTransient.HandleCfgs[0].TriggerStatusCfgs[0].AffectedByProp=$true}
 'filter'{$json.OnDoHitTransient.FilterCondition._type='Unknown'}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 40 -Compress
 $logic=New-ExplicitLogic $pet $bad $units $random
 $seed=$random.Seed
 Assert-Finish (-not $transient.Invoke($logic,@($enemy,$hits[0],$type)) -and @($enemy.Statuses|Where-Object ClassId -eq 24772).Count -eq 0 -and $random.Seed -eq $seed) ('Rejected 64450 input changed state or random stream: '+$fault)
}
Write-Output ('64450 explicit chained transient effect: all '+$hits.Count+' original nodes, exact 50% stream, target/source, mark filter, miss/blind rejection and malformed graph passed.')

function Invoke-ExplicitFixture($skillId,$seed=91,$mode='normal'){
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=$seed
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=12;$state.Grid.Height=12;$state.Grid.DefaultGroundType=2
 $owner=New-Unit 1 1 4 4;$pet=New-Unit 2 1 4 4;$enemy=New-Unit 3 2 4 6
 $pet.MasterId=$pet.Turn.MasterId=1;$pet.CharacterType=7
 $owner.Turn.Speed=1;$pet.Turn.Speed=1000;$enemy.Turn.Speed=1
 $owner.Hp=$owner.Turn.Hp=50000
 if($mode -in @('conversion','full','banned')){
  $owner.Statuses.Add($catalog.CreateStatus(25136,1,1,11,13970,10209,7,30,3,$null))
  if($mode -eq 'full'){$owner.Hp=$owner.Turn.Hp=$owner.MaxHp}
  if($mode -eq 'banned'){$owner.Statuses.Add($catalog.CreateStatus(24223,1,1,12))}
 }
 foreach($unit in @($owner,$pet,$enemy)){
  Add-Prop $unit 'MaxHp' 100000;Add-Prop $unit 'Attack' 1000
  Add-Prop $unit 'BaseCureAdd' 10000;Add-Prop $unit 'BaseBeCureAdd' 10000
 }
 if($skillId -eq 50230){foreach($x in -1..1){foreach($y in -1..1){$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))}}}
 $props=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
 foreach($row in $catalog.CalculateSkillProps($skillId,5,20,3).GetEnumerator()){
  $props.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$row.Key;Value=$row.Value})
 }
 $item=if($skillId -eq 50230){16021}elseif($skillId -eq 50213){16020}else{0}
 $active=$catalog.CreateSkill($skillId,0,$item,5,20,3,$props)
 $active.HasCustomDamage=$true;$active.CustomDamage=101
 if($mode -eq 'autoChild'){$active.HitGraphEntities[0].ChildSkillId=13745;$active.HitGraphEntities[0].ChildSkillRate=1}
 $pet.Skills.Add($active);$pet.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 $state.Units.Add($owner);$state.Units.Add($pet);$state.Units.Add($enemy)
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(3);$state.Waves.Add($wave)
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
 $step=$battle.Step(0,1)
 Assert-Finish (@($step.Event.SkillCasts|Where-Object {$_.ActorId -eq 2 -and $_.SkillEntityId -eq $skillId}).Count -eq 1 -and $pet.SkillState.Slots[0].ReleasedTimes -eq 1) ('Original explicit fixture did not execute once: '+$skillId)
 $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
 Assert-Finish ($saved.RandomSeed -eq $state.RandomSeed -and $saved.Units[0].Hp -eq $owner.Hp -and $saved.Units[1].SkillState.Slots[0].ReleasedTimes -eq 1) 'Explicit graph checkpoint/release differs.'
 return @{State=$state;Owner=$owner;Pet=$pet;Enemy=$enemy;Event=$step.Event}
}
$f=Invoke-ExplicitFixture 50213
Assert-Finish (@($f.Owner.Statuses|Where-Object ClassId -eq 24825).Count -eq 1 -and $f.Event.DamageEvents.Count -eq 0 -and $f.Event.SkillCasts.Count -eq 1) 'Pure 50213 executor fabricated a hit/child or failed to apply its master status.'
Write-Output 'Original 50213 executor: successful pure-logic cast without a hit graph, one master status, one release and checkpoint passed.'
$f=Invoke-ExplicitFixture 50230
$heals=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 50235)
$parentHits=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 50230)
Assert-Finish ($parentHits.Count -eq 5 -and $heals.Count -eq 1 -and $heals[0].SourceId -eq 2 -and $heals[0].TargetId -eq 1 -and $heals[0].Damage -gt 0 -and [Math]::Abs($heals[0].Delay-.15) -lt .0001 -and $f.Owner.Hp -gt 50000 -and @($f.Event.SkillCasts|Where-Object SkillEntityId -eq 50235).Count -eq 1) ('Original 50230 per-Hit child duplicated across body cells or failed to cure its master: hits='+$parentHits.Count+' / cures='+$heals.Count+' / amounts='+($heals.Damage -join ','))
Write-Output ('Original 50230 executor: five independently settled body cells, one 50235 master recovery '+$heals[0].Damage+', original delay, one release and checkpoint passed.')
foreach($mode in @('conversion','full','banned','autoChild')){
 $f=Invoke-ExplicitFixture 50230 91 $mode
 $heals=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 50235)
 $shield=@($f.Owner.Statuses|Where-Object ClassId -eq 24435)
 Assert-Finish ($heals.Count -eq 1 -and @($f.Event.SkillCasts|Where-Object SkillEntityId -eq 13745).Count -eq 0) 'The explicit child exception leaked into automatic hit children.'
 if($mode -eq 'autoChild'){Assert-Finish ($heals[0].Damage -gt 0 -and $shield.Count -eq 0) 'An automatic-child gate blocked the explicit graph child.'}
 else{
  Assert-Finish ($heals[0].Damage -eq 0) 'Master recovery ignored its cure interception.'
  if($mode -eq 'banned'){Assert-Finish ($shield.Count -eq 0 -and $f.Owner.Hp -eq 50000) 'Prohibited master recovery produced HP or shield.'}
  else{Assert-Finish ($shield.Count -eq 1 -and $shield[0].CustomShieldValue -gt 0 -and $shield[0].ShieldRemainValue -eq $shield[0].CustomShieldValue) 'Whitelisted master recovery did not produce a custom shield.'}
  if($mode -eq 'full'){Assert-Finish ($f.Owner.Hp -eq $f.Owner.MaxHp) 'Full master recovery clipped the shield conversion input.'}
 }
 Write-Output ('Original 50230 coupled '+$mode+' passed: master recovery '+$heals[0].Damage+', shield '+($shield.ShieldRemainValue -join ','))
}
$f=Invoke-ExplicitFixture 64450
$parentHits=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 64450)
Assert-Finish ($parentHits.Count -eq 3 -and $f.Event.SkillCasts.Count -eq 1 -and $f.Pet.Statuses.Count -eq 0 -and @($f.Enemy.Statuses|Where-Object ClassId -eq 24772).Count -gt 0) ('Original chained transient effect failed or created a forbidden automatic child: hits='+$parentHits.Count+' / target statuses='+($f.Enemy.Statuses.ClassId -join ','))
Write-Output 'Original 64450 executor: all three original chained nodes, target debuff despite automatic-child gate, one ordinary release and checkpoint passed.'

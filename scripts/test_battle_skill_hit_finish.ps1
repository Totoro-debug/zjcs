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
$logicType=[Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$create=$logicType.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$start=$logicType.GetMethod('OnSkillStart',$flags)
$finish=$logicType.GetMethod('CollectHitFinishSkills',$flags)
function Field($value,$name){$value.GetType().GetField($name,$flags).GetValue($value)}
$source=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 5
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($source);$units.Add($enemy)
$random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$skill=$catalog.CreateSkill(13960,0,0,5,20,3,$null)
$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
function New-Logic($definition){$create.Invoke($null,@($source,$definition,$units,$catalog,$random,$null))}
$status=Add-Eligibility $source
$logic=New-Logic $skill
Assert-Finish ($null -ne $logic) 'The original hit-finish graph is not enabled.'
$null=$start.Invoke($logic,@())
$source.Statuses.Clear()
$request=$finish.Invoke($logic,@($hit,[decimal]103,[float]1.2))
Assert-Finish ($request.Count -eq 1 -and (Field $request[0] 'SkillId') -eq 13965 -and (Field $request[0] 'Target') -eq $source -and (Field $request[0] 'CustomCure') -eq 51 -and (Field $request[0] 'HasCustomCure')) 'Snapshot, caster binding or truncation differs.'
Assert-Finish ([Math]::Abs((Field $request[0] 'Delay')-1.2) -lt .0001) 'Hit completion time was lost.'
foreach($amount in @([decimal]0,[decimal]1,[decimal]-4)){
 Assert-Finish ($finish.Invoke($logic,@($hit,$amount,[float]1)).Count -eq 0) 'Zero recovery emitted a child cast.'
}
$request=$finish.Invoke($logic,@($hit,([decimal][long]::MaxValue*4),[float]1))
Assert-Finish ((Field $request[0] 'CustomCure') -eq [long]::MaxValue) 'Large multi-target sum overflowed the child payload.'
$logic=New-Logic $skill;$null=$start.Invoke($logic,@());$null=Add-Eligibility $source
Assert-Finish ($finish.Invoke($logic,@($hit,[decimal]100,[float]1)).Count -eq 0) 'Later status acquisition changed the cast-start snapshot.'
$source.Statuses.Clear()
foreach($fault in @('forbidden','zero','policy','port','counter','reset','scale','filter')){
 $status=Add-Eligibility $source
 $bad=$catalog.CreateSkill(13960,0,0,5,20,3,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault){
 'forbidden'{$status.ForbiddenType=1}
 'zero'{$status.StackCount=0}
 'policy'{$bad.CanTriggerChild=$false}
 'port'{$json.OnHitFinish.HandleCfgs[0].DamageKey.Id=1029}
 'counter'{$json.OnHitFinish.FilterCondition.CounterBBKey.IsPersistentBlackboard=$false}
 'reset'{$json.OnSkillStart.HandleCfgs[0].ResetValue=0}
 'scale'{$json.OnHitFinish.HandleCfgs[0].ScaleValue=4999}
 'filter'{$json.OnSkillStart.FilterCondition._type='Unknown'}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 80 -Compress
 $logic=New-Logic $bad;$null=$start.Invoke($logic,@())
 Assert-Finish ($finish.Invoke($logic,@($hit,[decimal]100,[float]1)).Count -eq 0) ('Unsupported/ineligible graph executed: '+$fault)
 $source.Statuses.Clear()
}
Assert-Finish ($random.Seed -eq 91) 'Hit-finish filtering consumed random draws.'
Write-Output '13960 hit-finish capability: start snapshot, persistent counter contract, truncation, zero, wide sums, exact bindings and child policy passed.'

$rulesPath=$root+'/unity/Assets/Official/Combat/rules.json'
$formula=$catalog.CreateDamageFormula([IO.File]::ReadAllText($rulesPath),(Get-FileHash $rulesPath).Hash.ToLowerInvariant(),'854_156383')
function Invoke-FinishFixture($mode){
$state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
$state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=12;$state.Grid.Height=12;$state.Grid.DefaultGroundType=2
$actor=New-Unit 1 1 4 4;$boss=New-Unit 2 2 4 6;$other=New-Unit 3 2 5 5
$actor.Hp=$actor.Turn.Hp=1000
Add-Prop $actor 'Attack' 1000
Add-Prop $actor 'BaseCureAdd' 10000
Add-Prop $actor 'BaseBeCureAdd' 10000
foreach($x in -1..1){foreach($y in -1..1){$boss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))}}
if($mode -ne 'late'){$null=Add-Eligibility $actor}
$active=$catalog.CreateSkill(13960,0,0,5,20,3,$null);$active.HasCustomDamage=$true;$active.CustomDamage=101
$actor.Skills.Add($active);$actor.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
$state.Units.Add($actor);$state.Units.Add($boss);$state.Units.Add($other)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$wave.UnitIds.Add(3);$state.Waves.Add($wave)
if($mode -eq 'partial'){
 $boss.Statuses.Add($catalog.CreateStatus(24174,2,2,2,0,0,1,1,1,$null,200))
 $other.Hp=$other.Turn.Hp=37
}
if($mode -eq 'full'){
 foreach($victim in @($boss,$other)){$victim.Statuses.Add($catalog.CreateStatus(24174,$victim.Id,$victim.Id,2,0,0,1,1,1,$null,20000))}
}
if($mode -eq 'curemods'){
 Add-Prop $actor 'CureAddPercent' 2500
 Add-Prop $actor 'BeCureAddPercent' 2500
 Add-Prop $actor 'FinalCureScale' 2000
}
if($mode -eq 'curedebuff'){Add-Prop $actor 'BeCureAddPercent' -5000}
if($mode -in @('conversion','conversionFull','conversionBanned')){
 $actor.Statuses.Add($catalog.CreateStatus(25136,1,1,11,13970,10209,7,30,3,$null))
 if($mode -eq 'conversionFull'){$actor.Hp=$actor.Turn.Hp=$actor.MaxHp}
 if($mode -eq 'conversionBanned'){$actor.Statuses.Add($catalog.CreateStatus(24223,1,1,12))}
}
if($mode -eq 'celldelay'){$active.HitGraphEntities[0].ScopeEntries[0].Delay=.4}
if($mode -eq 'dodge'){foreach($victim in @($boss,$other)){Add-Prop $victim 'DodgePercent' 10000}}
if($mode -eq 'blind'){Add-Prop $actor 'BlindingPercent' 10000}
if($mode -eq 'cap'){$actor.Hp=$actor.Turn.Hp=99990}
if($mode -eq 'policy'){$active.CanTriggerChild=$false}
if($mode -eq 'multiple'){
 $active.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{EntityId=13961;Delay=2;Anchor=1})
}
if($mode -eq 'child'){
 $node=$active.HitGraphEntities|Where-Object EntityId -eq 13961
 $node.ChildSkillId=13745;$node.ChildSkillRate=1
}
$settlement=[Zjcs.Combat.Official.FightDamageSettlement]::new()
if($mode -in @('removed','late','dead')){
 $settlement.add_SourceHit([Action[Zjcs.Combat.Official.FightDamageEvent]]{
  param($damage)
  if($damage.RealDamageValue -ge 0){return}
  if($mode -eq 'dead'){$actor.Hp=$actor.Turn.Hp=0}
  elseif($mode -eq 'removed'){$actor.Statuses.Clear()}
  elseif($actor.Statuses.Count -eq 0){$null=Add-Eligibility $actor}
 }.GetNewClosure())
}
$battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,$settlement,$catalog)
$step=$battle.Step(0,1)
return @{State=$state;Actor=$actor;Event=$step.Event}
}
$f=Invoke-FinishFixture 'baseline'
$state=$f.State;$actor=$f.Actor;$step=@{Event=$f.Event}

$own=@($step.Event.DamageEvents|Where-Object SkillEntityId -eq 13960)
$heals=@($step.Event.DamageEvents|Where-Object SkillEntityId -eq 13965)
$total=($own|Measure-Object Damage -Sum).Sum
Assert-Finish (@($own|Where-Object TargetId -eq 2).Count -eq 6 -and @($own|Where-Object TargetId -eq 3).Count -eq 1) ('The original six body cells and additional target were not settled: '+($own.TargetId -join ','))
Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq [long][Math]::Truncate(-$total*.5)) ('Whole-node recovery differs: '+$total+' / '+($heals.Damage -join ','))
Assert-Finish ($heals[0].SourceId -eq 1 -and $heals[0].TargetId -eq 1 -and [Math]::Abs($heals[0].Delay-1) -lt .0001) 'Recovery caster/target or original hit timing differs.'
Assert-Finish ($actor.SkillState.Slots[0].ReleasedTimes -eq 1 -and @($step.Event.SkillCasts|Where-Object SkillEntityId -eq 13965).Count -eq 1) 'Recovery consumed extra ordinary releases or was repeated per target.'
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
Assert-Finish ($restored.Units[0].Hp -eq $actor.Hp -and $restored.RandomSeed -eq $state.RandomSeed -and @($restored.Receipts[0].Event.DamageEvents|Where-Object SkillEntityId -eq 13965).Count -eq 1) 'Recovery checkpoint differs.'
Write-Output ('Original 13960 executor: seven body/target occurrences, one actual-HP-based 13965 recovery ('+$heals[0].Damage+'), timing, ordinary release and checkpoint passed.')

foreach($mode in @('partial','full','curemods','cap','policy','multiple','child','removed','late','dodge','blind','dead','curedebuff','celldelay','conversion','conversionFull','conversionBanned')){
 $f=Invoke-FinishFixture $mode
 $own=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 13960)
 $heals=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 13965)
 $total= -($own|Measure-Object Damage -Sum).Sum
 switch($mode){
 'conversion'{
  $shield=@($f.Actor.Statuses|Where-Object ClassId -eq 24435)
  Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq 0 -and $shield.Count -eq 1 -and $shield[0].ShieldRemainValue -eq 457 -and $f.Actor.Hp -eq 1000) '13960 recovery did not honor the explicit 25136 whitelist and post-modifier shield conversion.'
 }
 'conversionFull'{
  $shield=@($f.Actor.Statuses|Where-Object ClassId -eq 24435)
  Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq 0 -and $shield.Count -eq 1 -and $shield[0].ShieldRemainValue -eq 457 -and $f.Actor.Hp -eq $f.Actor.MaxHp) 'Full-HP 13960 recovery was clipped before shield conversion.'
 }
 'conversionBanned'{Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq 0 -and @($f.Actor.Statuses|Where-Object ClassId -eq 24435).Count -eq 0 -and $f.Actor.Hp -eq 1000) '13960 recovery converted to shield before the lower-order prohibition.'}
 'curedebuff'{Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq 176) 'Ordinary cure reduction was bypassed.'}
 'celldelay'{Assert-Finish ($heals.Count -eq 1 -and [Math]::Abs($heals[0].Delay-1.4) -lt .0001 -and $heals[0].Damage -eq 353) 'Recovery ran before the final delayed body-cell settlement.'}
 'dodge'{Assert-Finish ($own.Count -gt 0 -and @($own|Where-Object {-not $_.Dodge}).Count -eq 0 -and $total -eq 0 -and $heals.Count -eq 0) 'Dodges produced recovery.'}
 'blind'{Assert-Finish ($own.Count -gt 0 -and @($own|Where-Object {-not $_.Blinding}).Count -eq 0 -and $total -eq 0 -and $heals.Count -eq 0) 'Blinded misses produced recovery.'}
 'dead'{Assert-Finish ($total -gt 0 -and $f.Actor.Hp -eq 0 -and $heals.Count -eq 0) 'A dead source cast a recovery child.'}
 'partial'{
  Assert-Finish (($own|Measure-Object ShieldDamage -Sum).Sum -eq 200 -and @($own|Where-Object TargetId -eq 3)[0].Damage -eq -37) 'Shield/overkill fixture did not exercise actual HP clipping.'
  Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq [long][Math]::Truncate($total*.5) -and $total -lt 707) 'Shield absorption or overkill polluted actual HP recovery.'
 }
 'full'{Assert-Finish ($total -eq 0 -and $heals.Count -eq 0) 'Fully shielded hit spawned a zero recovery child.'}
 'curemods'{Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq 635) ('Original cure modifiers were bypassed: '+($heals.Damage -join ','))}
 'cap'{Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq 10 -and $heals[0].DamageValue -eq 353 -and $f.Actor.Hp -eq $f.Actor.MaxHp) 'Recovery bypassed the ordinary HP cap.'}
 'policy'{Assert-Finish ($total -gt 0 -and $heals.Count -eq 0) 'Hit-finish bypassed CanTriggerChild.'}
 'multiple'{Assert-Finish ($heals.Count -eq 2 -and $heals[0].Damage -eq 353 -and $heals[1].Damage -eq 353 -and [Math]::Abs($heals[1].Delay-2) -lt .0001) 'Multiple original hit nodes shared damage or recovery totals.'}
 'child'{
  $children=@($f.Event.DamageEvents|Where-Object SkillEntityId -eq 13745)
  Assert-Finish ($children.Count -gt 0 -and @($children|Where-Object {$_.Damage -lt 0}).Count -gt 0) 'Nested damage exclusion fixture did not cause child damage.'
  Assert-Finish ($heals.Count -eq 1 -and $heals[0].Damage -eq [long][Math]::Truncate($total*.5)) 'Other child skill damage was included in hit-finish recovery.'
 }
 'removed'{Assert-Finish ($heals.Count -eq 1 -and $f.Actor.Statuses.Count -eq 0) 'Removing the condition after cast start invalidated its snapshot.'}
 'late'{Assert-Finish ($heals.Count -eq 0 -and $f.Actor.Statuses.Count -gt 0) 'Adding the condition after cast start changed its snapshot.'}
 }
 Write-Output ('Original 13960 executor case '+$mode+' passed: parent HP loss '+$total+', healing '+($heals.Damage -join ','))
}

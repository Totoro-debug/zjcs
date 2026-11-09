$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path @(Get-FightCombatSources)
$skillPath=$root+'/unity/Assets/Official/Combat/skill-rules.json';$battlePath=$root+'/unity/Assets/Official/Combat/battle-rules.json'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load([IO.File]::ReadAllText($skillPath),(Get-FileHash $skillPath).Hash.ToLowerInvariant(),'854_156383',[IO.File]::ReadAllText($battlePath),(Get-FileHash $battlePath).Hash.ToLowerInvariant(),'854_156383')
function Assert-Scale($value,$message){if(-not $value){throw $message}}
function New-Unit($id,$side,$x,$y){
 $u=[Zjcs.Combat.Official.FightBattleUnitState]::new();$u.Id=$id;$u.Side=$side;$u.CharacterType=1;$u.X=$x;$u.Y=$y
 $u.Hp=$u.MaxHp=$u.LastMaxHp=100000;$u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=100-$id;return $u
}
$actor=New-Unit 1 1 4 4;$target=New-Unit 2 2 4 5
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($actor);$units.Add($target)
$random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$logicType=[Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$create=$logicType.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$scale=$logicType.GetMethod('OnTransientDamage',$flags)
function New-Logic($skill){$create.Invoke($null,@($actor,$skill,$units,$catalog,$random,$null))}
$collect=$logicType.GetMethod('CollectChildSkills',$flags)
function Field($value,$name){$value.GetType().GetField($name,$flags).GetValue($value)}
function Get-Requests($skill){
 $logic=New-Logic $skill;$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
 return $collect.Invoke($logic,@($hit,$target,$null))
}
function Request-Ids($requests){@($requests|ForEach-Object {(Field $_ 'Target').Id})}
$skill=$catalog.CreateSkill(13640,0,10204,5,20,3,$null)
$actor.Hp=$actor.Turn.Hp=100000
$target=New-Unit 2 1 4 5;$target.Hp=20000
$third=New-Unit 3 1 4 6;$third.Hp=10000
$fourth=New-Unit 4 1 4 7;$fourth.Hp=10000
$enemy=New-Unit 5 2 4 8;$enemy.Hp=1
$units.Clear();foreach($u in @($actor,$target,$third,$fourth,$enemy)){$units.Add($u)}
$seed=$random.Seed;$requests=@(Get-Requests $skill)
Assert-Scale ((Request-Ids $requests) -join ',' -eq '3,4') 'Lowest HP sorting or stable tie order differs.'
Assert-Scale ($random.Seed -eq $seed -and @($requests|Where-Object {(Field $_ 'SkillId') -ne 13643 -or [Math]::Abs((Field $_ 'Delay')-.62) -gt .0001}).Count -eq 0) 'The graph consumed random state or lost original child/delay.'
$third.Hp=90000
Assert-Scale ((Request-Ids $requests) -join ',' -eq '3,4') 'Dispatch requests retained a live ordering instead of a collection snapshot.'
$third.Hp=10000
$boundary=New-Unit 6 1 12 4;$boundary.Hp=5000
$boundary.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(-1,0));$boundary.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0))
$units.Add($boundary);$units.Add($boundary)
Assert-Scale ((Request-Ids @(Get-Requests $skill)) -join ',' -eq '6,3') 'A large target boundary body cell was missed or collected twice.'
$boundary.X=13
Assert-Scale ((Request-Ids @(Get-Requests $skill)) -join ',' -eq '3,4') 'An outside body entered the original range.'
$third.Ended=$true;$fourth.Targetable=$false
Assert-Scale ((Request-Ids @(Get-Requests $skill)) -join ',' -eq '2,1') 'Ended/untargetable targets leaked in or full HP was incorrectly excluded.'
$third.Ended=$false;$fourth.Targetable=$true;$third.Hp=0
Assert-Scale ((Request-Ids @(Get-Requests $skill)) -join ',' -eq '4,2') 'A dead candidate leaked into the collection.'
$third.MaxHp=$fourth.MaxHp=9000000000000000000
$third.Hp=8999999999999999999;$fourth.Hp=8999999999999999998
$target.Hp=$target.MaxHp
Assert-Scale ((Request-Ids @(Get-Requests $skill)) -join ',' -eq '4,3') 'Large HP ratios overflowed or were rounded into a false tie.'
foreach($fault in @('policy','port','sort','limit','range','body','trigger','filter')){
 $bad=$catalog.CreateSkill(13640,0,10204,5,20,3,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault){
  'policy'{$bad.CanTriggerChild=$false}
  'port'{$json.OnDoHit.HandleCfgs[0].SourceEntityKey.Id=3}
  'sort'{$json.OnDoHit.HandleCfgs[1].IsAscendingOrder=$false}
  'limit'{$json.OnDoHit.HandleCfgs[2].ForeachCount=3}
  'range'{$json.OnDoHit.HandleCfgs[0].FilterCondition.SubFilters[0].Range[0].X=99}
  'body'{$json.OnDoHit.HandleCfgs[0].FilterCondition.SubFilters[0].ConsiderTargetBodySize=$false}
  'trigger'{$json.OnDoHit.HandleCfgs[2].HandleCfgs[0].TargetEntityKey.Id=2003}
  'filter'{$json.OnDoHit.HandleCfgs[0].FilterCondition.SubFilters[1]._type='UnknownFilter'}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 50 -Compress
 $seed=$random.Seed
 Assert-Scale (@(Get-Requests $bad).Count -eq 0 -and $random.Seed -eq $seed) ('Rejected low-HP graph modified requests/random: '+$fault)
}
Write-Output '13640 collection capability: original range/body boundary, two entities, exact HP ratios, stable order, snapshot, full HP, dead/ended/targetability, child policy, original delay and malformed bindings passed.'

$rulesPath=$root+'/unity/Assets/Official/Combat/rules.json'
$formula=$catalog.CreateDamageFormula([IO.File]::ReadAllText($rulesPath),(Get-FileHash $rulesPath).Hash.ToLowerInvariant(),'854_156383')
function Invoke-LowHpBattle($mode='normal'){
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=14;$state.Grid.Height=14;$state.Grid.DefaultGroundType=2
 $source=New-Unit 1 1 4 4;$ally=New-Unit 2 1 4 5;$low=New-Unit 3 1 5 4;$other=New-Unit 4 1 3 4;$enemy=New-Unit 5 2 6 6
 $ally.Hp=$ally.Turn.Hp=50000;$low.Hp=$low.Turn.Hp=10000;$other.Hp=$other.Turn.Hp=20000
 $source.Turn.Speed=1000
 foreach($u in @($source,$ally,$low,$other,$enemy)){
  foreach($pair in @(@('MaxHp',100000),@('Attack',1000),@('BaseCureAdd',10000),@('BaseBeCureAdd',10000))){
   $p=[Zjcs.Combat.Official.FightPropertyValue]::new();$p.Id=$catalog.PropertyType($pair[0]);$p.Value=$pair[1];$u.Properties.Add($p)
  }
  $state.Units.Add($u)
 }
 if($mode -eq 'full'){
  $low.Hp=$low.Turn.Hp=$other.Hp=$other.Turn.Hp=100000
  $source.Statuses.Add($catalog.CreateStatus(25136,1,1,11,13970,10209,7,30,3,$null))
 }
 $props=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
 foreach($row in $catalog.CalculateSkillProps(13640,5,20,3).GetEnumerator()){$props.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$row.Key;Value=$row.Value})}
 $active=$catalog.CreateSkill(13640,0,10204,5,20,3,$props)
 $source.Skills.Add($active);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(5);$state.Waves.Add($wave)
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
 $step=$battle.Step(0,1)
 $children=@($step.Event.SkillCasts|Where-Object SkillEntityId -eq 13643)
 $expected=if($mode -eq 'full'){'2,1'}else{'3,4'}
 $actualTargets=($children|ForEach-Object {$_.TargetIds[0]}) -join ','
 Assert-Scale ($children.Count -eq 2 -and $actualTargets -eq $expected) ('Real low-HP child target selection differs: '+$mode+' / targets='+$actualTargets+' / casts='+($step.Event.SkillCasts.SkillEntityId -join ','))
 Assert-Scale (@($children|Where-Object {$_.ActorId -ne 1 -or $_.Slot -ne -1 -or $_.SkillItemId -ne 10204}).Count -eq 0 -and $source.SkillState.Slots[0].ReleasedTimes -eq 1) 'Root identity or ordinary release differs.'
 $heals=@($step.Event.DamageEvents|Where-Object SkillEntityId -eq 13643)
 if($heals.Count -ne 2 -or $heals[0].Damage -le 0){
  Write-Output ('Root properties: '+(($active.Properties|ForEach-Object {''+$_.Id+'='+$_.Value}) -join ','))
  Write-Output ('Child properties: '+(($catalog.CalculateSkillProps(13643,5,20,3).GetEnumerator()|ForEach-Object {''+$_.Key+'='+$_.Value}) -join ','))
  Write-Output ('Source properties: '+(($source.Properties|ForEach-Object {''+$_.Id+'='+$_.Value}) -join ','))
  $heals|Select-Object TargetId,Damage,AbsoluteDamage,BeforeHp,AfterHp,IsCure|Format-List|Out-Host
  $state.Units|Select-Object Id,Hp,MaxHp|Format-Table|Out-Host
 }
 Assert-Scale ($heals.Count -eq 2 -and $heals[0].Damage -gt 0) ('Original per-target recovery did not execute: count='+$heals.Count+' / amounts='+($heals.Damage -join ',')+' / allSkills='+($step.Event.DamageEvents.SkillEntityId -join ',')+' / targets='+($heals.TargetId -join ','))
 if($mode -eq 'full'){
  $shield=@($source.Statuses|Where-Object ClassId -eq 24435)
  Assert-Scale ($heals[1].Damage -eq 0 -and $source.Hp -eq $source.MaxHp -and $shield.Count -eq 1 -and $shield[0].ShieldRemainValue -gt 0) 'Full-HP selected target did not convert its original cure to shield.'
 }else{Assert-Scale ($heals[1].Damage -gt 0 -and $low.Hp -gt 10000 -and $other.Hp -gt 20000 -and $ally.Hp -eq 50000) 'The wrong candidates received recovery.'}
 $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
 Assert-Scale ($saved.RandomSeed -eq $state.RandomSeed -and $saved.Units[0].SkillState.Slots[0].ReleasedTimes -eq 1 -and $saved.Receipts[-1].Event.SkillCasts.Count -eq $step.Event.SkillCasts.Count) 'Low-HP graph checkpoint differs.'
 Write-Output ('Original 13640 executor '+$mode+': two ordered explicit 13643 targets '+$expected+', root inheritance, cure/shield settlement, one release and checkpoint passed.')
}
Invoke-LowHpBattle
Invoke-LowHpBattle 'full'

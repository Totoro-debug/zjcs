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
function Invoke-Scale($logic,$skill,[long]$amount=-101,[bool]$isCure=$false){
 $hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
 $context=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),$amount,[Math]::Abs($amount),$isCure)
 $null=$scale.Invoke($logic,@($target,$hit,$context));return $context
}
foreach($id in @(13870,13970)){
 $actor.Statuses.Clear();$target.Hp=20000
 $status=$catalog.CreateStatus(24721,1,1,1);$actor.Statuses.Add($status)
 $skill=$catalog.CreateSkill($id,0,0,1,1,1,$null);$logic=New-Logic $skill
 $seed=$random.Seed;$ctx=Invoke-Scale $logic $skill
 Assert-Scale ($ctx.Damage -eq -151 -and $ctx.AbsoluteDamage -eq 151 -and $random.Seed -eq $seed) ('Conditional scale, truncation or random stream differs: '+$id)
 $bb=$logicType.GetField('blackboard',$flags).GetValue($logic)
 Assert-Scale ($bb[563722184] -eq 1 -and $bb[122229964] -eq 50) 'Conditional count or absolute difference differs.'
 Assert-Scale ((Invoke-Scale $logic $skill 0).Damage -eq 0) 'Zero damage became positive.'
 Assert-Scale ((Invoke-Scale $logic $skill -101 $true).Damage -eq -101) 'Damage condition amplified healing.'
 $skill.CanTriggerChild=$false
 Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -151) 'A numeric graph incorrectly depended on child permission.'
 if($id -eq 13870){
  $target.Hp=30001
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -101) 'HP above the original threshold was amplified.'
  $target.Hp=29999
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -151) 'HP was not reread after an earlier occurrence.'
  $target.Hp=30000
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -151) 'The original float threshold was replaced with a rounded decimal constant.'
 }else{
  $status.ForbiddenType=1
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -101) 'Forbidden eligibility status remained active.'
  $status.ForbiddenType=0;$status.StackCount=0
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -101) 'Zero-stack status remained active.'
  $actor.Statuses.Clear()
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -101) 'Removed status remained snapshotted.'
  $status.StackCount=1;$actor.Statuses.Add($status)
  Assert-Scale ((Invoke-Scale $logic $skill).Damage -eq -151) 'New eligibility status was not reread.'
 }
 foreach($fault in @('port','count','scale','filter','relation')){
  $bad=$catalog.CreateSkill($id,0,0,1,1,1,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
  switch($fault){
   'port'{$json.OnDoHitTransient.HandleCfgs[1].DamageKey.Id=1026}
   'count'{$json.OnDoHitTransient.HandleCfgs[0].Value=2}
   'scale'{$json.OnDoHitTransient.HandleCfgs[1].ScaleValue=700}
   'filter'{$json.OnDoHitTransient.FilterCondition._type='UnknownFilter'}
   'relation'{if($id -eq 13870){$json.OnDoHitTransient.FilterCondition.Relation=1}else{$json.OnDoHitTransient.FilterCondition.EntityKey.Id=2002}}
  }
  $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 40 -Compress
  Assert-Scale ((Invoke-Scale (New-Logic $bad) $bad).Damage -eq -101) ('Malformed conditional graph changed damage: '+$id+'/'+$fault)
 }
 Write-Output ('Original '+$id+' conditional damage graph: original float/status condition, current occurrence, truncation, delta, zero, healing, child flag, stream and malformed bindings passed.')
}

$rulesPath=$root+'/unity/Assets/Official/Combat/rules.json'
$formula=$catalog.CreateDamageFormula([IO.File]::ReadAllText($rulesPath),(Get-FileHash $rulesPath).Hash.ToLowerInvariant(),'854_156383')
function Invoke-ConditionalBattle($id,$eligible,$shielded=$false,$wide=$false){
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=12;$state.Grid.Height=12;$state.Grid.DefaultGroundType=2
 $source=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 5
 if($wide){foreach($x in -1..1){$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,0))}}
 $source.Turn.Speed=1000;$enemy.Turn.Speed=1
 foreach($u in @($source,$enemy)){
  foreach($pair in @(@('MaxHp',100000),@('Attack',1000))){$p=[Zjcs.Combat.Official.FightPropertyValue]::new();$p.Id=$catalog.PropertyType($pair[0]);$p.Value=$pair[1];$u.Properties.Add($p)}
 }
 if($id -eq 13870){$enemy.Hp=$enemy.Turn.Hp=if($eligible){20000}else{100000}}
 elseif($eligible){$source.Statuses.Add($catalog.CreateStatus(24721,1,1,1))}
 if($shielded){
  $shield=[Zjcs.Combat.Official.FightBattleStatusState]::new();$shield.ClassId=999999;$shield.InstanceId=99
  $shield.ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent;$shield.ShieldInitialized=$true;$shield.ShieldRemainValue=100000
  $enemy.Statuses.Add($shield)
 }
 $active=$catalog.CreateSkill($id,0,0,1,1,1,$null);$active.HasCustomDamage=$true;$active.CustomDamage=101
 $source.Skills.Add($active);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 $state.Units.Add($source);$state.Units.Add($enemy)
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$state.Waves.Add($wave)
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
 $step=$battle.Step(0,1);$hits=@($step.Event.DamageEvents|Where-Object {$_.SourceId -eq 1 -and $_.SkillEntityId -eq $id})
 Assert-Scale ($hits.Count -gt 0 -and $source.SkillState.Slots[0].ReleasedTimes -eq 1) ('Original condition fixture did not cast once: '+$id)
 $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
 Assert-Scale ($saved.RandomSeed -eq $state.RandomSeed -and $saved.Units[1].Hp -eq $enemy.Hp) 'Conditional damage checkpoint differs.'
 return $hits
}
$widePlain=@(Invoke-ConditionalBattle 13970 $false $false $true)
$wideScaled=@(Invoke-ConditionalBattle 13970 $true $false $true)
Assert-Scale ($widePlain.Count -eq 3 -and $wideScaled.Count -eq 3 -and @($wideScaled|Where-Object {-not $_.HasHitGridPosition}).Count -eq 0) 'Original six-cell scope did not settle each covered body cell.'
for($i=0;$i -lt 3;$i++){
 Assert-Scale ($wideScaled[$i].Damage -eq [long][decimal]::Truncate([decimal]$widePlain[$i].Damage*1.5)) 'An area cell lost its conditional amplification.'
}
Write-Output 'Original 13970 area graph: three covered cells of one large target settle independently with their own conditional damage.'
foreach($id in @(13870,13970)){
 $plain=@(Invoke-ConditionalBattle $id $false);$scaled=@(Invoke-ConditionalBattle $id $true)
 Assert-Scale ($plain.Count -eq $scaled.Count) 'Numeric graph changed original hit count.'
 for($i=0;$i -lt $plain.Count;$i++){
  Assert-Scale ($scaled[$i].Damage -eq [long][decimal]::Truncate([decimal]$plain[$i].Damage*1.5)) ('Real conditional damage/order differs: '+$id)
 }
 $shieldPlain=@(Invoke-ConditionalBattle $id $false $true);$shieldScaled=@(Invoke-ConditionalBattle $id $true $true)
 Assert-Scale ($shieldScaled[0].ShieldDamage -eq [long][decimal]::Truncate([decimal]$shieldPlain[0].ShieldDamage*1.5) -and $shieldScaled[0].BeforeHp -eq $shieldScaled[0].AfterHp) 'Shield absorbed the unscaled conditional damage.'
 Write-Output ('Original '+$id+' executor: conditional damage, existing shield settlement, one release and checkpoint passed; occurrences='+$scaled.Count)
}

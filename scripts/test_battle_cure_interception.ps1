. (Join-Path $PSScriptRoot 'test_battle_skill_hit_finish.ps1')
function Invoke-CureFixture($mode){
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=12;$state.Grid.Height=12;$state.Grid.DefaultGroundType=2
 $actor=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 6
 $actor.MaxHp=$actor.LastMaxHp=1000;$actor.Hp=$actor.Turn.Hp=500
 Add-Prop $actor 'BaseCureAdd' 10000;Add-Prop $actor 'BaseBeCureAdd' 10000
 $active=$catalog.CreateSkill(13965,0,10208,5,20,3,$null);$active.HasCustomCure=$true;$active.CustomCure=101
 $actor.Skills.Add($active);$actor.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 $conversion=$catalog.CreateStatus(25136,1,2,11,13970,10209,7,30,3,$null)
 $actor.Statuses.Add($conversion)
 $c=$conversion.Components.CureReduce
 Assert-Finish ($null -ne $c -and $c.DefinitionResolved -and $c.ExecuteOrder -eq 1001 -and $c.IsCheckSkillTrigger -and $c.StatusTriggers[0].EntityId -eq 24435) 'Original conversion configuration is missing.'
 switch($mode){
  'full'{$actor.Hp=$actor.Turn.Hp=$actor.MaxHp}
  'banned'{$actor.Statuses.Clear();$actor.Statuses.Add($catalog.CreateStatus(24223,1,2,12))}
  'both'{$actor.Statuses.Add($catalog.CreateStatus(24223,1,2,12))}
  'tieConversionFirst'{
   $ban=$catalog.CreateStatus(24223,1,2,12);$ban.Components.CureReduce.ExecuteOrder=1001
   $actor.Statuses.Add($ban);$c.ReduceCurePer=.5
  }
  'tieBanFirst'{
   $ban=$catalog.CreateStatus(24223,1,2,12);$ban.Components.CureReduce.ExecuteOrder=1001
   $actor.Statuses.Insert(0,$ban);$c.ReduceCurePer=.5
  }
  'tagged'{$active.GameplayTags=[long[]]@(256,0,0,0)}
  'unlisted'{$active.CanTriggerStatusEntityIds.Clear()}
  'forbidden'{$conversion.ForbiddenType=1}
  'zeroStack'{$conversion.StackCount=0}
  'zeroRate'{$c.Rate=0}
  'partial'{$c.ReduceCurePer=.33}
  'roundZero'{$c.ReduceCurePer=.001}
  'negative'{$c.ReduceCurePer=-.5}
  'tooLarge'{$c.ReduceCurePer=2}
  'unsupported'{$c.HasUnsupportedFilter=$true}
  'older'{$conversion.Components.CureReduce=$null}
 }
 $state.Units.Add($actor);$state.Units.Add($enemy)
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$state.Waves.Add($wave)
 if($mode -eq 'older'){
  $state=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
  $actor=$state.Units[0]
 }
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
 if($mode -eq 'full'){
  # A direct triggered cure executes even when ordinary healing AI would skip a full target.
  $targets=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$targets.Add($actor)
  $casts=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleSkillCastEvent]]::new()
  $damage=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleDamageEvent]]::new()
  $moves=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleMovementEvent]]::new()
  $method=[Zjcs.Combat.Official.FightBattleExecutor].GetMethods([Reflection.BindingFlags]'Instance,NonPublic')|Where-Object { $_.Name -eq 'ExecuteSkill' -and $_.GetParameters().Length -eq 14 }
  $args=[object[]]@($actor,$active,$targets,$casts,$damage,$moves,[float]0,[float]0,[float]0,$actor,$false,[double]1,$null,$null)
  $null=$method.Invoke($battle,$args)
  $step=@{Event=@{DamageEvents=$damage}}
 }else{$step=$battle.Step(0,1)}
 $heals=@($step.Event.DamageEvents|Where-Object SkillEntityId -eq 13965)
 $shields=@($actor.Statuses|Where-Object ClassId -eq 24435)
 Assert-Finish ($heals.Count -eq 1 -and $heals[0].IsCure -and $heals[0].TargetId -eq 1) ('Standalone original cure was not executed: '+$mode)
 return @{State=$state;Actor=$actor;Event=$step.Event;Heal=$heals[0];Shields=$shields}
}

foreach($mode in @('conversion','full','banned','both','tagged','unlisted','forbidden','zeroStack','zeroRate','partial','roundZero','negative','tooLarge','unsupported','older','tieConversionFirst','tieBanFirst')){
 $f=Invoke-CureFixture $mode
 # Original 25136 also modifies healing through DamageProcess property 2000.
 # Its effective cultivated modifier resolves the 101 custom base to 130 before interception.
 $expectedShield=130;$expectedHeal=0
 if($mode -in @('banned','both')){$expectedShield=0}
 if($mode -in @('tagged','unlisted','forbidden','zeroStack','zeroRate','roundZero','negative','unsupported')){$expectedShield=0;$expectedHeal=130}
 if($mode -eq 'partial'){$expectedShield=42;$expectedHeal=88}
 if($mode -eq 'forbidden'){$expectedHeal=101}
 if($mode -eq 'tieConversionFirst'){$expectedShield=65}
 if($mode -eq 'tieBanFirst'){$expectedShield=0}
 Assert-Finish ($f.Heal.Damage -eq $expectedHeal -and $f.Shields.Count -eq [int]($expectedShield -gt 0)) ('Cure interception result differs: '+$mode+' / heal='+$f.Heal.Damage+' / shields='+$f.Shields.Count)
 if($expectedShield -gt 0){
  $shield=$f.Shields[0]
  Assert-Finish ($shield.ShieldRemainValue -eq $expectedShield -and $shield.CustomShieldValue -eq $expectedShield -and $shield.CreatorId -eq 2 -and $shield.OwnerId -eq 1 -and $shield.SourceSkillRank -eq 7 -and $shield.SourceSkillLevel -eq 30 -and $shield.ApplyingSkillItemId -eq 10209 -and $shield.Origin.SkillEntityId -eq 13970) ('Converted custom parameter or original status ownership/cultivation differs: '+$mode)
 }
 $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State))
 Assert-Finish ($saved.Units[0].Hp -eq $f.Actor.Hp -and @($saved.Units[0].Statuses|Where-Object ClassId -eq 24435).Count -eq $f.Shields.Count) 'Converted cure/shield checkpoint differs.'
 Write-Output ('Cure interception '+$mode+' passed: HP +'+$f.Heal.Damage+', shield '+$expectedShield)
}

$status=$catalog.CreateStatus(25136,1,2,1,13970,10209,7,30,3,$null)
$copy=$status.Components.Copy()
$copy.CureReduce.SourceSkillTagQuery[0].Tags[0]=0
$copy.CureReduce.StatusTriggers[0].EntityId=999
Assert-Finish ($status.Components.CureReduce.SourceSkillTagQuery[0].Tags[0] -eq 256 -and $status.Components.CureReduce.StatusTriggers[0].EntityId -eq 24435) 'Cure interception copies share mutable masks or triggers.'
Write-Output 'Original cure reduction: whitelist, no-tag Without, explicit exclusion tag, prohibition priority, full-HP conversion, truncation/clamping, unsupported filter, old checkpoint and deep copy passed.'

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)
$root = Split-Path -Parent $PSScriptRoot
$skillPath = Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json'
$battlePath = Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText($skillPath), (Get-FileHash $skillPath -Algorithm SHA256).Hash.ToLowerInvariant(), '854_156383',
    [IO.File]::ReadAllText($battlePath), (Get-FileHash $battlePath -Algorithm SHA256).Hash.ToLowerInvariant(), '854_156383')
function Assert-Logic($condition, $label) { if (-not $condition) { throw $label } }
function New-Unit($id, $side, $x, $y) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id=$id; $unit.Side=$side; $unit.CharacterType=1; $unit.X=$x; $unit.Y=$y
    $unit.Hp=$unit.MaxHp=$unit.LastMaxHp=1000000
    $unit.Turn.Id=$id; $unit.Turn.Hp=$unit.Hp; $unit.Turn.Speed=100-$id
    return $unit
}
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$settlement = [Zjcs.Combat.Official.FightDamageSettlement]::new()
$hitExecutor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new($formula,$settlement)
$logicType = [Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime', $true)
$create = $logicType.GetMethod('Create', [Reflection.BindingFlags]'Static,NonPublic')
$transient = $logicType.GetMethod('OnDoHitTransient', [Reflection.BindingFlags]'Instance,NonPublic')
$random = [Zjcs.Combat.Official.FightRandomStream]::new(91)
# Confirmed 13820 charge snapshot and scale.
$start=$logicType.GetMethod('OnSkillStart',[Reflection.BindingFlags]'Instance,NonPublic')
$scale=$logicType.GetMethod('OnTransientDamage',[Reflection.BindingFlags]'Instance,NonPublic')
$charged=New-Unit 11 1 4 4; $victim=New-Unit 12 2 4 5
$chargeUnits=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$chargeUnits.Add($charged);$chargeUnits.Add($victim)
$a=$catalog.CreateStatus(24031,11,11,101);$a.StackCount=2
$b=$catalog.CreateStatus(24031,11,99,102);$b.StackCount=3
$forbidden=$catalog.CreateStatus(24031,11,11,103);$forbidden.StackCount=4;$forbidden.ForbiddenType=1
$charged.Statuses.Add($a);$charged.Statuses.Add($b);$charged.Statuses.Add($forbidden)
$blast=$catalog.CreateSkill(13820,0,10196,5,20,1,$null)
$blastHit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($blast)[0]
$blastLogic=$create.Invoke($null,@($charged,$blast,$chargeUnits,$catalog,$random))
Assert-Logic ($start.Invoke($blastLogic,@())) 'Charge start graph failed.'
Assert-Logic (-not $charged.Statuses.Contains($a) -and -not $charged.Statuses.Contains($b) -and $charged.Statuses.Contains($forbidden)) 'Snapshot did not consume all and only valid source instances.'
$later=$catalog.CreateStatus(24031,11,11,104);$later.StackCount=1;$charged.Statuses.Add($later)
Assert-Logic (-not $start.Invoke($blastLogic,@()) -and $later.StackCount -eq 1) 'Repeated start consumed newly gained charge.'
$context=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),-101,101,$false)
$scale.Invoke($blastLogic,@($victim,$blastHit,$context))|Out-Null
Assert-Logic ($context.Damage -eq -136 -and $context.AbsoluteDamage -eq 136) 'Five-charge signed damage did not truncate toward zero.'
$castBB=$logicType.GetField('blackboard',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($blastLogic)
Assert-Logic ($castBB[122229964] -eq 35) 'DeltaDamage did not record absolute new-old difference.'
$otherLogic=$create.Invoke($null,@($charged,$blast,$chargeUnits,$catalog,$random))
Assert-Logic ($start.Invoke($otherLogic,@())) 'A second cast did not capture new charge.'
$one=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),-101,101,$false)
$scale.Invoke($otherLogic,@($victim,$blastHit,$one))|Out-Null
Assert-Logic ($one.Damage -eq -108 -and $one.AbsoluteDamage -eq 108) 'One-charge rational scaling or cast isolation failed.'
$cure=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),-101,101,$true)
$scale.Invoke($otherLogic,@($victim,$blastHit,$cure))|Out-Null
Assert-Logic ($cure.Damage -eq -101 -and $cure.AbsoluteDamage -eq 101) 'Damage graph modified healing.'
foreach($flag in @('Dodge','Blinding','Immunity')){
 $t=[Zjcs.Combat.Official.FightDamageTypeData]::new();$t.$flag=$true
 $zero=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new($t,0,0,$false)
 $scale.Invoke($otherLogic,@($victim,$blastHit,$zero))|Out-Null
 Assert-Logic ($zero.Damage -eq 0 -and $zero.AbsoluteDamage -eq 0) 'Zero damage became nonzero.'
}
$max=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),[long]::MinValue,[long]::MaxValue,$false)
$scale.Invoke($otherLogic,@($victim,$blastHit,$max))|Out-Null
Assert-Logic ($max.Damage -eq [long]::MinValue -and $max.AbsoluteDamage -eq [long]::MaxValue) 'Scaling overflow was not saturated.'
# Actual shield settlement consumes the amplified value, preserving pre-HP timing.
$shield=[Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ClassId=999999;$shield.InstanceId=105;$shield.ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$shield.ShieldInitialized=$true;$shield.ShieldRemainValue=100000;$victim.Statuses.Add($shield)
$blastHit.HasCustomDamage=$true;$blastHit.CustomDamage=101
$damageCallback=[Action[Zjcs.Combat.Official.FightBattleTransientDamageContext]]{param($ctx)$scale.Invoke($otherLogic,@($victim,$blastHit,$ctx))|Out-Null}
$unscaled=$hitExecutor.Apply($random,10,$charged,$victim,$blast,$blastHit,0,$false,0,$null,$false,1,$null,$null,$null)
$shield.ShieldRemainValue=100000
$scaled=$hitExecutor.Apply($random,11,$charged,$victim,$blast,$blastHit,0,$false,0,$null,$false,1,$null,$null,$damageCallback)
Assert-Logic ($scaled.ShieldDamage -eq [long][decimal]::Truncate([decimal]$unscaled.ShieldDamage*107/100) -and $victim.Hp -eq 1000000) 'Shield did not absorb amplified damage.'
Write-Output 'Charge snapshot/consumption, valid sources, later gains, cast isolation, signed rounding, DeltaDamage, zero, cure, saturation and amplified shield checks passed.'

function Invoke-RealChargeBlast([int]$layers, [string]$missProperty = $null, [bool]$invalidTarget = $false) {
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
 $state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=10;$state.Grid.Height=10;$state.Grid.DefaultGroundType=2
 $source=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 5
 if($invalidTarget){$enemy.X=9;$enemy.Y=9}
 $active=$catalog.CreateSkill(13820,0,10196,5,20,1,$null)
 $active.HasCustomDamage=$true;$active.CustomDamage=101
 $source.Skills.Add($active);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 if($layers -gt 0){$stored=$catalog.CreateStatus(24031,1,1,1);$stored.StackCount=$layers;$source.Statuses.Add($stored)}
 $state.Units.Add($source);$state.Units.Add($enemy)
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.Index=0;$wave.UnitIds.Add(2);$state.Waves.Add($wave)
 $battleFormula=$formula
 if($missProperty){
  $schema=[Zjcs.Combat.Official.FightDamagePropertySchema]::new()
  $name=[Zjcs.Combat.Official.FightPropertyName]::new();$name.Id=999997;$name.Name=$missProperty
  $schema.Names.Add($name);$schema.FloatPropertyIds.Add(999997)
  $value=[Zjcs.Combat.Official.FightPropertyValue]::new();$value.Id=999997;$value.Value=10000
  if($missProperty -eq 'DodgePercent'){$enemy.Properties.Add($value)}else{$source.Properties.Add($value)}
  $battleFormula=[Zjcs.Combat.Official.FightDamageFormula]::new($schema,[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
 }
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$battleFormula,$settlement,$catalog)
 $event=$battle.Step(0,1)
 if($invalidTarget){
  Assert-Logic (@($event.Event.SkillCasts|Where-Object SkillEntityId -eq 13820).Count -eq 0) 'Invalid-target fixture unexpectedly cast the blast.'
  Assert-Logic (($source.Statuses|Where-Object ClassId -eq 24031).StackCount -eq $layers) 'Unstarted cast consumed stored charges.'
  $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
  Assert-Logic (($restored.Units[0].Statuses|Where-Object ClassId -eq 24031).StackCount -eq $layers) 'Unconsumed charges were lost in checkpoint restore.'
  return 0
 }

 Assert-Logic (@($event.Event.DamageEvents|Where-Object SkillEntityId -eq 13820).Count -eq 1) 'Real 13820 did not execute its original hit.'
 Assert-Logic (-not ($source.Statuses|Where-Object ClassId -eq 24031)) 'Real cast did not consume original charge.'
 return 1000000-$enemy.Hp
}
$plain=Invoke-RealChargeBlast 0;$five=Invoke-RealChargeBlast 5
Assert-Logic ($plain -gt 0 -and $five -eq [long][decimal]::Truncate([decimal]$plain*135/100)) 'Executor did not use original per-cast charge snapshot at damage settlement.'
foreach($change in @(@('"StatusId":24031','"StatusId":9999'),@('"SourceEntityKey":{"Id":1,','"SourceEntityKey":{"Id":9999,'),@('FightHandleCfgGetStatusStackCount','UnknownReadHandler'))) {
 $owner=New-Unit 21 1 4 4;$stored=$catalog.CreateStatus(24031,21,21,1);$stored.StackCount=3;$owner.Statuses.Add($stored)
 $invalid=$catalog.CreateSkill(13820,0,10196,5,20,1,$null);$invalid.LogicDefinitionJson=$invalid.LogicDefinitionJson.Replace($change[0],$change[1])
 Assert-Logic ($invalid.LogicDefinitionJson.Contains($change[1])) 'Malformed charge fixture did not modify its graph.'
 $runtime=$create.Invoke($null,@($owner,$invalid,$chargeUnits,$catalog,$random))
 Assert-Logic (-not $start.Invoke($runtime,@()) -and $stored.StackCount -eq 3) 'Unsupported charge graph consumed layers.'
}
Write-Output 'Actual 13820 execution matches the charge-free damage control; malformed start graphs leave charges intact.'

# Consumption callbacks may add charge to an existing, not-yet-consumed instance.
# Only its pre-cast layers belong to the snapshot.
$owner=New-Unit 31 1 4 4
$first=$catalog.CreateStatus(24031,31,31,1);$first.StackCount=2
$second=$catalog.CreateStatus(24031,31,99,2);$second.StackCount=3
$listener=[Zjcs.Combat.Official.FightBattleStatusState]::new()
$listener.ClassId=999998;$listener.InstanceId=3;$listener.CreatorId=99;$listener.StackCount=1
$listener.HasConsumeStackCount=$true;$listener.ConsumeStackCountRulesSupported=$true
$listener.ConsumeStackCountTargetStatusId=24031;$listener.ConsumeStackCountThreshold=2
$onConsume=[Zjcs.Combat.Official.FightBattleStatusStateTrigger]::new()
$onConsume.EntityId=24031;$onConsume.BasePercent=1;$onConsume.Target=2
$listener.ConsumeStackCountStatuses.Add($onConsume)
$owner.Statuses.Add($first);$owner.Statuses.Add($second);$owner.Statuses.Add($listener)
$runtime=$create.Invoke($null,@($owner,$blast,$chargeUnits,$catalog,$random))
Assert-Logic ($start.Invoke($runtime,@())) 'Reentrant consumption fixture did not start.'
$remaining=@($owner.Statuses|Where-Object ClassId -eq 24031|ForEach-Object StackCount|Measure-Object -Sum)[0].Sum
Assert-Logic ($remaining -eq 2 -and $listener.ConsumeStackCountProgress -eq 1) 'Reentrant gains were consumed or notifications used the wrong actual layer count.'
$context=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),-100,100,$false)
$scale.Invoke($runtime,@($victim,$blastHit,$context))|Out-Null
Assert-Logic ($context.Damage -eq -135) 'Reentrant callbacks changed the cast snapshot.'
Write-Output 'Actual consumption notifications use the old five layers and preserve two reentrant gains.'

foreach($missProperty in @('DodgePercent','BlindingPercent')) {
 $miss=Invoke-RealChargeBlast 5 $missProperty
 Assert-Logic ($miss -eq 0) 'The actual missed cast did not stay at zero damage after charge consumption.'
}
Write-Output 'Real dodged and blinded blasts consume five layers and do not refund missed casts.'

$null=Invoke-RealChargeBlast 5 $null $true
Write-Output 'An unstarted invalid-target cast preserves all five charges and their checkpoint state.'

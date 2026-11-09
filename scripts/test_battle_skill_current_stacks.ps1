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

$scale=$logicType.GetMethod('OnTransientDamage',[Reflection.BindingFlags]'Instance,NonPublic')
$start=$logicType.GetMethod('OnSkillStart',[Reflection.BindingFlags]'Instance,NonPublic')
$actor=New-Unit 1 1 4 4;$target=New-Unit 2 2 4 5
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($actor);$units.Add($target)
$skill=$catalog.CreateSkill(50210,0,0,1,1,1,$null)
Assert-Logic (-not $skill.CanTriggerChild) 'Original 50210 child-trigger flag changed.'
$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
$a=$catalog.CreateStatus(24823,1,1,101);$a.StackCount=2;$actor.Statuses.Add($a)
$b=$catalog.CreateStatus(24823,1,99,102);$b.StackCount=3;$actor.Statuses.Add($b)
$forbidden=$catalog.CreateStatus(24823,1,1,103);$forbidden.StackCount=7;$forbidden.ForbiddenType=1;$actor.Statuses.Add($forbidden)
$targetOnly=$catalog.CreateStatus(24823,2,2,104);$targetOnly.StackCount=9;$target.Statuses.Add($targetOnly)
$logic=$create.Invoke($null,@($actor,$skill,$units,$catalog,$random,$null))
Assert-Logic ($null -ne $logic -and -not $start.Invoke($logic,@())) '50210 initialization fabricated a start graph.'
function Invoke-Segment($flow,[long]$damage=-101,[bool]$cure=$false) {
 $context=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),$damage,[Math]::Abs($damage),$cure)
 $scale.Invoke($flow,@($target,$hit,$context))|Out-Null
 return $context
}
$five=Invoke-Segment $logic
Assert-Logic ($five.Damage -eq -202 -and $five.AbsoluteDamage -eq 202 -and $a.StackCount -eq 2 -and $b.StackCount -eq 3) 'Five current valid layers were not read without consumption.'
$bb=$logicType.GetField('blackboard',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($logic)
Assert-Logic ($bb[719180550] -eq 5 -and $bb[122229964] -eq 101) 'Original local output or DeltaDamage differs.'
$a.StackCount=1;$actor.Statuses.Remove($b)|Out-Null
$one=Invoke-Segment $logic
Assert-Logic ($one.Damage -eq -121 -and $one.AbsoluteDamage -eq 121 -and $bb[719180550] -eq 1) 'The next segment reused an earlier layer snapshot or rounded away from zero.'
$b.StackCount=4;$actor.Statuses.Add($b)
$again=Invoke-Segment $logic
Assert-Logic ($again.Damage -eq -202 -and $actor.Statuses.Count -eq 3) 'New layers or status instances were not retained.'
$actor.Statuses.Clear()
$none=Invoke-Segment $logic
Assert-Logic ($none.Damage -eq -101 -and $bb[719180550] -eq 0 -and $bb[122229964] -eq 0) 'Zero layers reused old segment data or counted target layers.'
$actor.Statuses.Add($a)
$cure=Invoke-Segment $logic -101 $true
Assert-Logic ($cure.Damage -eq -101 -and $cure.AbsoluteDamage -eq 101) 'Damage scaling affected healing.'
foreach($flag in @('Dodge','Blinding','Immunity')) {
 $type=[Zjcs.Combat.Official.FightDamageTypeData]::new();$type.$flag=$true
 $zero=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new($type,0,0,$false)
 $scale.Invoke($logic,@($target,$hit,$zero))|Out-Null
 Assert-Logic ($zero.Damage -eq 0 -and $zero.AbsoluteDamage -eq 0) 'Zero damage became nonzero.'
}
$shield=[Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ClassId=999999;$shield.InstanceId=105;$shield.ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$shield.ShieldInitialized=$true;$shield.ShieldRemainValue=100000;$target.Statuses.Add($shield)
$hit.HasCustomDamage=$true;$hit.CustomDamage=101
$callback=[Action[Zjcs.Combat.Official.FightBattleTransientDamageContext]]{param($ctx)$scale.Invoke($logic,@($target,$hit,$ctx))|Out-Null}
$plain=$hitExecutor.Apply($random,10,$actor,$target,$skill,$hit,0,$false,0,$null,$false,1,$null,$null,$null)
$shield.ShieldRemainValue=100000
$scaled=$hitExecutor.Apply($random,11,$actor,$target,$skill,$hit,0,$false,0,$null,$false,1,$null,$null,$callback)
Assert-Logic ($scaled.ShieldDamage -eq [long][decimal]::Truncate([decimal]$plain.ShieldDamage*120/100) -and $target.Hp -eq 1000000) 'Shield did not absorb the current-layer amplified damage.'
$bad=$catalog.CreateSkill(50210,0,0,1,1,1,$null)
$bad.LogicDefinitionJson=$bad.LogicDefinitionJson.Replace('"StatusId":24823','"StatusId":24031')
Assert-Logic ($bad.LogicDefinitionJson -ne $skill.LogicDefinitionJson) 'Malformed-graph fixture did not modify the original read.'
$badFlow=$create.Invoke($null,@($actor,$bad,$units,$catalog,$random,$null))
$badHit=Invoke-Segment $badFlow
Assert-Logic ($badHit.Damage -eq -101) 'An unknown status-read graph modified damage.'
Write-Output '50210 current layers, source filtering, no consumption, per-segment refresh, child flag, rounding, DeltaDamage, zero, cure, shield and malformed-graph checks passed.'

function Invoke-RealStackedStrike([int]$layers, [bool]$support=$false) {
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
 $state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=10;$state.Grid.Height=10;$state.Grid.DefaultGroundType=2
 $source=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 5
 $slot=if($support){-2}else{0}
 $active=$catalog.CreateSkill(50210,$slot,16020,1,1,1,$null)
 $active.HasCustomDamage=$true;$active.CustomDamage=101
 if($support){
  $active.LimitedTimes=1
  $source.PetEnergy=[Zjcs.Combat.Official.FightBattlePetEnergyState]::new()
  $source.PetEnergy.OwnerUnitId=1;$source.PetEnergy.PetUnitId=1;$source.PetEnergy.PetClassId=1620;$source.PetEnergy.PetEvolutionPhase=1
  $source.PetEnergy.SupportSkills.Add($active)
  $source.PetEnergy.SupportSkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 }else{
  $source.Skills.Add($active);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 }
 if($layers -gt 0){$stored=$catalog.CreateStatus(24823,1,1,1);$stored.StackCount=$layers;$source.Statuses.Add($stored)}
 $state.Units.Add($source);$state.Units.Add($enemy)
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.Index=0;$wave.UnitIds.Add(2);$state.Waves.Add($wave)
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,$settlement,$catalog)
 $event=$battle.Step(0,1)
 Assert-Logic (@($event.Event.DamageEvents|Where-Object SkillEntityId -eq 50210).Count -eq 1) 'Real 50210 did not execute the original hit.'
 if($support){
  Assert-Logic (@($event.Event.SkillCasts|Where-Object {$_.SkillEntityId -eq 50210 -and $_.IsPetSupport}).Count -eq 1) '50210 did not retain the pet support source.'
  Assert-Logic ($source.PetEnergy.SupportSkillState.Slots[0].ReleasedTimes -eq 1 -and $source.SkillState.Slots.Count -eq 0) 'Pet support consumed an owner slot or lost its own release.'
 }
 if($layers -gt 0){
  Assert-Logic (($source.Statuses|Where-Object InstanceId -eq 1).StackCount -eq $layers) 'Real 50210 consumed amplification layers.'
  $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
  Assert-Logic (($restored.Units[0].Statuses|Where-Object InstanceId -eq 1).StackCount -eq $layers) 'Amplification layers were lost in checkpoint restore.'
  if($support){Assert-Logic ($restored.Units[0].PetEnergy.SupportSkillState.Slots[0].ReleasedTimes -eq 1) 'Pet support release was lost in checkpoint restore.'}
 }
 return 1000000-$enemy.Hp
}
$base=Invoke-RealStackedStrike 0;$stacked=Invoke-RealStackedStrike 5
Assert-Logic ($base -gt 0 -and $stacked -eq 2*$base) 'Real executor did not apply 50210 current-layer scaling with CanTriggerChild false.'
Write-Output 'Real 50210 cast, damage control, retained layers and checkpoint restore passed.'
$supportBase=Invoke-RealStackedStrike 0 $true;$supportStacked=Invoke-RealStackedStrike 5 $true
Assert-Logic ($supportBase -gt 0 -and $supportStacked -eq 2*$supportBase) 'The real pet support path did not apply the original current-layer graph.'
Write-Output 'Real 1620 pet support uses its own slot/count, doubles damage with five current layers and preserves charge/release checkpoint state.'

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
$items=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleGridItemState]]::new()
function New-Tile($id,$side,$x,$y) {
 $tile=[Zjcs.Combat.Official.FightGridItemRuntime]::Create($id,3320,$actor,$actor,[Zjcs.Combat.Official.FightGridPoint]::new($x,$y),0)
 $tile.Runtime.Side=$side
 return $tile
}
$a=New-Tile 11 1 4 5;$b=New-Tile 12 1 5 5;$c=New-Tile 13 1 4 7
$items.Add($a);$items.Add($b);$items.Add($c)
$items.Add((New-Tile 14 2 3 5));$items.Add((New-Tile 15 1 7 5))
$ended=New-Tile 16 1 4 4;$ended.Ended=$true;$items.Add($ended)
$other=New-Tile 17 1 4 4;$other.EntityClassId=9999;$items.Add($other)
$stopped=New-Tile 18 1 4 4;$stopped.Runtime.Ended=$true;$items.Add($stopped)
$legacy=New-Tile 19 1 4 4;$legacy.HasRuntime=$false;$items.Add($legacy)
$items.Add($a)
$skill=$catalog.CreateSkill(13730,0,0,1,1,1,$null)
$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
$logic=$create.Invoke($null,@($actor,$skill,$units,$catalog,$random,$items))
Assert-Logic ($null -ne $logic -and -not $start.Invoke($logic,@())) 'Burn scaling fabricated a skill-start graph.'
function Invoke-Segment($flow,[long]$damage=-101,[bool]$cure=$false) {
 $ctx=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new([Zjcs.Combat.Official.FightDamageTypeData]::new(),$damage,[Math]::Abs($damage),$cure)
 $scale.Invoke($flow,@($target,$hit,$ctx))|Out-Null
 return $ctx
}
$first=Invoke-Segment $logic
Assert-Logic ($first.Damage -eq -116 -and $first.AbsoluteDamage -eq 116) 'Wrong faction/range/liveness/entity deduplication or 5 percent rounding.'
$bb=$logicType.GetField('blackboard',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($logic)
Assert-Logic ($bb[511879177] -eq 3 -and $bb[122229964] -eq 15) 'Original count/DeltaDamage outputs differ.'
$c.Ended=$true
$next=Invoke-Segment $logic
Assert-Logic ($next.Damage -eq -111 -and $bb[511879177] -eq 2) 'The next segment reused an ended tile.'
$b.Runtime.Side=2
Assert-Logic ((Invoke-Segment $logic).Damage -eq -106) 'The grid entity faction was not checked again.'
$target.X=9;$target.Y=9
Assert-Logic ((Invoke-Segment $logic).Damage -eq -101 -and $bb[511879177] -eq 0) 'The target center or zero-count output was stale.'
$target.X=4;$target.Y=5
$items.Clear()
$graph=$skill.LogicDefinitionJson|ConvertFrom-Json
foreach($offset in $graph.OnDoHitTransient.HandleCfgs[0].FilterCondition.SubFilters[2].Range) {
 $items.Add((New-Tile (30+$items.Count) 1 (4+$offset.X) (5+$offset.Y)))
}
$many=Invoke-Segment $logic
Assert-Logic ($many.Damage -eq -166 -and $many.AbsoluteDamage -eq 166 -and $bb[511879177] -eq 13 -and $items.Count -eq 13 -and @($items|Where-Object Ended).Count -eq 0) 'Burn tiles were capped, consumed or scaled with a different factor.'
Assert-Logic ((Invoke-Segment $logic -101 $true).Damage -eq -101) 'Burn damage scaling affected healing.'
foreach($flag in @('Dodge','Blinding','Immunity')) {
 $type=[Zjcs.Combat.Official.FightDamageTypeData]::new();$type.$flag=$true
 $zero=[Zjcs.Combat.Official.FightBattleTransientDamageContext]::new($type,0,0,$false)
 $scale.Invoke($logic,@($target,$hit,$zero))|Out-Null
 Assert-Logic ($zero.Damage -eq 0 -and $zero.AbsoluteDamage -eq 0) 'Zero damage became nonzero.'
}
$empty=$create.Invoke($null,@($actor,$skill,$units,$catalog,$random,$null))
Assert-Logic ((Invoke-Segment $empty).Damage -eq -101) 'Missing grid context fabricated burn tiles.'
foreach($fault in @('class','faction','range','read','scale')) {
 $bad=$catalog.CreateSkill(13730,0,0,1,1,1,$null)
 $json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault) {
  'class' {$json.OnDoHitTransient.HandleCfgs[0].FilterCondition.SubFilters[0].ClassId=3052}
  'faction' {$json.OnDoHitTransient.HandleCfgs[0].FilterCondition.SubFilters[1].CheckEntityKey.Id=2002}
  'range' {$json.OnDoHitTransient.HandleCfgs[0].FilterCondition.SubFilters[2].UseWorldPosByCenterEntityTransform=$true}
  'read' {$json.OnDoHitTransient.HandleCfgs[1].StorageBBKey.IsPersistentBlackboard=$true}
  'scale' {$json.OnDoHitTransient.HandleCfgs[2].ScaleValue=501}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 80 -Compress
 $badLogic=$create.Invoke($null,@($actor,$bad,$units,$catalog,$random,$items))
 Assert-Logic ((Invoke-Segment $badLogic).Damage -eq -101) ('Unsupported burn graph modified damage: '+$fault)
}
Write-Output '13730: live faction tiles, exact range, per-entity count, per-segment refresh, no cap/consumption, outputs, rounding, cure/zero and malformed graphs passed.'

$shield=[Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ClassId=999999;$shield.InstanceId=105;$shield.ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$shield.ShieldInitialized=$true;$shield.ShieldRemainValue=100000;$target.Statuses.Add($shield)
$hit.HasCustomDamage=$true;$hit.CustomDamage=101
$callback=[Action[Zjcs.Combat.Official.FightBattleTransientDamageContext]]{param($ctx)$scale.Invoke($logic,@($target,$hit,$ctx))|Out-Null}
$plain=$hitExecutor.Apply($random,10,$actor,$target,$skill,$hit,0,$false,0,$null,$false,1,$null,$null,$null)
$shield.ShieldRemainValue=100000
$scaled=$hitExecutor.Apply($random,11,$actor,$target,$skill,$hit,0,$false,0,$null,$false,1,$null,$null,$callback)
Assert-Logic ($scaled.ShieldDamage -eq [long][decimal]::Truncate([decimal]$plain.ShieldDamage*165/100) -and $target.Hp -eq 1000000) 'Shield did not absorb burn amplified damage.'
function Invoke-RealBurn([int]$tileCount) {
 $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
 $state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
 $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=10;$state.Grid.Height=10;$state.Grid.DefaultGroundType=2
 $source=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 5
 $active=$catalog.CreateSkill(13730,0,0,1,1,1,$null)
 $active.HasCustomDamage=$true;$active.CustomDamage=101
 $source.Skills.Add($active);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
 $state.Units.Add($source);$state.Units.Add($enemy)
 $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.Index=0;$wave.UnitIds.Add(2);$state.Waves.Add($wave)
 $positions=@(@(4,5),@(5,5),@(4,7))
 for($i=0;$i -lt $tileCount;$i++) {
  $tile=[Zjcs.Combat.Official.FightGridItemRuntime]::Create((11+$i),3320,$source,$source,[Zjcs.Combat.Official.FightGridPoint]::new($positions[$i][0],$positions[$i][1]),0)
  $state.GridItems.Add($tile)
 }
 $battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,$settlement,$catalog)
 $event=$battle.Step(0,1)
 $events=@($event.Event.DamageEvents|Where-Object SkillEntityId -eq 13730)
 Assert-Logic ($events.Count -eq 1) ('Real 13730 did not execute its original hit: '+$events.Count)
 Assert-Logic ($state.GridItems.Count -eq $tileCount -and @($state.GridItems|Where-Object Ended).Count -eq 0) 'Real 13730 consumed grid tiles.'
 $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
 Assert-Logic ($restored.GridItems.Count -eq $tileCount) 'Burn tiles were lost on checkpoint restore.'
 return 1000000-$enemy.Hp
}
$baseline=Invoke-RealBurn 0;$withTiles=Invoke-RealBurn 3
Assert-Logic ($baseline -gt 0 -and $withTiles -eq [long][decimal]::Truncate([decimal]$baseline*115/100)) ('Real burn executor factor differs: '+$baseline+' -> '+$withTiles)
Write-Output '13730: real executor, original hit, shield amplification, retained tiles and checkpoint restore passed.'

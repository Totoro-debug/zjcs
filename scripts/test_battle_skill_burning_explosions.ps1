. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path (Get-FightCombatSources)
$skillPath=Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json';$battlePath=Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load([IO.File]::ReadAllText($skillPath),(Get-FileHash $skillPath).Hash.ToLowerInvariant(),'854_156383',[IO.File]::ReadAllText($battlePath),(Get-FileHash $battlePath).Hash.ToLowerInvariant(),'854_156383')
function Assert-Explosion($value,$label){if(-not $value){throw $label}}
function New-Unit($id,$side,$x,$y){
 $u=[Zjcs.Combat.Official.FightBattleUnitState]::new();$u.Id=$id;$u.Side=$side;$u.CharacterType=1;$u.X=$x;$u.Y=$y;$u.Hp=$u.MaxHp=$u.LastMaxHp=100000000;$u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=100-$id;return $u
}
$actor=New-Unit 1 1 4 4;$enemy=New-Unit 2 2 4 5
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($actor);$units.Add($enemy)
$items=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleGridItemState]]::new()
function New-Tile($id,$side,$x,$y){$t=[Zjcs.Combat.Official.FightGridItemRuntime]::Create($id,3320,$actor,$actor,[Zjcs.Combat.Official.FightGridPoint]::new($x,$y),0);$t.Runtime.Side=$side;return $t}
$random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$logicType=[Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime',$true)
$create=$logicType.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$collect=$logicType.GetMethod('CollectBurningExplosions',[Reflection.BindingFlags]'Instance,NonPublic')
$skill=$catalog.CreateSkill(13740,0,10193,1,1,1,$null)
$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
$logic=$create.Invoke($null,@($actor,$skill,$units,$catalog,$random,$items))
Assert-Explosion ($null -ne $logic -and $hit.EntityId -eq 13741) 'Missing original hit-only graph.'
foreach($direction in 0..3){
 $actor.Direction=$direction;$actor.HasFlipX=$true;$actor.FlipX=$direction -eq 1
 $cast=[Zjcs.Combat.Official.FightBattleSkillCastPresentation]::Capture($actor,$skill,$units,$enemy)
 $scopeMethod=[Zjcs.Combat.Official.FightBattleTargeting].GetMethod('TransformScope',[Reflection.BindingFlags]'Static,NonPublic')
 $offsets=$scopeMethod.Invoke($null,@($hit.Scope,$hit.ScopeTransform,$actor,$cast))
 $items.Clear();$index=0
 foreach($offset in $offsets){$items.Add((New-Tile (20+$index) 1 ($cast.SourceX+$offset.X) ($cast.SourceY+$offset.Y)));$index++}
 $items.Add($items[0]);$items.Add((New-Tile 100 2 4 5));$items.Add((New-Tile 101 1 20 20))
 $positions=$collect.Invoke($logic,@($hit,$enemy,$cast))
 Assert-Explosion ($positions.Count -eq 12) ('Original directional range/dedup differs: '+$direction+' -> '+$positions.Count)
 $actor.X+=20;$enemy.Y+=20
 Assert-Explosion ($collect.Invoke($logic,@($hit,$enemy,$cast)).Count -eq 12) 'Source-cast anchor incorrectly followed source or victim movement.'
 $actor.X-=20;$enemy.Y-=20
 $items[0].Ended=$true
 Assert-Explosion ($positions.Count -eq 12 -and $collect.Invoke($logic,@($hit,$enemy,$cast)).Count -eq 11) 'Collection was not an independent live snapshot.'
}
$actor.Direction=2;$actor.HasFlipX=$false
$cast=[Zjcs.Combat.Official.FightBattleSkillCastPresentation]::Capture($actor,$skill,$units,$enemy)
$items.Clear();$tile=New-Tile 11 1 4 5;$items.Add($tile)
Assert-Explosion ($collect.Invoke($logic,@($hit,$enemy,$cast)).Count -eq 1) 'Known forward tile was excluded.'
$skill.CanTriggerChild=$false
Assert-Explosion ($collect.Invoke($logic,@($hit,$enemy,$cast)).Count -eq 0) 'CanTriggerChild was bypassed.'
$skill.CanTriggerChild=$true
foreach($fault in @('rate','port','range','foreach','filter')){
 $bad=$catalog.CreateSkill(13740,0,10193,1,1,1,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault){
 'rate'{$json.OnDoHit.HandleCfgs[1].HandleCfgs[0].Rate=0.75}
 'port'{$json.OnDoHit.HandleCfgs[1].HandleCfgs[0].SourceEntityKey.Id=2002}
 'range'{$json.OnDoHit.HandleCfgs[0].FilterCondition.SubFilters[2].Range[0].Y=9}
 'foreach'{$json.OnDoHit.HandleCfgs[1].ForeachCount=1}
 'filter'{$json.OnDoHit.HandleCfgs[0].FilterCondition.SubFilters[0]._type='Unknown'}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 80 -Compress
 $badLogic=$create.Invoke($null,@($actor,$bad,$units,$catalog,$random,$items))
 Assert-Explosion ($collect.Invoke($badLogic,@($hit,$enemy,$cast)).Count -eq 0) ('Unsupported configuration executed: '+$fault)
}
Write-Output '13740 collection: all four directions, 12 cells, liveness, snapshot, entity dedup, child policy and malformed graphs passed.'
$rulesPath=Join-Path $root 'unity/Assets/Official/Combat/rules.json'
$formula=$catalog.CreateDamageFormula([IO.File]::ReadAllText($rulesPath),(Get-FileHash $rulesPath).Hash.ToLowerInvariant(),'854_156383')
$settlement=[Zjcs.Combat.Official.FightDamageSettlement]::new()
$state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
$state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=12;$state.Grid.Height=12;$state.Grid.DefaultGroundType=2
$source=New-Unit 1 1 4 4;$boss=New-Unit 2 2 4 6;$other=New-Unit 3 2 5 6
$attackProp=[Zjcs.Combat.Official.FightPropertyValue]::new();$attackProp.Id=$catalog.PropertyType('Attack');$attackProp.Value=1000;$source.Properties.Add($attackProp)
$falloff=[Zjcs.Combat.Official.FightBattleStatusState]::new();$falloff.InstanceId=1;$falloff.ClassId=999;$falloff.OwnerId=2;$falloff.CreatorId=1;$falloff.RootSkillId=13740
$falloff.DamageId=10023;$falloff.Rate=1;$falloff.FalloffTarget=1;$falloff.FalloffPercent=5000;$falloff.NumOfStart=1;$falloff.MaxFalloffCount=2;$boss.Statuses.Add($falloff)
foreach($x in -1..1){foreach($y in -1..1){$boss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))}}
$active=$catalog.CreateSkill(13740,0,10193,1,1,1,$null);$active.HasCustomDamage=$true;$active.CustomDamage=101
$source.Skills.Add($active);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
$state.Units.Add($source);$state.Units.Add($boss);$state.Units.Add($other)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$wave.UnitIds.Add(3);$state.Waves.Add($wave)
foreach($pos in @(@(4,5),@(5,5))){$t=[Zjcs.Combat.Official.FightGridItemRuntime]::Create((11+$state.GridItems.Count),3320,$source,$source,[Zjcs.Combat.Official.FightGridPoint]::new($pos[0],$pos[1]),0);$state.GridItems.Add($t)}
$battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,$settlement,$catalog)
$step=$battle.Step(0,1)
$children=@($step.Event.SkillCasts|Where-Object SkillEntityId -eq 13745)
Assert-Explosion ($children.Count -eq 2) 'Explosions were repeated per victim or omitted.'
$bossHits=@($step.Event.DamageEvents|Where-Object {$_.SkillEntityId -eq 13745 -and $_.TargetId -eq 2})
Assert-Explosion ($bossHits.Count -eq 7) ('Overlapping explosions did not settle all seven occupied boss cells: '+$bossHits.Count)
Assert-Explosion (@($bossHits|Where-Object {$_.Damage -ge 0 -or $_.BeforeHp -le $_.AfterHp}).Count -eq 0) ('Explosion occurrences did not actually reduce boss HP: '+($bossHits.Damage -join ','))
Assert-Explosion ($bossHits[1].Damage -eq [long][Math]::Truncate($bossHits[0].Damage*0.5) -and $bossHits[2].Damage -eq [long][Math]::Truncate($bossHits[0].Damage*0.25) -and $bossHits[4].Damage -eq $bossHits[0].Damage) 'Explosion lost the root skill for per-target falloff, or shared falloff between separate child casts.'
$otherHits=@($step.Event.DamageEvents|Where-Object {$_.SkillEntityId -eq 13745 -and $_.TargetId -eq 3})
Assert-Explosion ($otherHits.Count -eq 1) 'Another enemy inside the blast was excluded.'
foreach($child in $children){Assert-Explosion ([Math]::Abs($child.Delay-0.76) -lt 0.0001 -and $child.Slot -eq -1 -and $child.SkillItemId -eq 10193) 'Child timing/root item/action differs.'}
foreach($damage in $bossHits){Assert-Explosion ([Math]::Abs($damage.Delay-0.91) -lt 0.0001) 'Original 13746 delay was lost.'}
Assert-Explosion ($state.GridItems.Count -eq 2 -and @($state.GridItems|Where-Object Ended).Count -eq 0) 'Explosions consumed their tiles.'
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
Assert-Explosion ($restored.GridItems.Count -eq 2) 'Tile checkpoint restore differs.'
Write-Output '13740 production executor: one explosion per tile, overlapping boss hits, root item, exact delays, retained tiles and checkpoint passed.'

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path (Get-FightCombatSources)
$skillPath=Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json';$battlePath=Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load([IO.File]::ReadAllText($skillPath),(Get-FileHash $skillPath).Hash.ToLowerInvariant(),'854_156383',[IO.File]::ReadAllText($battlePath),(Get-FileHash $battlePath).Hash.ToLowerInvariant(),'854_156383')
function Assert-Child($value,$label){if(-not $value){throw $label}}
function New-Unit($id,$side,$x,$y){
 $u=[Zjcs.Combat.Official.FightBattleUnitState]::new();$u.Id=$id;$u.Side=$side;$u.CharacterType=1;$u.X=$x;$u.Y=$y;$u.Hp=$u.MaxHp=$u.LastMaxHp=100000000;$u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=100-$id;return $u
}

$logicType=[Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime',$true)
$create=$logicType.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$collect=$logicType.GetMethod('CollectChildSkills',[Reflection.BindingFlags]'Instance,NonPublic')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Get-Field($value,$name){$value.GetType().GetField($name,$flags).GetValue($value)}
$source=New-Unit 1 1 8 8;$master=New-Unit 2 1 20 20;$enemy=New-Unit 3 2 9 8
$source.CharacterType=5;$source.MasterId=$source.Turn.MasterId=2
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($source);$units.Add($master);$units.Add($enemy)
$random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$skill=$catalog.CreateSkill(54370,0,16442,5,20,3,$null)
function New-Logic($definition){$create.Invoke($null,@($source,$definition,$units,$catalog,$random,$null))}
$logic=New-Logic $skill
$request=$collect.Invoke($logic,@($null,$enemy,$null))
Assert-Child ($request.Count -eq 1 -and (Get-Field $request[0] 'SkillId') -eq 54373 -and (Get-Field $request[0] 'Target') -eq $master) 'Master blackboard did not bind the actual master.'
Assert-Child ($collect.Invoke($logic,@($null,$enemy,$null)).Count -eq 0) 'Start graph ran more than once for one cast.'
foreach($fault in @('absent','ended','zero','policy','port','rate','filter')){
 $bad=$catalog.CreateSkill(54370,0,16442,5,20,3,$null);$json=$bad.LogicDefinitionJson|ConvertFrom-Json
 switch($fault){
 'absent'{$source.MasterId=999}
 'ended'{$master.Ended=$true}
 'zero'{$source.MasterId=0}
 'policy'{$bad.CanTriggerChild=$false}
 'port'{$json.OnSkillStart.HandleCfgs[1].TargetEntityKey.Id=2002}
 'rate'{$json.OnSkillStart.HandleCfgs[1].Rate=0.5}
 'filter'{$json.OnSkillStart.FilterCondition=@{_type='Unknown'}}
 }
 $bad.LogicDefinitionJson=$json|ConvertTo-Json -Depth 80 -Compress
 Assert-Child ($collect.Invoke((New-Logic $bad),@($null,$enemy,$null)).Count -eq 0) ('Unsupported/missing master graph executed: '+$fault)
 $source.MasterId=2;$master.Ended=$false
}
$master.Hp=0
Assert-Child ($collect.Invoke((New-Logic $skill),@($null,$enemy,$null)).Count -eq 1) 'HP was incorrectly substituted for official battlefield presence.'
$master.Hp=$master.MaxHp
$child=$catalog.CreateSkill(54373,-1,16442,5,20,3,$null)
$hit=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($child)[0]
$logic=New-Logic $child
Assert-Child ($collect.Invoke($logic,@($null,$master,$null)).Count -eq 0) 'Hit graph ran on skill start.'
$request=$collect.Invoke($logic,@($hit,$master,$null))
Assert-Child ($request.Count -eq 1 -and (Get-Field $request[0] 'SkillId') -eq 54375 -and (Get-Field $request[0] 'Target') -eq $master) 'SkillTargetEntity did not retain the explicit master target.'
$child.CanTriggerChild=$false
Assert-Child ($collect.Invoke((New-Logic $child),@($hit,$master,$null)).Count -eq 0) 'Hit graph bypassed child policy.'
Assert-Child ($random.Seed -eq 91) 'Deterministic/missing/gated child graphs consumed random draws.'
Assert-Child ($catalog.MonsterCharacterType(9320) -eq 7 -and $catalog.MonsterBodyOffsets(9320).Count -gt 0) 'Original evolved pet identity is missing.'
Write-Output 'Master/target child adapters: exact ports, start once, missing/ended master, explicit target and child policy passed.'
$rulesPath=Join-Path $root 'unity/Assets/Official/Combat/rules.json'
$formula=$catalog.CreateDamageFormula([IO.File]::ReadAllText($rulesPath),(Get-FileHash $rulesPath).Hash.ToLowerInvariant(),'854_156383')
$state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$state.StageId=1;$state.MaxRound=10;$state.RandomSeed=91
$state.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$state.Grid.Width=32;$state.Grid.Height=32;$state.Grid.DefaultGroundType=1
$source.Skills.Add($skill);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,0,-10))
foreach($u in $units){$state.Units.Add($u)}
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(3);$state.Waves.Add($wave)
$battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$step=$battle.Step(0,1)
$casts=@($step.Event.SkillCasts|Where-Object {$_.SkillEntityId -in 54370,54373,54375})
Assert-Child ((($casts.SkillEntityId|Sort-Object) -join ',') -eq '54370,54373,54375') ('Original child chain differs: '+($casts.SkillEntityId -join ','))
foreach($cast in $casts){Assert-Child ($cast.ActorId -eq 1 -and $cast.SkillItemId -eq 16442 -and [Math]::Abs($cast.Delay) -lt 0.0001) 'Child caster/root item/delay differs.'}
foreach($childCast in @($casts|Where-Object SkillEntityId -ne 54370)){Assert-Child ($childCast.TargetIds[0] -eq 2 -and $childCast.Slot -eq -1) 'Child target was replaced by AI or consumed a normal slot.'}
$tiles=@($state.GridItems|Where-Object {-not $_.Ended})
$nearSource=@($tiles|Where-Object {[Math]::Abs($_.X-8)+[Math]::Abs($_.Y-8) -le 3})
$nearMaster=@($tiles|Where-Object {[Math]::Abs($_.X-20)+[Math]::Abs($_.Y-20) -le 3})
Assert-Child ($nearSource.Count -gt 0 -and $nearMaster.Count -gt 0 -and $tiles.Count -eq $nearSource.Count+$nearMaster.Count) 'Original caster/master grid scopes were not both spawned.'
foreach($tile in $tiles){
 Assert-Child ([Math]::Abs($tile.CreationDelay-0.63) -lt 0.0001 -and $tile.Runtime.MasterId -eq 1) 'Original grid delay/reference snapshot differs.'
 $status=@($tile.Runtime.Statuses|Where-Object ClassId -eq 54372)[0]
 Assert-Child ($null -ne $status -and $status.CreatorId -eq 1 -and $status.OwnerId -eq $tile.Id -and $status.Origin.SkillEntityId -eq 54370) 'Grid status creator/applicator/root inheritance differs.'
 Assert-Child ($status.SourceSkillRank -eq 5 -and $status.SourceSkillLevel -eq 20 -and $status.SourceSubRank -eq 3) 'Child grid lost root cultivation ranks.'
}
Assert-Child ($source.SkillState.Slots[0].ReleasedTimes -eq 1) 'Child chain spent extra ordinary releases.'
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
Assert-Child ($restored.GridItems.Count -eq $state.GridItems.Count -and $restored.RandomSeed -eq $state.RandomSeed -and @($restored.Receipts[0].Event.SkillCasts|Where-Object {$_.SkillEntityId -in 54370,54373,54375}).Count -eq 3) 'Child chain/grid/random checkpoint differs.'
Write-Output ('Original 54370 -> 54373 -> 54375 executor: '+$nearSource.Count+' caster tiles, '+$nearMaster.Count+' master tiles, original delays, root ownership, one release and checkpoint passed.')

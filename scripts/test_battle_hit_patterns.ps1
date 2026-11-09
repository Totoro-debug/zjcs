. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
Add-Type -Path (Get-FightCombatSources)
function Assert-Pattern($condition,$message){if(-not $condition){throw $message}}
function New-PatternUnit($id,$side,$x,$y){
 $u=[Zjcs.Combat.Official.FightBattleUnitState]::new();$u.Id=$id;$u.Side=$side;$u.CharacterType=if($side -eq 1){1}else{2};$u.X=$x;$u.Y=$y
 $u.Hp=$u.MaxHp=$u.LastMaxHp=10000;$u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=100-$id;return $u
}
$actor=New-PatternUnit 1 1 0 0;$boss=New-PatternUnit 2 2 2 0;$other=New-PatternUnit 3 2 3 1
foreach($p in @(@(0,0),@(0,1),@(1,0),@(0,0))){$boss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($p[0],$p[1]))}
$skill=[Zjcs.Combat.Official.FightBattleSkillDefinition]::new();$skill.EntityId=4001;$skill.ItemId=5001;$skill.TargetType=1;$skill.Slot=0
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,0))
$hit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new();$hit.EntityId=4002;$hit.DamageId=4003;$hit.DamageType=8;$hit.HasCustomDamage=$true;$hit.CustomDamage=100;$hit.Anchor=1;$hit.Delay=0.4
$index=0
foreach($p in @(@(2,0),@(2,1),@(3,0),@(3,1))){
 $point=[Zjcs.Combat.Official.FightGridPoint]::new($p[0],$p[1]);$hit.Scope.Add($point)
 $entry=[Zjcs.Combat.Official.FightBattleHitScopeEntry]::new();$entry.Position=$point;$entry.Delay=$index/10.0;$hit.ScopeEntries.Add($entry);$index++
}
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($actor);$units.Add($boss);$units.Add($other)
$targets=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$targets.Add($boss);$targets.Add($other)
$context=[Zjcs.Combat.Official.FightHitPatternContext]::new();$context.Source=$actor;$context.Units=$units;$context.Skill=$skill;$context.Hit=$hit;$context.Targets=$targets;$context.Primary=$boss;$context.Random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$area=[Zjcs.Combat.Official.FightBattleHitPatterns]::For($hit)
Assert-Pattern ($area -is [Zjcs.Combat.Official.IFightHitPattern]) 'Area does not use the hit-pattern interface.'
$planned=$area.Plan($context)
Assert-Pattern ($planned.Count -eq 4 -and @($planned|Where-Object {$_.Target.Id -eq 2}).Count -eq 3 -and @($planned|Where-Object {$_.Target.Id -eq 3}).Count -eq 1) 'Multi-cell damage omitted a body cell or another enemy.'
for($i=0;$i -lt 4;$i++) {Assert-Pattern ($planned[$i].HasGridPosition -and [Math]::Abs($planned[$i].Delay-$i/10.0) -lt 0.0001) 'Original cell identity/order/delay differs.'}
$hit.TargetMode=2;$hit.RandomCount=1
$randomPlan=$area.Plan($context)
Assert-Pattern (@($randomPlan|ForEach-Object {$_.Target.Id}|Select-Object -Unique).Count -eq 1 -and $randomPlan.Count -in @(1,3)) 'Explicit random count became a body-cell limit.'
$context.SelectedByRandomParent=$true
Assert-Pattern ($area.Plan($context).Count -eq 4) 'An already-selected random anchor limited its area victims again.'
$context.SelectedByRandomParent=$false;$hit.TargetMode=1;$hit.ActionType=5
Assert-Pattern ($area.Plan($context).Count -eq 2) 'Attack-cell multiplication was applied to healing.'
$hit.ActionType=4;$skill.SkillType=3
Assert-Pattern ($area.Plan($context).Count -eq 2) 'Skill-level healing inherited attack-cell multiplication.'
$skill.SkillType=0
$hit.ActionType=4;$hit.TargetMode=0
$point=[Zjcs.Combat.Official.FightBattleHitDefinition]::new();$context.Hit=$point
$entity=[Zjcs.Combat.Official.FightBattleHitPatterns]::For($point)
Assert-Pattern ($entity.GetType() -ne $area.GetType() -and $entity.Plan($context).Count -eq 1) 'Point attacks did not retain their separate pattern and target limit.'
$context.SelectedByRandomParent=$true
Assert-Pattern ($entity.Plan($context).Count -eq 2) 'Preselected direct targets were selected a second time.'
$context.SelectedByRandomParent=$false;$context.Hit=$hit
$other.Hp=0
Assert-Pattern ($area.Plan($context).Count -eq 3) 'A dead target received planned attack cells.'
$other.Hp=10000
Write-Output 'Hit-pattern interface: entity/area variants, all occupied cells, duplicate body exclusion, per-cell delays, random counts/anchors, healing and dead-target exclusions passed.'

# The real executor applies falloff, shield and HP once for every planned occurrence.
$status=[Zjcs.Combat.Official.FightBattleStatusState]::new();$status.InstanceId=1;$status.ClassId=999;$status.OwnerId=$boss.Id;$status.CreatorId=$actor.Id;$status.RootSkillId=4001
$status.Rate=1;$status.FalloffTarget=1;$status.FalloffPercent=5000;$status.NumOfStart=1;$status.MaxFalloffCount=2;$boss.Statuses.Add($status)
$skill.Hits.Add($hit);$actor.Skills.Add($skill)
$slot=[Zjcs.Combat.Official.FightSkillSlotState]::new();$slot.Slot=0;$slot.LimitedTimes=-1;$actor.SkillState.Slots.Add($slot)
$checkpoint=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$checkpoint.StageId=1;$checkpoint.RandomSeed=91
$checkpoint.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new();$checkpoint.Grid.Width=10;$checkpoint.Grid.Height=10;$checkpoint.Grid.DefaultGroundType=2
foreach($u in $units){$checkpoint.Units.Add($u)}
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$wave.UnitIds.Add(3);$checkpoint.Waves.Add($wave)
$formula=[Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(),[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$battle=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
$step=$battle.Step(0,1)
$bossHits=@($step.Event.DamageEvents|Where-Object TargetId -eq 2);$otherHits=@($step.Event.DamageEvents|Where-Object TargetId -eq 3)
Assert-Pattern ($bossHits.Count -eq 3 -and $otherHits.Count -eq 1) 'The executor merged occupied scope cells.'
Assert-Pattern ($bossHits[0].Damage -eq -100 -and $bossHits[1].Damage -eq -50 -and $bossHits[2].Damage -eq -25 -and $otherHits[0].Damage -eq -100) ('Configured per-target falloff did not apply between body-cell hits: '+($bossHits.Damage -join ',')+' other='+($otherHits.Damage -join ','))
Assert-Pattern ($boss.Hp -eq 9825 -and $other.Hp -eq 9900 -and $actor.SkillState.Slots[0].ReleasedTimes -eq 1) 'Per-cell HP or single skill release differs.'
for($i=0;$i -lt 4;$i++) {Assert-Pattern ($step.Event.DamageEvents[$i].HasHitGridPosition -and [Math]::Abs($step.Event.DamageEvents[$i].Delay-(0.4+$i/10.0)) -lt 0.0001) 'Cell identity/delay did not reach damage events.'}
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
$savedHits=$saved.Receipts[0].Event.DamageEvents
Assert-Pattern ($savedHits.Count -eq 4 -and $savedHits[2].HasHitGridPosition -and $savedHits[2].HitGridX -eq 3 -and $savedHits[2].HitGridY -eq 0 -and $saved.Units[1].Hp -eq 9825) 'Checkpoint lost per-cell occurrences or their settled HP.'
Write-Output 'Real hit patterns: occupied cells, original cell delays, per-target falloff, one release and checkpoint metadata passed.'

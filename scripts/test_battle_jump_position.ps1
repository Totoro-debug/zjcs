$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'test_battle_ordinary_movement.ps1')
function Point($x,$y){[Zjcs.Combat.Official.FightGridPoint]::new($x,$y)}
function Unit($id,$side,$x,$y){
 $u=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=$id;Side=$side;CharacterType=1;X=$x;Y=$y;Hp=1000;MaxHp=1000;LastMaxHp=1000;MoveDist=4}
 $u.Turn.Id=$id;$u.Turn.Hp=1000;$u.Turn.Speed=if($side -eq 1){100}else{1};return $u
}
function Skill($dx,$dy){
 $s=[Zjcs.Combat.Official.FightBattleSkillDefinition]@{EntityId=9001;ItemId=9001;Slot=0;TargetType=1;SkillTargetType=3;RangeVerified=$true;HasCustomDamage=$true;CustomDamage=1}
 $s.Range.Add((Point $dx $dy));return $s
}
function Plan($a,$roster,$s){[Zjcs.Combat.Official.FightBattleTargeting]::PlanOrdinaryLanding($a,$roster,$s,$grid)}
$grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=16;Height=16;DefaultGroundType=2}
$a=Unit 1 1 2 2;$b=Unit 2 2 6 4;$s=Skill 1 0
$roster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($a,$b)
$p=Plan $a $roster $s
Require ($p.Equals((Point 5 4))) 'Euclidean (3,2) landing was rejected by path length'
$b.X=7;$b.Y=2;$p=Plan $a $roster $s
Require ($p.Equals((Point 6 2))) 'Four-cell boundary was rejected'
$b.X=6;$b.Y=5;$p=Plan $a $roster $s
Require (-not $p.Equals((Point 5 5)) -and [Math]::Pow($p.X-2,2)+[Math]::Pow($p.Y-2,2) -le 16) 'Outside-circle diagonal exceeded movement allowance'
# A jump has no intermediate walk cells, but the landing must fit the complete body.
$b.X=6;$b.Y=4
foreach($y in 0..15){$grid.BlockedPositions.Add((Point 3 $y))}
$p=Plan $a $roster $s
Require ($p.Equals((Point 5 4))) 'Intermediate wall incorrectly turned a jump into walking'
$grid.BlockedPositions.Add((Point 5 4));$p=Plan $a $roster $s
Require (-not $p.Equals((Point 5 4))) 'Blocked landing accepted'
$grid.BlockedPositions.Clear();$a.BodyOffsets.Add((Point 0 1));$grid.BlockedPositions.Add((Point 5 5))
$p=Plan $a $roster $s
Require (-not $p.Equals((Point 5 4))) 'Jump ignored the mover body footprint'
$a.BodyOffsets.RemoveAt(1);$grid.BlockedPositions.Clear()
# An unreachable large target still attracts an approach, instead of switching to a small one.
$near=Unit 3 2 4 2;$b.X=14;$b.Y=14;$b.BodyOffsets.Add((Point 0 1));$s.AiPriorityTypes.Add(10)
$roster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($a,$b,$near)
$p=Plan $a $roster $s
Require ($p.X -gt 3 -and $p.Y -gt 2) 'Unreachable big target was discarded for a small one'
$near.X=3
$selected=[Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange($a,$roster,$s)
Require ($selected.Count -eq 0) 'Mandatory large-target branch fell through to a small target'
# Authored BiggerBodyRange precedes movement distance among reachable choices.
$b.X=6;$b.Y=4;$p=Plan $a $roster $s
Require ($p.X -eq 5 -and $p.Y -in 4,5) 'Big-body priority was dropped during landing selection'
# Source-centred area chooses a landing covering the group, even with a target already in range.
$s=Skill 1 0;$s.Range.Add((Point 0 1));$s.AiPriorityTypes.Add(1)
$hit=[Zjcs.Combat.Official.FightBattleHitEntity]@{EntityId=9010;Anchor=1;HasFixedDamage=$true}
$hit.Scope.Add((Point 1 0));$hit.Scope.Add((Point 0 1));$s.HitGraphEntities.Add($hit)
$s.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{EntityId=9010;Anchor=1})
$c=Unit 4 2 6 3;$b.X=7;$b.Y=2;$b.BodyOffsets.Clear();$b.BodyOffsets.Add((Point 0 0))
$roster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($a,$near,$b,$c)
$p=Plan $a $roster $s
Require ($p.Equals((Point 6 2))) 'MoreHitCount did not score actual source landing against the cluster'
Require ($a.X -eq 2 -and $a.Y -eq 2) 'Planning mutated the authoritative position'
# Marked enemies are a mandatory branch, even when a large body/group is available.
$mark=[Zjcs.Combat.Official.FightBattleStatusState]@{AiMoreHit=$true}
$near.Statuses.Add($mark);$b.BodyOffsets.Add((Point 0 1))
$p=Plan $a $roster $s
Require ($p.X -eq 2 -and $p.Y -eq 2) 'Marked-target branch fell through to the large-body/group branch'
$s.SkillType=1;$p=Plan $a $roster $s
Require ($p.Equals((Point 2 2))) 'Hostile debuff did not honor the mandatory marked target'
$s.SkillType=0
$near.Statuses.Clear();$b.BodyOffsets.RemoveAt(1)
# Maximize distinct large-body cells; a lower-priority cluster cannot outweigh them.
$wide=Skill 2 0;$wide.Range.Add((Point 3 0))
$wideHit=[Zjcs.Combat.Official.FightBattleHitEntity]@{EntityId=9020;Anchor=1;HasFixedDamage=$true}
foreach($dx in 1..3){foreach($dy in 0..1){$wideHit.Scope.Add((Point $dx $dy))}}
$wide.HitGraphEntities.Add($wideHit);$wide.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{EntityId=9020;Anchor=1})
$large=Unit 8 2 5 3
foreach($offset in @(@(0,1),@(1,0),@(1,1))){$large.BodyOffsets.Add((Point $offset[0] $offset[1]))}
$largeRoster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($a,$large)
$p=Plan $a $largeRoster $wide
Require ($p.Equals((Point 3 3))) 'Large-body branch did not cover all four cells at the safer tied landing'
# Equal marked-target choices skip lower-priority body size and HP; landing safety breaks ties.
$markedActor=Unit 20 1 5 5;$markedLeft=Unit 21 2 2 5;$markedRight=Unit 22 2 8 5
$markedLeft.Statuses.Add($mark);$markedRight.Statuses.Add($mark)
$markedRight.BodyOffsets.Add((Point 0 1));$markedRight.Hp=1
$markedSkill=Skill 1 0;$markedSkill.Range.Add((Point -1 0))
$markedRoster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($markedActor,$markedLeft,$markedRight)
$p=Plan $markedActor $markedRoster $markedSkill
Require ($p.Equals((Point 1 5))) 'Marked branch leaked into body size or lower-HP priority'
# Current single-target interpretation: after no group opportunity, use move/safety then HP percent.
$markedLeft.Statuses.Clear();$markedRight.Statuses.Clear();$markedRight.BodyOffsets.RemoveAt(1)
$markedLeft.Hp=100;$markedLeft.MaxHp=1000;$markedRight.Hp=50;$markedRight.MaxHp=100
$p=Plan $markedActor $markedRoster $markedSkill
Require ($p.Equals((Point 3 5))) 'Non-final single-target cast did not minimize movement before comparing HP percentage'
$markedSkill.UseLastAiPriority=$true;$p=Plan $markedActor $markedRoster $markedSkill
Require ($p.Equals((Point 1 5))) 'Final single-target cast did not choose the safer landing before HP percentage'
# Outside attack/debuff skills, ordinary targeting still follows the skill effect.
$heal=Skill 1 0;$heal.SkillType=3;$heal.TargetType=2;$heal.AiPriorityTypes.Add(2)
$friend=Unit 9 1 3 2;$friend.Hp=100
$supportRoster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($a,$b,$friend)
$support=[Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange($a,$supportRoster,$heal)
Require ($support.Count -eq 1 -and $support[0].Id -eq 9) 'Enemy attack priorities leaked into healing'
$buff=Skill 1 0;$buff.SkillType=2;$buff.TargetType=2;$buff.Range.Add((Point 0 1));$buff.AiPriorityTypes.Add(2)
$healthyFriend=Unit 10 1 2 3;$healthyFriend.Statuses.Add($mark)
$supportRoster=[Zjcs.Combat.Official.FightBattleUnitState[]]@($a,$b,$friend,$healthyFriend)
$support=[Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange($a,$supportRoster,$buff)
Require ($support.Count -eq 1 -and $support[0].Id -eq 9) 'Attack mark overrode the ally buff effect priority'
Require ((Plan $a $supportRoster $buff).Equals((Point 2 2))) 'Ally buff moved away from its selected effect target'
$defense=Skill 0 0;$defense.SkillType=2;$defense.TargetType=0
Require ((Plan $a $supportRoster $defense).Equals((Point 2 2))) 'Self defense was repositioned using enemy attack priorities'
$a.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ActionType=58;CreatorId=$b.Id})
$buff.ReleaseWhileRidicule=$true;$friend.X=12;$friend.Y=4
Require ((Plan $a $supportRoster $buff).X -gt $a.X) 'Taunt incorrectly redirected an allowed ally buff approach toward its enemy creator'
$a.Statuses.Clear();$friend.X=3;$friend.Y=2
# The actual executor emits one original 15300 action from start directly to landing.
$s=Skill 1 0;$b.X=6;$b.Y=4;$a.Skills.Add($s)
$a.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$cp=[Zjcs.Combat.Official.FightBattleCheckpoint]@{StageId=1;RandomSeed=19;MaxRound=8;Grid=$grid}
$cp.Units.Add($a);$cp.Units.Add($b)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$cp.Waves.Add($wave)
$schema=[Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{Name='MoveDist';Id=100})
$schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{Name='MoveDistPercent';Id=118})
$formula=[Zjcs.Combat.Official.FightDamageFormula]::new($schema,[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$a.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=100;Value=4})
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$step=$runner.Step(0,1)
Require ($step.Event.Movements.Count -eq 1 -and $step.Event.Movements[0].Path.Count -eq 2 -and $step.Event.Movements[0].Path[1].Equals((Point 5 4))) 'Executor emitted intermediate grid steps'
Require ($step.Event.Movements[0].MoveSkillEntityId -eq 15300 -and $step.Event.SkillCasts.Count -eq 1) 'Jump lost original movement or next cast'
Require ($a.Direction -eq 3 -and -not $a.FlipX -and $step.Event.Movements[0].Path[1].X -gt $step.Event.Movements[0].Path[0].X) 'Long diagonal jump retained an adjacent-cell direction'
Near $step.Event.SkillCasts[0].Delay .5 'One jump must occupy one original movement action'
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
Require ($restored.Receipts[0].Event.Movements[0].Path.Count -eq 2 -and $restored.Units[0].X -eq 5) 'Checkpoint expanded jump or lost landing'
$progression=Get-Content "$root/unity/Assets/Official/Character/character-progression.json" -Raw | ConvertFrom-Json -Depth 100
foreach($row in $progression.level_prop.'50'.rows.PSObject.Properties){Require ($row.Value.'100' -eq 4) 'Original player subrank base MoveDist differs from four'}
Write-Output 'Battle jump positioning passed: Euclidean four-cell circle, one original jump, landing footprint, reachable candidates before priorities, big bodies, source-centred cluster coverage, original timing and checkpoint.'

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Get-FightCombatSources)
$skillPath = Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json'
$battlePath = Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText($skillPath), (Get-FileHash $skillPath -Algorithm SHA256).Hash.ToLowerInvariant(), '854_156383',
    [IO.File]::ReadAllText($battlePath), (Get-FileHash $battlePath -Algorithm SHA256).Hash.ToLowerInvariant(), '854_156383')
function Assert-Combo($condition, $label) { if (-not $condition) { throw $label } }
function New-Unit($id, $side, $x, $y, $speed) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id=$id; $u.Side=$side; $u.CharacterType=1; $u.X=$x; $u.Y=$y
    $u.Hp=$u.MaxHp=$u.LastMaxHp=1000000; $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=$speed
    return $u
}
function New-Fixture($configure = $null) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1; $cp.MaxRound=10; $cp.RandomSeed=91
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width=10; $cp.Grid.Height=10; $cp.Grid.DefaultGroundType=2
    $p=New-Unit 1 1 4 2 100
    $e=New-Unit 2 2 4 4 1
    $skill=$catalog.CreateSkill(13100,0,10154,5,20,1,$null)
    $p.Skills.Add($skill); $p.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,3,-10))
    $cp.Units.Add($p); $cp.Units.Add($e)
    $w=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $w.Index=0; $w.UnitIds.Add(2); $cp.Waves.Add($w)
    if ($configure) { & $configure $cp $p $e $skill }
    $formula=[Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $ex=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
    return @{ Cp=$cp; Player=$p; Enemy=$e; Skill=$skill; Executor=$ex }
}
$f=New-Fixture
Assert-Combo (($f.Skill.ComboSkills -join ',') -eq '13104,13105,13106,13107,13108') 'Original combo list absent'
$segment=$catalog.CreateSkill(13104,-1,10154,5,20,1,$null)
Assert-Combo (($segment.FallbackSkills -join ',') -eq '13104,13105,13106,13107' -and $segment.FallbackRange.Count -eq 25) 'Original fallback configuration differs'
Assert-Combo ($segment.SourceMoves[0].AiMustMoveTo -and -not $segment.SourceMoves[0].CanExpandOffset -and $segment.MoveByTeleport) 'Original mandatory offset/teleport fields absent'
$r=$f.Executor.Step(0,1)
$casts=@($r.Event.SkillCasts | Where-Object ActorId -eq 1 | Sort-Object Delay)
Assert-Combo ($casts.Count -eq 6 -and ($casts[0..2].SkillEntityId -join ',') -eq '13100,13104,13105' -and $casts[-1].SkillEntityId -eq 13108) "Original stages / fixed-range fallback differ: $($casts.SkillEntityId -join ',')"
Assert-Combo ($f.Player.SkillState.Slots[0].ReleasedTimes -eq 1) 'Combo spent the ordinary release more than once'
$time=0.0
for ($i=0; $i -lt 6; $i++) {
    Assert-Combo ([Math]::Abs($casts[$i].Delay-$time) -lt 0.0002) "Segment $i start time differs"
    Assert-Combo ($casts[$i].SkillItemId -eq 10154) 'Segment lost root item'
    if ($i -gt 0) {
        $facing=if ($casts[$i].SkillEntityId -eq 13107) { 0 } else { 2 }
        Assert-Combo ($casts[$i].Presentation.FacingIndex -eq $facing) 'Continuation lost its original fixed facing'
    }
    if ($i -lt 5) { $time += $catalog.CreateSkill($casts[$i].SkillEntityId,-1,10154,5,20,1,$null).ActionDuration }
}
Assert-Combo ($r.Event.DamageEvents.Count -eq 6) "Original hit count differs: $($r.Event.DamageEvents.Count); $($r.Event.DamageEvents.SkillEntityId -join ',')"
Assert-Combo ($f.Player.X -eq $casts[-1].Presentation.TargetX -and $f.Player.Y -eq $casts[-1].Presentation.TargetY+1) 'Last segment did not use its actual target fixed offset'
Assert-Combo (@($r.Event.Movements | Where-Object { $_.Delay -gt 1.0 -and $_.Path.Count -gt 2 }).Count -eq 0) 'A teleport became a walking path'
# Fixed facing preserves horizontal mirroring; the next ordinary move must still
# update it horizontally and retain it on a subsequent vertical leg.
$moveFlags=[Reflection.BindingFlags]'NonPublic,Instance'
$tryMove=$f.Executor.GetType().GetMethod('TryMoveActor',$moveFlags)
$moveEvents=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleMovementEvent]]::new()
$f.Player.HasFlipX=$true; $f.Player.FlipX=$false
$left=[Zjcs.Combat.Official.FightGridPoint]::new($f.Player.X-1,$f.Player.Y)
Assert-Combo ($tryMove.Invoke($f.Executor,[object[]]@($f.Player,$left,1,$moveEvents))) 'Ordinary left move could not follow a combo'
Assert-Combo ($f.Player.FlipX -and $f.Player.Direction -eq 1) 'Ordinary left movement retained the combo mirror'
$down=[Zjcs.Combat.Official.FightGridPoint]::new($f.Player.X,$f.Player.Y-1)
Assert-Combo ($tryMove.Invoke($f.Executor,[object[]]@($f.Player,$down,1,$moveEvents))) 'Ordinary vertical move could not follow a combo'
Assert-Combo ($f.Player.FlipX -and $f.Player.Direction -eq 0 -and $moveEvents.Count -eq 2) 'Vertical movement reset the independent horizontal mirror'

# Block the right offset. The third configured segment must use one legal fallback,
# keep scheduling the remaining configured stages.
$blocked=New-Fixture { param($cp) for($y=0;$y -lt 10;$y++) { $cp.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(5,$y)) } }
$b=$blocked.Executor.Step(0,1)
$bc=@($b.Event.SkillCasts | Where-Object ActorId -eq 1 | Sort-Object Delay)
Assert-Combo ($bc.Count -eq 6 -and $bc[2].SkillEntityId -ne 13105) "One-shot fallback differs: $($bc.SkillEntityId -join ',')"
Assert-Combo ($blocked.Player.SkillState.Slots[0].ReleasedTimes -eq 1) 'Fallback spent another root release'
Assert-Combo (@($b.Event.Movements | Where-Object { $_.Path[-1].X -eq 5 }).Count -eq 0) 'Fallback entered blocked offset'
$again=New-Fixture { param($cp) for($y=0;$y -lt 10;$y++) { $cp.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(5,$y)) } }
$againResult=$again.Executor.Step(0,1)
Assert-Combo (($againResult.Event.SkillCasts.SkillEntityId -join ',') -eq ($b.Event.SkillCasts.SkillEntityId -join ',') -and $again.Cp.RandomSeed -eq $blocked.Cp.RandomSeed) 'Seeded fallback is not deterministic'

# Direct fallback selection draws exactly once for the eligible skill and once for
# its eligible target. Inspect the production selector independently of damage rolls.
$direct=New-Fixture
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$stream=$direct.Executor.GetType().GetField('random',$flags).GetValue($direct.Executor)
$expected=[Zjcs.Combat.Official.FightRandomStream]::new($stream.Seed)
$null=$expected.Next(); $null=$expected.Next()
$arguments=[object[]]@($direct.Player,$direct.Skill,$segment,$null,$null)
$method=$direct.Executor.GetType().GetMethod('TryComboFallback',$flags)
Assert-Combo ($method.Invoke($direct.Executor,$arguments) -and $stream.Seed -eq $expected.Seed) 'Fallback random stream did not use exactly two draws'
$blockedSegment=$catalog.CreateSkill(13104,-1,10154,5,20,1,$null)
$blockedSegment.FallbackRange.Clear()
$before=$stream.Seed
$arguments=[object[]]@($direct.Player,$direct.Skill,$blockedSegment,$null,$null)
Assert-Combo (-not $method.Invoke($direct.Executor,$arguments) -and $stream.Seed -eq $before) 'No-candidate fallback consumed random or created a replacement'

# A large target keeps one identity, but each stage independently picks a legal
# in-range body cell. Nearest ties preserve the original body-offset order.
$large=New-Fixture { param($cp,$p,$e)
    $p.Y=1; $e.BodyOffsets.Clear()
    for($y=-1;$y -le 1;$y++) { for($x=-1;$x -le 1;$x++) {
        $e.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))
    } }
}
$pick=$large.Executor.GetType().GetMethod('TryComboTargetPosition',$flags)
$stream=$large.Executor.GetType().GetField('random',$flags).GetValue($large.Executor)
$before=$stream.Seed
$arguments=[object[]]@($large.Player,$large.Skill,$large.Enemy,$null)
Assert-Combo ($pick.Invoke($large.Executor,$arguments) -and $arguments[3].X -eq 4 -and $arguments[3].Y -eq 3) 'Large-target selector did not pick nearest legal body cell'
$large.Cp.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(4,2))
$arguments=[object[]]@($large.Player,$large.Skill,$large.Enemy,$null)
Assert-Combo ($pick.Invoke($large.Executor,$arguments) -and $arguments[3].X -eq 3 -and $arguments[3].Y -eq 3) 'Blocked landing / equal-distance body order differs'
$large.Enemy.BodyOffsets.Reverse()
$arguments=[object[]]@($large.Player,$large.Skill,$large.Enemy,$null)
Assert-Combo ($pick.Invoke($large.Executor,$arguments) -and $arguments[3].X -eq 5 -and $arguments[3].Y -eq 3 -and $stream.Seed -eq $before) 'Body tie order used randomness or lost original order'
$large.Enemy.BodyOffsets.Reverse(); $large.Cp.Grid.BlockedPositions.Clear()
$lr=$large.Executor.Step(0,1)
Assert-Combo (@($lr.Event.SkillCasts | Where-Object ActorId -eq 1).Count -eq 6 -and $lr.Event.DamageEvents.Count -eq 6) 'Large target did not receive six ordinary segment hits'
Assert-Combo (@($lr.Event.SkillCasts | Where-Object { $_.ActorId -eq 1 -and ($_.TargetIds.Count -ne 1 -or $_.TargetIds[0] -ne 2) }).Count -eq 0) 'A body cell became a separate combat target'
$rangeOnly=New-Fixture { param($cp,$p,$e,$s)
    $p.Y=1; $e.BodyOffsets.Clear()
    for($y=-1;$y -le 1;$y++) { for($x=-1;$x -le 1;$x++) {
        $e.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))
    } }
    $s.Range.Clear(); $s.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,3))
}
$arguments=[object[]]@($rangeOnly.Player,$rangeOnly.Skill,$rangeOnly.Enemy,$null)
Assert-Combo (-not $pick.Invoke($rangeOnly.Executor,$arguments)) 'Large-target selector used an out-of-range body cell'

# Source snapshots and current-source anchors remain distinct after source motion.
$source=New-Unit 7 1 1 2 1; $target=New-Unit 8 2 7 8 1
$view=[Zjcs.Combat.Official.FightBattleSkillCastPresentation]::new(); $view.SourceX=3; $view.SourceY=4
foreach($anchor in 0,1,2,3) {
    $point=[Zjcs.Combat.Official.FightHitAnchorExtensions]::ResolveCenter($anchor,$source,$target,$view)
    $xy=@('7,8','3,4','1,2','7,8')[$anchor]
    Assert-Combo ("$($point.X),$($point.Y)" -eq $xy) "Original anchor $anchor differs"
}
foreach($anchor in 1,2,3) {
    $configure={ param($cp,$p,$e,$skill)
        $skill.ComboSkills.Clear(); $skill.HitGraphRoots.Clear(); $skill.HitGraphRootEntityIds.Clear()
        $skill.HitGraphRootEntityId=0; $skill.HitGraphEntities.Clear()
        $hit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new()
        $hit.EntityId=99001; $hit.DamageId=99002; $hit.DamageType=8; $hit.ActionType=4
        $hit.Anchor=$anchor; $hit.Delay=0.85; $hit.HasCustomDamage=$true; $hit.CustomDamage=10
        $offset=if($anchor -eq 1){2}elseif($anchor -eq 2){1}else{0}
        $hit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,$offset))
        $skill.Hits.Add($hit)
    }.GetNewClosure()
    $moving=New-Fixture $configure
    $moveResult=$moving.Executor.Step(0,1)
    Assert-Combo ($moving.Player.X -eq 4 -and $moving.Player.Y -eq 3 -and $moving.Enemy.Hp -eq 999990) "Moved-source anchor $anchor did not hit its intended cell"
}

$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.Cp)
$roundtrip=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$legacy=$roundtrip.Units[0].Skills[0]
$legacy.SequenceDefinitionResolved=$false; $legacy.ComboSkills.Clear(); $legacy.SourceMoves[0].AiMustMoveTo=$false
$formula=[Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(),[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$null=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($roundtrip,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
Assert-Combo ($legacy.ComboSkills.Count -eq 5 -and $legacy.SourceMoves[0].AiMustMoveTo -and $roundtrip.Units[0].SkillState.Slots[0].ReleasedTimes -eq 1) 'Legacy configuration recovery changed cast state or omitted combo data'

# Defeat on the first hit stops the sequence. No cooldown/release reset on resume.
$dead=New-Fixture { param($cp,$p,$e,$s) $e.Hp=$e.Turn.Hp=1; $s.HasCustomDamage=$true; $s.CustomDamage=100 }
$d=$dead.Executor.Step(0,1)
Assert-Combo (@($d.Event.SkillCasts | Where-Object ActorId -eq 1).Count -eq 1) 'Combo continued after all enemies died'
$far=New-Fixture { param($cp,$p,$e,$s)
    $e.Hp=$e.Turn.Hp=1; $s.HasCustomDamage=$true; $s.CustomDamage=100
    $other=New-Unit 3 2 9 9 1; $cp.Units.Add($other); $cp.Waves[0].UnitIds.Add(3)
}
$fr=$far.Executor.Step(0,1)
Assert-Combo (@($fr.Event.SkillCasts | Where-Object ActorId -eq 1).Count -eq 1 -and $far.Cp.Units[2].Hp -gt 0) 'No-candidate continuation created a cast or harmed an out-of-range target'
Write-Host 'Original skill combo checks passed: six segments, original timing/offsets/teleports/facing, one ordinary release, blocked-offset fallback, large-target body selection, deterministic random and defeat/no-candidate stop.'

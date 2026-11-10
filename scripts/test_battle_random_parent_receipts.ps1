. (Join-Path $PSScriptRoot 'test_battle_hit_summon.ps1')

function Assert-Parent($condition,$message) { if(-not $condition){throw $message} }
function New-ParentBattle($skillId,$viewless,$duplicate) {
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.RandomSeed=11
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=16;Height=16;DefaultGroundType=0}
    foreach($id in 1..3) {
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id;$u.Side=if($id-eq 1){1}else{2};$u.CharacterType=$u.Side
        $u.X=5+3*($id-1);$u.Y=5;$u.Hp=$u.MaxHp=1000000;$u.Level=150;$u.SubRank=1
        $u.Direction=3;$u.HasFlipX=$true;$u.MoveMask=5
        $u.Turn=[Zjcs.Combat.Official.FightTurnUnit]@{Id=$id;Hp=1000000;Speed=100-$id}
        foreach($pair in @(@(1,10001),@(2,10003),@(3,1000000),@(4,(100-$id)))) {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        if($id-eq 1) {
            $skill=$catalog.CreateSkill($skillId,0,0,5,20,1,$null)
            if($duplicate) {
                $r=$skill.HitGraphRoots[0]
                $skill.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{
                    EntityId=$r.EntityId;Delay=$r.Delay+0.15;Anchor=$r.Anchor;IgnoreAICount=$r.IgnoreAICount})
            }
            $u.Skills.Add($skill)
            $u.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
        }
        if($id-eq 2) {
            $u.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
            $u.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,1))
            $u.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,1))
        }
        $cp.Units.Add($u)
    }
    $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$wave.UnitIds.Add(3);$cp.Waves.Add($wave)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $rootIds=[Collections.Generic.HashSet[int]]::new()
    foreach($root in $skill.HitGraphRoots){[void]$rootIds.Add($root.EntityId)}
    $parents=@($skill.HitGraphEntities|Where-Object {$rootIds.Contains($_.EntityId)})
    # Simulate the prior viewless collector after normal official catalog restoration;
    # all authored ranges, links, damage and random conditions remain untouched.
    if($viewless){foreach($parent in $parents){$parent.HitComponentType='Viewless reference fixture'}}
    $step=$runner.Step(0,1)
    foreach($parent in $parents){$parent.HitComponentType='FightHitRandomTargetComponent'}
    return @{State=$cp;Step=$step;Skill=$skill;RootIds=$rootIds}
}
function Normalize-ParentReceipts($battle) {
    foreach($receipt in $battle.State.Receipts) {
        foreach($cast in $receipt.Event.SkillCasts) {
            [void]$cast.HitSelections.RemoveAll([Predicate[Zjcs.Combat.Official.FightBattleHitSelectionSnapshot]]{
                param($hit) $battle.RootIds.Contains($hit.HitEntityId)})
            for($i=0;$i-lt $cast.HitSelections.Count;$i++){$cast.HitSelections[$i].Sequence=$i+1}
        }
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($battle.State)
}
foreach($skillId in @(10910,10920,13520)) {
    foreach($duplicate in @($false,$true)) {
        $before=New-ParentBattle $skillId $true $duplicate
        $after=New-ParentBattle $skillId $false $duplicate
        $cast=@($after.Step.Event.SkillCasts|Where-Object SkillEntityId -eq $skillId)[0]
        $roots=@($cast.HitSelections|Where-Object {$after.RootIds.Contains($_.HitEntityId)})
        Assert-Parent ($roots.Count-eq $after.Skill.HitGraphRoots.Count) 'Root references lost their independent parent receipts'
        for($i=0;$i-lt $roots.Count;$i++) {
            $root=$roots[$i];$cfg=$after.Skill.HitGraphRoots[$i]
            Assert-Parent ($root.Anchor-eq $cfg.Anchor -and [Math]::Abs($root.Delay-$cfg.Delay)-lt 0.00001 -and $root.EffectCenters.Count-eq 1 -and $root.EffectDelays.Count-eq 1) 'Parent anchor, creation time or effect centre missing'
            Assert-Parent ($root.Targets.Count-eq 0 -and $root.SelectedGridPositions.Count-eq 0 -and $root.RandomTargetGroupId-eq 0) 'Parent view repeated a child random draw/damage target'
        }
        $plain=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($after.Skill)
        Assert-Parent (@($plain|Where-Object IsViewOnly).Count-eq 0) 'View-only roots entered gameplay/AI hit counting'
        $execution=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($after.Skill,$true)
        Assert-Parent (@($execution|Where-Object IsViewOnly).Count-eq $roots.Count) 'Execution collector lost parent view definitions'
        $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($after.State))
        $restoredCast=@($restored.Receipts[0].Event.SkillCasts|Where-Object SkillEntityId -eq $skillId)[0]
        Assert-Parent (@($restoredCast.HitSelections|Where-Object {$after.RootIds.Contains($_.HitEntityId)}).Count-eq $roots.Count) 'Checkpoint lost parent receipts'
        Assert-Parent ((Normalize-ParentReceipts $before)-ceq (Normalize-ParentReceipts $after)) 'Parent receipt changed random stream, child selection, damage, statuses, timing or checkpoint gameplay'
    }
}
Write-Output 'Random parent receipts passed: three original skills, repeated roots, independent anchors/delays, no extra draws/damage/AI counts and unchanged normalized checkpoint gameplay.'

. (Join-Path $PSScriptRoot 'test_battle_hit_summon.ps1')

function New-PlacementBattle($skillId, $direction) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.RandomSeed = 11
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{Width=16;Height=16;DefaultGroundType=0}
    foreach ($id in 1..2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id; $u.Side=$id; $u.CharacterType=if($id-eq 1){1}else{2}
        $u.X=5+3*($id-1); $u.Y=5; $u.Hp=$u.MaxHp=1000000; $u.Level=150; $u.SubRank=1
        $u.Direction=$direction; $u.HasFlipX=$true; $u.MoveMask=5
        $u.Turn=[Zjcs.Combat.Official.FightTurnUnit]@{Id=$id;Hp=1000000;Speed=100-$id}
        foreach ($pair in @(@(1,10001),@(2,10003),@(3,1000000),@(4,(100-$id)))) {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        if($id-eq 1) {
            $u.Skills.Add($catalog.CreateSkill($skillId,0,0,5,20,1,$null))
            $u.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
        }
        $cp.Units.Add($u)
    }
    $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$cp.Waves.Add($wave)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $step=$runner.Step(0,1)
    return @{State=$cp;Step=$step;Cast=@($step.Event.SkillCasts|Where-Object SkillEntityId -eq $skillId)[0]}
}
function Assert-Placement($condition,$message) { if(-not $condition){throw $message} }
$offsets=@(@(0,-2),@(-2,0),@(0,2),@(2,0))
$facings=[Collections.Generic.HashSet[int]]::new()
foreach($direction in 0..3) {
    $battle=New-PlacementBattle 12140 $direction
    $cast=$battle.Cast; $receipt=@($cast.HitSelections|Where-Object HitEntityId -eq 12141)[0]
    [void]$facings.Add($receipt.FacingIndex)
    $summons=@($battle.State.Units|Where-Object MasterId -eq 1)
    Assert-Placement ($summons.Count-eq 1) 'Original 12140 did not create one summon'
    $offset=$offsets[$receipt.FacingIndex];$center=$receipt.EffectCenters[0]
    Assert-Placement ($summons[0].X-eq $center.X+$offset[0] -and $summons[0].Y-eq $center.Y+$offset[1]) 'Summon did not use the native scope direction and captured hit centre'
}
Assert-Placement ($facings.Count-eq 4) 'Four-direction summon fixtures did not exercise four actual cast facings'
$battle=New-PlacementBattle 12200 3
$doll=@($battle.State.Units|Where-Object {$null-ne $_.VoodooDoll})[0]
Assert-Placement ($null-ne $doll -and $doll.X-eq 7 -and $doll.Y-eq 5 -and $doll.VoodooDoll.TargetId-eq 2) "Voodoo placement differs: pos=$($doll.X),$($doll.Y) target=$($doll.VoodooDoll.TargetId) caster=$($battle.Cast.Presentation.SourceX),$($battle.Cast.Presentation.SourceY) facing=$($battle.Cast.Presentation.FacingIndex)"
$battle=New-PlacementBattle 57780 3
$summons=@($battle.State.Units|Where-Object MasterId -eq 1|Sort-Object Id)
Assert-Placement ($summons.Count-eq 2 -and $summons[0].X-eq 9 -and $summons[0].Y-eq 4 -and $summons[1].X-eq 9 -and $summons[1].Y-eq 5) 'Half-cell centre or original per-scope creation order was lost'
Write-Output 'Summon placement passed: native four directions, source-anchored reference separation, half-cell centre and original scope order.'

$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (Get-FightCombatSources)
$root=Split-Path -Parent $PSScriptRoot
function Input-Text($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Input-Hash($name) { (Get-FileHash (Join-Path $root "unity/Assets/Official/Combat/$name.json")).Hash.ToLowerInvariant() }
function Check($ok,$message) { if (!$ok) { throw $message } }
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Input-Text 'skill-rules'),(Input-Hash 'skill-rules'),'854_156383',(Input-Text 'battle-rules'),(Input-Hash 'battle-rules'),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Input-Text 'rules'),(Input-Hash 'rules'),'854_156383')
$formula=$catalog.CreateDamageFormula((Input-Text 'rules'),(Input-Hash 'rules'),'854_156383')
function Unit($id,$side,$type,$x,$speed) {
    $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id=$id; $u.Side=$side; $u.CharacterType=$type; $u.Level=20; $u.SubRank=1
    $u.X=$x; $u.Y=4; $u.Hp=$u.MaxHp=$u.LastMaxHp=100000
    $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=$speed
    foreach($prop in @(@(1,100),@(2,10),@(3,100000),@(32,100),@(33,100))) {
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$prop[0];Value=$prop[1]})
    }
    return $u
}
foreach($onAlly in @($false,$true)) {
foreach($type in @(1,2,3,4,5,7,8)) {
foreach($direction in 0..3) {
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1; $cp.MaxRound=10; $cp.RandomSeed=73
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=12;DefaultGroundType=2}
    $actor=Unit 1 1 $(if($onAlly){1}else{$type}) 3 1000
    $actor.Direction=$direction
    $ally=Unit 2 1 $(if($onAlly){$type}else{1}) 4 1
    $ally.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0))
    $ally.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
    $enemy=Unit 3 2 2 5 1; $outside=Unit 4 1 1 10 1
    $victim=if($onAlly){$ally}else{$actor}
    foreach($id in @(24040,24041)) {
        foreach($u in @($victim,$enemy,$outside)) {
            $u.Statuses.Add($catalog.CreateStatus($id,$u.Id,1,$id*10+$u.Id))
        }
    }
    $skill=$catalog.CreateSkill(11550,0,10066,2,20,1,$null)
    $actor.Skills.Add($skill); $actor.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,0))
    foreach($u in @($actor,$ally,$enemy,$outside)) { $cp.Units.Add($u) }
    $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(3); $cp.Waves.Add($wave)
    $executor=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $before=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    $step=$executor.Step(0,1)
    $excluded=$type -in @(3,7)
    $remaining=@($victim.Statuses | Where-Object ClassId -in @(24040,24041)).Count
    Check ($remaining -eq $(if($excluded){2}else{1})) "Wrong original 11552 dispel for character type $type : $remaining"
    Check (@($victim.Statuses | Where-Object ClassId -eq 24237).Count -eq 1) "Dispel exclusion incorrectly suppressed the original buff for type $type"
    Check ($step.Event.DispersedUnitIds.Contains($victim.Id) -eq !$excluded) "Wrong dispel notification for type $type"
    foreach($u in @($actor,$ally)) {
        Check (@($u.Statuses | Where-Object ClassId -eq 24237).Count -eq 1) "Original area buff missed friendly $($u.Id)"
        Check (@($step.Event.DamageEvents | Where-Object TargetId -eq $u.Id).Count -eq 1) "Status scope hit friendly $($u.Id) more than once"
    }
    foreach($u in @($enemy,$outside)) {
        Check (@($u.Statuses | Where-Object ClassId -in @(24040,24041)).Count -eq 2) "Invalid dispel target $($u.Id)"
        Check (@($u.Statuses | Where-Object ClassId -eq 24237).Count -eq 0) "Invalid buff target $($u.Id)"
    }
    Check ($actor.SkillState.Slots[0].ReleasedTimes -eq 1) 'Original skill consumed the wrong release count'
    Check ($executor.Step(0,1).Replayed) 'Receipt did not replay'
    $cold=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($before)
    $restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cold,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    [void]$restored.Step(0,1)
    Check ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cold)) "Cold checkpoint differs for type $type"
}
}
}
Write-Output 'Original 10066 -> 11550 -> 11552 passed 56 self/friendly and four-direction cases across seven character types: only 3/7 excluded, original area buff retained once per friendly despite a multi-cell body, enemy/out-of-scope filtered, one release, notifications, receipt replay and identical cold checkpoints.'

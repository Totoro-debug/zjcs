$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-DelayRule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-DelayRule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-Delay($condition,$message){if(-not $condition){throw $message}}
$skills=Read-DelayRule 'skill-rules';$battle=Read-DelayRule 'battle-rules';$rules=Read-DelayRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-DelayRule $skills),'854_156383',$battle,(Hash-DelayRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-DelayRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-DelayRule $rules),'854_156383')
function Invoke-DelayBattle($id,$restore,$legacy=$false,$refresh=$false,$foreignClock=$false){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.MaxRound=12;$cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
    foreach($unitId in 1,2){
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$u.Side=$unitId;$u.CharacterType=1;$u.X=$unitId+2;$u.Y=2
        $u.Hp=$u.MaxHp=$u.LastMaxHp=10000000;$u.Level=$u.SubRank=1
        foreach($pair in @(@(1,1000),@(2,1000),@(3,$u.MaxHp),@(4,$(if($unitId -eq 1){10000}else{1})))){$u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})}
        $u.Turn.Id=$unitId;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=$(if($unitId -eq 1){10000}else{1})
        $cp.Units.Add($u)
    }
    $ownerId=$(if($foreignClock){2}else{1})
    $source=$catalog.CreateStatus($id,$ownerId,1,990000+$id,$id,0,5,20,1,$null)
    if($foreignClock){$source.RoundStartListenTarget=1}
    Assert-Delay ($source.RoundStartRulesSupported -and $source.RoundStartDelayRound -eq 2 -and $source.RoundStartDelayRoundType -eq 1) "Original $id delayed definition is still blocked or changed"
    $cp.Units[$ownerId-1].Statuses.Add($source)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    Assert-Delay ($source.RoundStartDelayStartRound -eq 0 -and $source.RoundStartTriggeredCount -eq 0) 'Preparation did not retain its initial listener clock or invoked early'
    $expected=0
    $round=1
    while($round -le 7){
        if($restore -or $legacy){
            $json=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
            if($legacy -and $round -eq 2){
                # A formerly blocked status already has its real application time.
                $json=$json -replace '"RoundStartDelayRoundType":1,','' -replace '"RoundStartDelayStartRound":0,','' -replace '"RoundStartDelayRulesResolved":true,','' -replace '"RoundStartFilterRulesResolved":true,','' -replace '"RoundStartRequiresFriendWithoutStatuses":true,','' -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
            }
            $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
            $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
            $source=$cp.Units[$ownerId-1].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s)$s.ClassId -eq $id})
        }
        $result=$runner.Step(0,$cp.Counter+1)
        if($cp.Units[0].SkillState.Round -eq $round-1){
            Assert-Delay ($cp.Counter -lt 40) 'Delayed invocation never completed its pending status starts'
            $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
            Assert-Delay ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Repeated startup receipt advanced a delayed status'
            continue
        }
        Assert-Delay ($cp.Units[0].SkillState.Round -eq $round) 'Delay fixture did not advance the intended personal listener'
        $eligible=$round -ge 3 -and (-not $refresh -or $round -eq 3 -or $round -ge 6)
        if($eligible -and ($id -eq 25140 -or $expected -eq 0)){$expected++}
        $active=$cp.Units[$ownerId-1].Statuses|Where-Object ClassId -eq $id|Select-Object -First 1
        # Finite definitions expire after their one real third-round invocation.
        if($active){Assert-Delay ($active.RoundStartTriggeredCount -eq $expected) "Original $id invoked before delay, waited again after success, or lost its count at round $round"}
        if($id -notin 25140,25213,25285 -and $round -ge 3){Assert-Delay ($null -eq $active) "Original $id did not expire after its third-round invocation"}
        if($id -notin 25140,25213){
            $skillId=$(if($id -eq 16693){16750}elseif($id -eq 25285){13370}else{$id+1})
            $casts=@($result.Event.SkillCasts|Where-Object SkillEntityId -eq $skillId)
            Assert-Delay ($casts.Count -eq [int]($round -eq 3)) "Original $id did not execute its actual configured skill exactly in round three"
        }elseif($expected -gt 0){
            $effectId=$(if($id -eq 25140){24431}else{24852})
            Assert-Delay ($cp.Units[$ownerId-1].Statuses.ClassId -contains $effectId) "Original $id did not apply its configured status after the delay"
        }
        if($refresh -and $round -eq 3){
            $old=$active
            $template=$catalog.CreateStatus($id,$ownerId,1,999000,$id,0,5,20,1,$null)
            if($foreignClock){$template.RoundStartListenTarget=1}
            $renewed=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($cp.Units[$ownerId-1],$template,$cp.Units[$ownerId-1].SkillState.Round,999000)
            Assert-Delay ([object]::ReferenceEquals($old,$renewed) -and $renewed.RoundStartTriggeredCount -eq 1 -and $renewed.RoundStartMeetProgress -eq 1) 'Delay renewal lost its live instance, calls or meet progress'
        }
        $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        Assert-Delay ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Repeated receipt restarted or advanced a delayed status'
        $round++
    }
    Assert-Delay ($expected -gt 0) 'Delayed original never produced an invocation'
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
foreach($id in 64603,64613,64623,64633,64643,16693,25140,25213,25285){
    $continuous=Invoke-DelayBattle $id $false
    Assert-Delay ($continuous -eq (Invoke-DelayBattle $id $true)) "Original $id changed after per-round checkpoint restore"
    Assert-Delay ($continuous -eq (Invoke-DelayBattle $id $true $true)) "Previously blocked original $id restarted its delay after legacy restore"
}
foreach($foreignClock in $false,$true){
    $continuous=Invoke-DelayBattle 25140 $false $false $true $foreignClock
    Assert-Delay ($continuous -eq (Invoke-DelayBattle 25140 $true $false $true $foreignClock)) 'Refreshed delay or independent listener clock changed after checkpoint restore'
}
# Waiting must precede probability draws and MeetCount accumulation, even when TryAtStart is set.
$owner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Hp=1000;MaxHp=1000}
$template=$catalog.CreateStatus(25140,1,1,1,25140)
$template.RoundStartTryAtStart=$true;$template.RoundStartRate=0.5;$template.RoundStartMeetCount=2
$source=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,$template,0,1)
$rng=[Zjcs.Combat.Official.FightRandomStream]::new(23)
foreach($round in 0..2){
    $owner.SkillState.Round=$round;$seed=$rng.Seed
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,[Zjcs.Combat.Official.FightBattleUnitState[]]@($owner),$rng,$catalog,$source,($round -eq 0))|Out-Null
    Assert-Delay ($source.RoundStartTriggeredCount -eq 0 -and $source.RoundStartMeetProgress -eq 0 -and $rng.Seed -eq $seed) 'Waiting consumed RNG, accumulated conditions or honored TryAtStart despite its delay'
}
# DelayRoundType.GlobalRound uses the shared master-unit clock rather than the listener clock.
$owner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Side=1;Hp=1000;MaxHp=1000;CharacterType=1}
$owner.Turn.Id=1;$owner.Turn.Hp=1000;$owner.Turn.RoundOffset=0;$owner.Turn.ActionRound=0
$owner.SkillState.Round=7
$ally=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;Side=1;Hp=1000;MaxHp=1000;CharacterType=1}
$ally.Turn.Id=2;$ally.Turn.Hp=1000;$ally.Turn.RoundOffset=0;$ally.Turn.ActionRound=0
$globalUnits=[Zjcs.Combat.Official.FightBattleUnitState[]]@($owner,$ally)
$template=[Zjcs.Combat.Official.FightBattleStatusState]@{InstanceId=1;OwnerId=1;CreatorId=1;ClassId=990001;
    HasRoundStartTrigger=$true;RoundStartRulesSupported=$true;RoundStartDelayRound=1;RoundStartDelayRoundType=2;
    RoundStartDelayStartRound=1;RoundStartListenTarget=2;RoundStartRate=1;RoundStartMeetCount=2}
$owner.Statuses.Add($template);$rng=[Zjcs.Combat.Official.FightRandomStream]::new(23)
foreach($actionRound in 0,1){
    $owner.Turn.ActionRound=$actionRound;$ally.Turn.ActionRound=$actionRound;$seed=$rng.Seed
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,
        $globalUnits,$rng,$null)|Out-Null
    Assert-Delay ($rng.Seed -eq $seed -and $template.RoundStartMeetProgress -eq 0) 'Global delay consumed probability or progress before its configured global rounds elapsed'
}
$owner.Turn.ActionRound=2;$ally.Turn.ActionRound=2;$seed=$rng.Seed
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,
    $globalUnits,$rng,$null)|Out-Null
Assert-Delay ($template.RoundStartMeetProgress -eq 1) 'Global delay followed the leading listener clock or did not resume after all master units advanced'
# Original 25285 checks any alive/present friend (self, NPC or summon), before probability/progress.
foreach($kind in 'self','player','npc','summon','enemy','dead','departed','excluded24207','excluded24301'){
    $owner=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Side=1;Hp=1000;MaxHp=1000;CharacterType=1}
    $owner.SkillState.Round=0
    $template=$catalog.CreateStatus(25285,1,1,1,25285)
    $template.RoundStartRate=0.5;$template.RoundStartMeetCount=2
    $source=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner,$template,0,1)
    $owner.SkillState.Round=3
    $candidate=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;Side=1;Hp=1000;MaxHp=1000;CharacterType=1}
    if($kind -ne 'self'){$owner.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=24207})}
    switch($kind){
        'npc' {$candidate.CharacterType=3}
        'summon' {$candidate.CharacterType=2;$candidate.MasterId=1;$candidate.Targetable=$false}
        'enemy' {$candidate.Side=2}
        'dead' {$candidate.Hp=0}
        'departed' {$candidate.Ended=$true}
        'excluded24207' {$candidate.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=24207})}
        'excluded24301' {$candidate.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=24301})}
    }
    $valid=$kind -in 'self','player','npc','summon'
    if($kind -eq 'self'){$candidate.Side=2}
    $rng=[Zjcs.Combat.Official.FightRandomStream]::new(23);$seed=$rng.Seed
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,[Zjcs.Combat.Official.FightBattleUnitState[]]@($owner,$candidate),$rng,$catalog)|Out-Null
    Assert-Delay (($rng.Seed -ne $seed) -eq $valid -and $source.RoundStartTriggeredCount -eq 0 -and $source.RoundStartMeetProgress -le [int]$valid) "Original friend filter ignored the $kind condition or evaluated RNG/progress first"
}
Write-Output 'Round delay passed: nine original definitions, actual third-round skills/statuses, one initial wait, renewal and retained counters, independent listener/lifetime clocks, ignored startup, no early RNG/progress, self/player/NPC/summon friend filter and exclusions, legacy continuation and exact per-round restore.'

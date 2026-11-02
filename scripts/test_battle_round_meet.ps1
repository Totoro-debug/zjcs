$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-MeetRule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-MeetRule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-Meet($condition,$message){if(-not $condition){throw $message}}
$skills=Read-MeetRule 'skill-rules';$battle=Read-MeetRule 'battle-rules';$rules=Read-MeetRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-MeetRule $skills),'854_156383',$battle,(Hash-MeetRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-MeetRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-MeetRule $rules),'854_156383')
foreach($id in 24026,24027,24614){Assert-Meet ($catalog.CreateStatus($id,1,1).RoundStartRulesSupported) "Original $id remains blocked"}
Assert-Meet (-not $catalog.CreateStatus(24643,1,1).RoundStartRulesSupported) 'MeetCount support silently enabled the unimplemented 24643 special handler'
Assert-Meet ($catalog.CreateStatus(24643,1,1).RoundStartResetMeetCountOnInvoke) 'Original reset flag was not parsed'
function New-MeetCheckpoint($statusId,$reset){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.MaxRound=20;$cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
    foreach($id in 1..2){
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id;$u.Side=$id;$u.CharacterType=1;$u.X=$id+1;$u.Y=2
        $u.MaxHp=$u.LastMaxHp=$u.Hp=10000000;$u.Level=$u.SubRank=1
        foreach($pair in @(@(1,1000),@(2,1000),@(3,$u.MaxHp),@(4,$(if($id -eq 1){100}else{1})))){
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        $u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    $source=$catalog.CreateStatus($statusId,1,1,990000+$statusId,0,0,5,20,1,$null)
    $source.RoundStartResetMeetCountOnInvoke=$reset
    $cp.Units[0].Statuses.Add($source)
    return $cp
}
function Invoke-MeetBattle($statusId,$reset,$restore){
    $cp=New-MeetCheckpoint $statusId $reset
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $threshold=$cp.Units[0].Statuses[0].RoundStartMeetCount
    foreach($round in 1..7){
        if($restore){
            $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
            $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        }
        $runner.Step(0,$cp.Counter+1)|Out-Null
        $owner=$cp.Units[0];$source=$owner.Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s)$s.ClassId -eq $statusId})
        $count=if($reset){[int][math]::Floor($round/$threshold)}else{[math]::Max(0,$round-$threshold+1)}
        $progress=if($reset){$round%$threshold}else{[math]::Min($round,$threshold)}
        Assert-Meet ($owner.SkillState.Round -eq $round -and $source.RoundStartTriggeredCount -eq $count -and $source.RoundStartMeetProgress -eq $progress) "MeetCount/reset timing changed for $statusId round $round"
        $effectId=if($statusId -eq 24614){24611}else{24478}
        $effect=$owner.Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s)$s.ClassId -eq $effectId})
        Assert-Meet (($effect -ne $null) -eq ($count -gt 0)) "Original $statusId applied its configured state before/after the wrong threshold"
        if($effect){
            Assert-Meet ($effect.SourceSkillRank -eq 5 -and $effect.SourceSkillLevel -eq 20) 'Meeting threshold lost original cultivation'
            if($effectId -eq 24478){Assert-Meet ($effect.StackCount -eq [math]::Min(5,$count)) 'Repeated qualifying rounds did not stack the original 24478 state'}
        }
        if($statusId -eq 24027){Assert-Meet (($owner.Statuses.ClassId -contains 24633) -eq ($count -gt 0)) 'The second original 24027 effect was lost'}
        $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        Assert-Meet ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Receipt replay accumulated another condition'
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
foreach($id in 24026,24027,24614){
    foreach($reset in $false,$true){
        Assert-Meet ((Invoke-MeetBattle $id $reset $false) -eq (Invoke-MeetBattle $id $reset $true)) "Meet progress diverged across every-turn restore for $id reset=$reset"
    }
}
# Legacy unsupported definitions never recorded a condition history. Restore the
# newly supported selector without inventing past matches, then count new rounds.
$cp=New-MeetCheckpoint 24026 $false
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
$initial=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
$continued=@(foreach($legacy in $false,$true){
    $json=$initial
    if($legacy){
        $json=$json -replace '"RoundStartMeetRulesResolved":true,','' -replace '"RoundStartMeetProgress":0,','' -replace '"RoundStartResetMeetCountOnInvoke":false,',''
        $json=$json -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
    }
    $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    foreach($round in 1..4){$runner.Step(0,$cp.Counter+1)|Out-Null}
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
})
Assert-Meet ($continued[0] -eq $continued[1]) 'Legacy unsupported definition changed new matching rounds'
# A previously supported one-condition source can infer a reached gate from its
# recorded successful invocation; no replay or extra condition is introduced.
$cp=New-MeetCheckpoint 25077 $false
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
$runner.Step(0,1)|Out-Null
$initial=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
$continued=@(foreach($legacy in $false,$true){
    $json=$initial
    if($legacy){$json=$json -replace '"RoundStartMeetRulesResolved":true,','' -replace '"RoundStartMeetProgress":1,','' -replace '"RoundStartResetMeetCountOnInvoke":false,',''}
    $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $runner.Step(0,$cp.Counter+1)|Out-Null
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
})
Assert-Meet ($continued[0] -eq $continued[1]) 'Legacy one-condition invocation lost its reached gate'
# Final call must finish before the user-confirmed remaining-count removal check.
$cp=New-MeetCheckpoint 24026 $false;$source=$cp.Units[0].Statuses[0]
$source.RoundStartMaxCount=2;$source.RoundStartRemoveWhenNotEnoughTimes=$true
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
foreach($round in 1..3){
    $runner.Step(0,$cp.Counter+1)|Out-Null
    Assert-Meet ($cp.Units[0].Statuses.Contains($source) -eq ($round -lt 3)) 'Exhaustion removed a source before its first threshold invocation'
}
Assert-Meet ($cp.Units[0].Statuses.ClassId -contains 24478) 'Exhaustion discarded the final configured effect'
# Failed selection keeps a reached gate; reset only follows an actual invocation.
$cp=New-MeetCheckpoint 24026 $true;$source=$cp.Units[0].Statuses[0];$owner=$cp.Units[0]
$source.RoundStartStatuses.Clear()
$source.RoundStartSkills.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{EntityId=12180;Source=2;Target=7;TargetPos=1;BasePercent=1})
$random=[Zjcs.Combat.Official.FightRandomStream]::new(23)
foreach($i in 1..4){[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)|Out-Null}
Assert-Meet ($source.RoundStartMeetProgress -eq 3 -and $source.RoundStartTriggeredCount -eq 0) 'An empty target reset or consumed the met gate'
$cp.Units[1].Side=1
$invocations=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)
Assert-Meet ($invocations.Count -eq 1 -and $invocations[0].TargetUnitId -eq 2 -and $source.RoundStartMeetProgress -eq 0 -and $source.RoundStartTriggeredCount -eq 1) 'An eligible target did not invoke/reset the retained gate'
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$cp.Units[1],$cp.Units,$random,$catalog)|Out-Null
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog,$null,$false,5)|Out-Null
Assert-Meet ($source.RoundStartMeetProgress -eq 0) 'Another listener or phase advanced the condition gate'
$source.RoundStartRate=0
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)|Out-Null
Assert-Meet ($source.RoundStartMeetProgress -eq 0) 'A failed condition probability advanced the gate'
Write-Output 'Round meet checks passed: originals 24026/24027/24614, actual configured states, reached gates, resetting fixtures, empty selection retry, final-invocation removal, cultivation, receipts and every-turn checkpoint continuation; 24643 handler stays blocked.'

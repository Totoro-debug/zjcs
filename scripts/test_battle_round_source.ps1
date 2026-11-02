$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-SourceRule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-SourceRule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-RoundSource($condition,$message){if(-not $condition){throw $message}}
$skills=Read-SourceRule 'skill-rules';$battle=Read-SourceRule 'battle-rules';$rules=Read-SourceRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-SourceRule $skills),'854_156383',$battle,(Hash-SourceRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-SourceRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-SourceRule $rules),'854_156383')
function New-SourceCheckpoint($statusId){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.MaxRound=12;$cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
    foreach($id in 1..2){
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id;$u.Side=$id;$u.CharacterType=1;$u.X=$id+1;$u.Y=2
        $u.MaxHp=$u.LastMaxHp=$u.Hp=10000000;$u.Level=$u.SubRank=1
        foreach($pair in @(@(1,$(if($id -eq 1){100}else{9000})),@(2,100),@(3,$u.MaxHp),@(4,$(if($id -eq 1){100}else{1})))){
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        $u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    # Original debuff on unit 1, created by its enemy 2. Personal rounds belong to 1;
    # its creator must remain 2 for casting, damage, cultivation and reporting.
    $cp.Units[0].Statuses.Add($catalog.CreateStatus($statusId,1,2,990000+$statusId,0,0,5,20,1,$null))
    return $cp
}
foreach($id in 24105,24135,24136,24179,24235,24243,24318,24333,24334,24343,24420,24481,24535,24538,24741,24813,24814,26056){
    Assert-RoundSource ($catalog.CreateStatus($id,1,2).RoundStartRulesSupported) "Original creator trigger $id remains unsupported"
}
foreach($id in 24037,24038,24045,24222,24472,24473,24474,24486,24640){
    Assert-RoundSource ($catalog.CreateStatus($id,1,2).RoundStartRulesSupported) 'Confirmed source-aware merged duration/count branch remains blocked'
}
function Invoke-SourceBattle($statusId,$restore,$legacy,$dead=$false,$sourcePosition=$false){
    $cp=New-SourceCheckpoint $statusId
    if($sourcePosition){$cp.Units[0].Statuses[0].RoundStartSkills[0].TargetPos=0}
    if($dead){
        $survivor=(New-SourceCheckpoint $statusId).Units[1]
        $survivor.Id=$survivor.Turn.Id=3;$survivor.X=5
        $cp.Units.Add($survivor)
        $cp.Units[1].Hp=$cp.Units[1].Turn.Hp=0
    }
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $skillId=$cp.Units[0].Statuses[0].RoundStartSkills[0].EntityId
    if($legacy){
        $json=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        $json=$json -replace '"RoundStartSourceRulesResolved":true,',''
        $json=$json -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    $source=$cp.Units[0].Statuses[0];$limit=$source.RoundStartMaxCount;$duration=$source.DurationRound
    foreach($round in 1..5){
        if($restore){
            $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
            $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        }
        $runner.Step(0,$cp.Counter+1)|Out-Null
        $event=$cp.Receipts[$cp.Receipts.Count-1].Event
        $casts=@($event.SkillCasts|Where-Object SkillEntityId -eq $skillId)
        $expected=($limit -lt 0 -or $round -le $limit) -and ($duration -lt 0 -or $round -le $duration)
        Assert-RoundSource ($casts.Count -eq [int]$expected -and $cp.Units[0].SkillState.Round -eq $round -and $cp.Units[1].SkillState.Round -eq 0) 'Creator cast advanced the wrong personal round or ignored its lifetime/cap'
        if($expected){
            Assert-RoundSource ($casts[0].ActorId -eq 2 -and $casts[0].TargetIds.Count -eq 1 -and $casts[0].TargetIds[0] -eq 1) 'Original creator trigger cast from its victim or attacked the creator'
            $damage=@($event.DamageEvents|Where-Object SkillEntityId -eq $skillId)
            if($sourcePosition){
                Assert-RoundSource ($damage.Count -eq 0) 'Source-centred one-cell scope incorrectly hit the adjacent victim'
            }else{
                Assert-RoundSource ($damage.Count -gt 0 -and @($damage|Where-Object {$_.SourceId -ne 2 -or $_.TargetId -ne 1 -or $_.Damage -ge 0 -or $_.AfterHp -ge $_.BeforeHp}).Count -eq 0) ("Original $statusId round $round creator damage failed: " + ($damage|ConvertTo-Json -Compress -Depth 6))
            }
        }
        $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        Assert-RoundSource ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Duplicate receipt repeated a creator trigger'
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
foreach($id in 24105,24135,24136,24243,24318){
    $expected=Invoke-SourceBattle $id $false $false
    Assert-RoundSource ($expected -eq (Invoke-SourceBattle $id $true $false)) "Per-round restore changed creator damage for $id"
    Assert-RoundSource ($expected -eq (Invoke-SourceBattle $id $true $true)) "Legacy definition changed creator continuation for $id"
    $expected=Invoke-SourceBattle $id $false $false $true
    Assert-RoundSource ($expected -eq (Invoke-SourceBattle $id $true $false $true)) "Per-round restore changed dead-creator damage for $id"
    Assert-RoundSource ($expected -eq (Invoke-SourceBattle $id $true $true $true)) "Legacy definition changed dead-creator continuation for $id"
}
# Original creator/self targets are independent of the status holder and listener.
foreach($id in 24235,24333,24334,24535,24538,24814,26056){
    $cp=New-SourceCheckpoint $id;$owner=$cp.Units[0];$creator=$cp.Units[1];$source=$owner.Statuses[0]
    $random=[Zjcs.Combat.Official.FightRandomStream]::new(23)
    $calls=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)
    Assert-RoundSource ($calls.Count -eq 0 -and $source.RoundStartMeetProgress -eq 0) 'Creator-listened source responded to its applicator instead'
    foreach($match in 1..$source.RoundStartMeetCount){
        $calls=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$creator,$cp.Units,$random,$catalog)
    }
    Assert-RoundSource ($calls.Count -eq 1 -and $calls[0].SourceUnitId -eq 2 -and $calls[0].TargetUnitId -eq 2 -and $calls[0].TargetPositionUnitId -eq 2 -and -not $calls[0].SelectTargets -and $calls[0].Trigger.SourceSkillRank -eq 5 -and $calls[0].Trigger.SourceSkillLevel -eq 20) 'Original creator/self trigger lost listener, explicit target, position or cultivation'
}
$expected=Invoke-SourceBattle 24105 $false $false $true $true
Assert-RoundSource ($expected -eq (Invoke-SourceBattle 24105 $true $false $true $true)) 'Dead creator source-position cast was lost or changed after restore'
$cp=New-SourceCheckpoint 24105;$source=$cp.Units[0].Statuses[0];$source.RoundStartSkills[0].TargetPos=0
$call=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($cp.Units[0],$cp.Units[0],$cp.Units,[Zjcs.Combat.Official.FightRandomStream]::new(23),$catalog)
Assert-RoundSource ($call.Count -eq 1 -and $call[0].TargetPositionUnitId -eq 2) 'Source position used the victim instead of the creator'
foreach($creatorId in 99,2){
    $cp=New-SourceCheckpoint 24105;$source=$cp.Units[0].Statuses[0];$source.CreatorId=$creatorId
    if($creatorId -eq 2){$cp.Units[1].Ended=$true}
    $call=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($cp.Units[0],$cp.Units[0],$cp.Units,[Zjcs.Combat.Official.FightRandomStream]::new(23),$catalog)
    Assert-RoundSource ($call.Count -eq 0 -and $source.RoundStartTriggeredCount -eq 0) 'Unavailable creator was replaced by the victim or consumed an actual call'
}
Write-Output 'Round source checks passed: original creator/victim damage, creator/self targets, listener and position, cultivation, finite duration/counts, receipts, legacy and per-turn continuation; source-aware merged-count definitions are enabled.'

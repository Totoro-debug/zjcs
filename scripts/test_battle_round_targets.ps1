$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-TargetRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-TargetRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function Assert-RoundTarget($condition,$message) { if (-not $condition) { throw $message } }
$skills=Read-TargetRule 'skill-rules'; $battle=Read-TargetRule 'battle-rules'; $rules=Read-TargetRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-TargetRule $skills),'854_156383',$battle,(Hash-TargetRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-TargetRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-TargetRule $rules),'854_156383')
$probe=$catalog.CreateStatus(25288,1,1,9925288,0,0,5,20,1,$null)
Assert-RoundTarget ($probe.RoundStartRulesSupported) 'Original 25288 StartEnd/minimum-HP target rules are not supported'
function ConvertTo-LegacyRoundTargetJson($json) {
    # Remove only the old component's missing definition, preserving unrelated state fields.
    return [regex]::Replace($json,'("HasRoundStartTrigger":true,)(.*?)("HasRoundEndTrigger":)',[Text.RegularExpressions.MatchEvaluator]{param($match)
        $section=$match.Groups[2].Value -replace '"RoundStartTargetRulesResolved":true,','' -replace '"RoundStartPhase":5,','' -replace '"RoundStartExcludedTargetStatuses":\[[0-9,]*\],',''
        $section=$section -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
        return $match.Groups[1].Value+$section+$match.Groups[3].Value
    })
}
function New-RoundTargetBattle($mode,$restore,$legacy = $false,$statusId = 25288) {
    $skillId=if($statusId -eq 25074){11790}else{61650}
    $protectionId=if($statusId -eq 25074){24304}else{24207}
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1; $cp.MaxRound=10; $cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=20;Height=8;DefaultGroundType=2}
    foreach ($id in 1..7) {
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id; $u.Side=1; $u.CharacterType=1; $u.X=$id+1; $u.Y=2
        $u.MaxHp=$u.LastMaxHp=1000000; $u.Hp=20000; $u.Level=$u.SubRank=1
        if ($id -eq 1) { $u.Hp=1 }
        if ($id -eq 2) { $u.Side=2; $u.Hp=1 }
        if ($id -eq 3) { $u.Hp=10000 }
        if ($id -eq 5) { $u.Hp=30000; $u.MaxHp=$u.LastMaxHp=5000000 }
        if ($id -eq 6) { $u.Hp=0 }
        if ($id -eq 7) { $u.Hp=1; $u.CharacterType=2; $u.MasterId=1; $u.Turn.MasterId=1 }
        foreach($pair in @(@(1,1000),@(2,1000),@(3,$u.MaxHp),@(4,$(if($id -eq 1){100}else{1})))) {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    # Excluded status identity is isolated input; its unrelated effects are not tested here.
    $cp.Units[2].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=$protectionId;OwnerId=3;CreatorId=3;InstanceId=9924207;DurationRound=-1;CurDurationRound=-1})
    if ($mode -in 'second','blocked') { $cp.Units[4].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=24301;OwnerId=5;CreatorId=5;InstanceId=9924301;DurationRound=-1;CurDurationRound=-1}) }
    if ($mode -eq 'blocked') { $cp.Units[3].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=$protectionId;OwnerId=4;CreatorId=4;InstanceId=9924208;DurationRound=-1;CurDurationRound=-1}) }
    $source=$catalog.CreateStatus($statusId,1,1,9925288,0,0,5,20,1,$null)
    if ($mode -eq 'phase') {
        $source.RoundStartTryAtStart=$false
        $cp.Units[2].Statuses.Clear(); $cp.Units[4].Hp=$cp.Units[4].Turn.Hp=500000
        # Start adds the excluded state before StartEnd chooses the actual skill target.
        $early=$catalog.CreateStatus(24514,3,1,9924514)
        $early.RoundStartTryAtStart=$false; $early.RoundStartListenTarget=1
        $early.RoundStartStatuses.Clear()
        $early.RoundStartStatuses.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{EntityId=$protectionId;Target=2;BasePercent=1})
        $cp.Units[2].Statuses.Add($early)
    }
    $cp.Units[0].Statuses.Add($source)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    if ($restore -or $legacy) {
        $json=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        if ($legacy) { $json=ConvertTo-LegacyRoundTargetJson $json }
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $source=$cp.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq $statusId})
    }
    $result=$runner.Step(0,1)
    $casts=@($result.Event.SkillCasts | Where-Object SkillEntityId -eq $skillId)
    if ($mode -eq 'blocked') {
        Assert-RoundTarget ($casts.Count -eq 0 -and $source.RoundStartTriggeredCount -eq 0) 'Excluded/dead/enemy/non-player targets consumed a round-start invocation'
        $cp.Units[3].Statuses.Clear()
        $result=$runner.Step(0,2)
        $casts=@($result.Event.SkillCasts | Where-Object SkillEntityId -eq $skillId)
    }
    $expected=if($mode -in 'second','blocked','phase'){4}else{5}
    Assert-RoundTarget ($casts.Count -eq 1 -and $casts[0].ActorId -eq 1 -and $casts[0].TargetIds.Count -eq 1 -and $casts[0].TargetIds[0] -eq $expected) "Original $statusId selected the wrong eligible minimum-HP player in $mode"
    Assert-RoundTarget ($cp.Units[$expected-1].Statuses.ClassId -contains $protectionId -and $source.RoundStartTriggeredCount -eq 1) "Original $skillId did not apply its configured $protectionId protection"
    $expectedRound=if($mode -eq 'blocked'){2}elseif($mode -eq 'phase' -or $statusId -eq 25074){1}else{0}
    Assert-RoundTarget ($cp.Units[0].SkillState.Round -eq $expectedRound) 'StartEnd ran in the wrong personal turn'
    $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    Assert-RoundTarget ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Replayed round-start target receipt changed the checkpoint'
    return $saved
}
foreach($statusId in 25288,25074) {
    foreach($mode in 'minimum','second','blocked','phase') {
        Assert-RoundTarget ((New-RoundTargetBattle $mode $false $false $statusId) -eq (New-RoundTargetBattle $mode $true $false $statusId)) "Round-start targeting changed after preparation restore in $mode for $statusId"
    }
    Assert-RoundTarget ((New-RoundTargetBattle 'minimum' $false $false $statusId) -eq (New-RoundTargetBattle 'minimum' $true $true $statusId)) "Legacy checkpoint lost target exclusions or StartEnd for $statusId"
    $saved=New-RoundTargetBattle 'minimum' $false $false $statusId
    $continued=@(foreach($legacy in $false,$true) {
        $json=if($legacy){ConvertTo-LegacyRoundTargetJson $saved}else{$saved}
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $previousProtection=$cp.Units[4].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq 24304})
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $result=$runner.Step(0,$cp.Counter+1)
        $source=$cp.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq $statusId})
        if($statusId -eq 25288) {
            Assert-RoundTarget ($source.RoundStartTriggeredCount -eq 1 -and @($result.Event.SkillCasts | Where-Object SkillEntityId -eq 61650).Count -eq 0) 'A restored finite trigger repeated its completed startup'
        } else {
            $casts=@($result.Event.SkillCasts | Where-Object SkillEntityId -eq 11790)
            # Original 24304 expires at its creator's next Start (RoundTarget=1,
            # DurationRound=1, RoundUpdateTiming=0), before this StartEnd selection.
            $renewed=$cp.Units[4].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq 24304})
            Assert-RoundTarget ($previousProtection -ne $null -and $source.RoundStartTriggeredCount -eq 2 -and $casts.Count -eq 1 -and $casts[0].TargetIds[0] -eq 5 -and -not [object]::ReferenceEquals($renewed,$previousProtection) -and $renewed.StartRound -eq 2) 'An unlimited restored trigger lost its counter or selected before the original protection expiry'
        }
        [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    })
    Assert-RoundTarget ($continued[0] -eq $continued[1]) "A partially invoked legacy checkpoint changed the next round for $statusId"
}
Write-Output 'Round-start targets passed: original 25288 -> 61650 -> 24207 and 25074 -> 11790 -> 24304, exclude-before-minimum ratio, alive friend player filtering, no-eligible retry, Start/StartEnd ordering, receipts, legacy and deterministic restore.'

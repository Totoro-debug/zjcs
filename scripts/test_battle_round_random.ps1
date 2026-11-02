$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-RandomRule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-RandomRule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-RoundRandom($condition,$message){if(-not $condition){throw $message}}
$skills=Read-RandomRule 'skill-rules';$battle=Read-RandomRule 'battle-rules';$rules=Read-RandomRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-RandomRule $skills),'854_156383',$battle,(Hash-RandomRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-RandomRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-RandomRule $rules),'854_156383')
foreach($id in 60520,25120,25134){
    $source=$catalog.CreateStatus($id,1,1)
    Assert-RoundRandom ($source.RoundStartRulesSupported -and $source.RoundStartRandomOneStatus) "Original $id random state remains unsupported"
}
function New-RandomCheckpoint($statusId,$seed=23){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.MaxRound=20;$cp.RandomSeed=$seed
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
    $cp.Units[0].Statuses.Add($catalog.CreateStatus($statusId,1,1,990000+$statusId,0,0,5,20,1,$null))
    return $cp
}
function Invoke-RandomBattle($statusId,$restore,$legacy){
    $cp=New-RandomCheckpoint $statusId
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $eligible=@($cp.Units[0].Statuses[0].RoundStartStatuses.EntityId)
    if($legacy){
        $json=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        $json=$json -replace '"RoundStartRandomRulesResolved":true,','' -replace '"RoundStartRandomOneStatus":true,',''
        $json=$json -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    $rounds=if($statusId -eq 60520){3}else{7}
    foreach($round in 1..$rounds){
        if($restore){
            $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
            $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        }
        $runner.Step(0,$cp.Counter+1)|Out-Null
        $owner=$cp.Units[0];$source=$owner.Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s)$s.ClassId -eq $statusId})
        $effects=@($owner.Statuses|Where-Object {$_.ClassId -in $eligible})
        $count=if($statusId -eq 60520){1}else{$round}
        Assert-RoundRandom ($effects.Count -eq 1 -and $source.RoundStartTriggeredCount -eq $count -and $owner.SkillState.Round -eq $round) "Original $statusId applied multiple entries or lost its count/cap in round $round"
        Assert-RoundRandom ($effects[0].CreatorId -eq 1 -and $effects[0].SourceSkillRank -eq 5 -and $effects[0].SourceSkillLevel -eq 20 -and @($effects[0].StatusProperties|Where-Object Value -ne 0).Count -gt 0) 'Random status lost original cultivation or its actual nonzero property effect'
        $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        Assert-RoundRandom ($runner.Step(0,$cp.Counter).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Receipt replay drew another status'
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
foreach($id in 60520,25120,25134){
    $expected=Invoke-RandomBattle $id $false $false
    Assert-RoundRandom ($expected -eq (Invoke-RandomBattle $id $true $false)) "Random sequence changed after per-turn restore for $id"
    Assert-RoundRandom ($expected -eq (Invoke-RandomBattle $id $true $true)) "Legacy random definition changed continued battle for $id"
}
# Positive seeds cover all configured choices without depending on a probabilistic
# frequency tolerance. Each draw must select exactly one real original entry.
foreach($id in 60520,25120,25134){
    $seen=[Collections.Generic.HashSet[int]]::new()
    foreach($seed in 1,30000,60000){
        $cp=New-RandomCheckpoint $id $seed;$owner=$cp.Units[0];$source=$owner.Statuses[0]
        $eligible=@($source.RoundStartStatuses.EntityId);$random=[Zjcs.Combat.Official.FightRandomStream]::new($seed)
        [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)|Out-Null
        $effects=@($owner.Statuses|Where-Object {$_.ClassId -in $eligible})
        Assert-RoundRandom ($effects.Count -eq 1) 'Random draw did not apply exactly one configured original state'
        $seen.Add($effects[0].ClassId)|Out-Null
    }
    Assert-RoundRandom ($seen.Count -eq $eligible.Count) "Original $id contains an unreachable configured choice"
}
$cp=New-RandomCheckpoint 60520 23;$owner=$cp.Units[0];$source=$owner.Statuses[0]
$source.RoundStartSkills.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{EntityId=18860;Source=2;Target=1;TargetPos=1;BasePercent=1})
$random=[Zjcs.Combat.Official.FightRandomStream]::new(23)
$casts=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)
Assert-RoundRandom ($casts.Count -eq 1 -and $casts[0].Trigger.EntityId -eq 18860 -and $owner.Statuses.Count -eq 2 -and $source.RoundStartTriggeredCount -eq 1) 'Random status flag changed the separate skill list'
$after=$random.Seed
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)|Out-Null
Assert-RoundRandom ($random.Seed -eq $after -and $source.RoundStartTriggeredCount -eq 1) 'A capped random source drew another entry'
# User-confirmed order: draw first, then roll the chosen entry; never redraw.
foreach($chance in 0.0,0.01){
    $cp=New-RandomCheckpoint 60520 1;$owner=$cp.Units[0];$source=$owner.Statuses[0]
    $source.RoundStartStatuses[0].BasePercent=$chance
    $random=[Zjcs.Combat.Official.FightRandomStream]::new(1);$oracle=[Zjcs.Combat.Official.FightRandomStream]::new(1)
    $oracle.Next()|Out-Null;if($chance -gt 0){$oracle.Next()|Out-Null}
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)|Out-Null
    Assert-RoundRandom ($source.RoundStartTriggeredCount -eq 0 -and $owner.Statuses.Count -eq 1 -and $random.Seed -eq $oracle.Seed) 'Failed selected probability redrew, consumed a trigger or evaluated unselected probabilities'
}
$cp=New-RandomCheckpoint 60520;$owner=$cp.Units[0];$source=$owner.Statuses[0]
$source.RoundStartRate=0;$random=[Zjcs.Combat.Official.FightRandomStream]::new(23)
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($owner,$owner,$cp.Units,$random,$catalog)|Out-Null
Assert-RoundRandom ($random.Seed -eq 23 -and $source.RoundStartTriggeredCount -eq 0) 'Failed component rate still drew a random state'
Write-Output 'Round random checks passed: originals 60520/25120/25134, one actual nonzero buff, uniform configured choices, selected-probability failure without redraw, finite cap, cultivation, receipts and legacy/per-turn deterministic continuation.'

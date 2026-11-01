. (Join-Path $PSScriptRoot 'test_battle_skill_replay.ps1')
$equipment = Join-Path $root 'unity/Assets/Game/Runtime/Offline/Equipment'
Add-Type -Path @((Join-Path $equipment 'OfficialEquipmentJson.cs'),
    (Join-Path $equipment 'OfficialEquipmentCatalog.cs'),
    (Join-Path $equipment 'OfficialEquipmentCatalog.TermReplacement.cs'))
$equipmentCatalog = [Zjcs.Offline.Equipment.OfficialEquipmentCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json'))

function Term-Replacements($id, $rank, $equipRank, $bless, [int[]] $indices) {
    $list = [System.Collections.Generic.List[int]]::new()
    foreach ($index in $indices) { $list.Add($index) }
    return $equipmentCatalog.CalculateTermReplacementProperties($id, $rank, $equipRank, $bless, $list)
}

$values = Term-Replacements 40015 4 4 0 @(0,0)
Assert-Replay ($values[5001].Key -eq 800 -and $values[5001].Value -eq 'Float') 'Original fixed-point probability or its type changed.'
Assert-Replay ($values[5002].Key -eq 1 -and $values[5002].Value -eq 'Long') 'Original integer replay cap or its type changed.'
$blessed = Term-Replacements 40000 4 4 1 @(0)
Assert-Replay ($blessed[5001].Key -eq 247) 'Original blessing multiplication must truncate 165 * 1.5 to 247.'
$otherRank = Term-Replacements 40000 5 4 1 @(0)
Assert-Replay ($otherRank[5001].Key -eq 165) 'Blessing scaled an equipment term from another rank.'
$missingScale = Term-Replacements 40000 4 4 9999 @(2)
Assert-Replay ($missingScale[5001].Key -eq 250) 'Missing blessing scale did not retain the original factor-one fallback.'
Assert-Replay ((Term-Replacements 40015 4 4 0 @(1,0,0)).Count -eq 1) 'Out-of-range values or extra group indices were not skipped.'
Assert-Replay ((Term-Replacements 999999 4 4 0 @(0)).Count -eq 0) 'An absent original term produced replacement properties.'

# Feed the real term's typed values into the original replay status; do not force its probability.
$replacementProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightStatusReplacementValue]]::new()
foreach ($entry in $values.GetEnumerator()) {
    $replacementProperties.Add([Zjcs.Combat.Official.FightStatusReplacementValue]@{
        PropertyId=$entry.Key; RawValue=$entry.Value.Key; IsFloat=($entry.Value.Value -eq 'Float')
    })
}
$skillItems = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/rules.json'), '854_156383')
$owner = New-Unit 1 1 0 0 1000 100
$owner.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$binding = [Zjcs.Combat.Official.FightOfficialPassiveSkillBinding]@{
    ItemId=17015; ReplacementProperties=$replacementProperties
}
$passiveSkills = [System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialPassiveSkillBinding]]::new()
$passiveSkills.Add($binding)
$bindings = $catalog.CreatePassiveStatusBindings($owner, $skillItems, $passiveSkills)
$catalog.AssembleUnit($owner, $null, $bindings)
Assert-Replay ($owner.Statuses.Count -eq 1 -and $owner.Statuses[0].ApplyingSkillItemId -eq 17015) 'Original equipment passive item did not assemble its status.'
$status = $owner.Statuses[0]
$status.InstanceId = 9901071
Assert-Replay ([math]::Abs($status.SkillReplay.Rate - 0.08) -lt 0.000001 -and $status.SkillReplay.MaxCountEachRound -eq 1) 'Equipment term parameters did not reach the original status replay configuration.'
$skill = New-ReplaySkill $false 0 0 1
$skill.HasReplay = $false
$result = Invoke-ReplayBattle $skill 0 @() @($status) {
    param($checkpoint, $executor)
    $checkpoint.MaxRound = 1000
    $script:equipmentReplayExecutor = $executor
}
for ($step = 2; $step -le 128 -and -not $result.Checkpoint.Finished; $step++) {
    $result = $equipmentReplayExecutor.Step(0, $step)
    if (@($result.Checkpoint.Receipts | ForEach-Object { $_.Event.SkillCasts } | Where-Object { $_.ReplayStatusInstanceId -eq 9901071 }).Count -gt 0) { break }
}
$replays = @($result.Checkpoint.Receipts | ForEach-Object { $_.Event.SkillCasts } | Where-Object { $_.ReplayStatusInstanceId -eq 9901071 })
Assert-Replay ($replays.Count -gt 0) 'The deterministic battle did not execute the real 8-percent equipment replay.'
Assert-Replay ($status.SkillReplay.CountEachRound -le 1) 'The real equipment replay exceeded its original per-round cap.'
$saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint)
Assert-Replay ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)) -eq $saved) 'Equipment replay parameters or progress were lost in the checkpoint.'
Write-Output ('Equipment replay checks passed: typed original values, indexed groups, blessing truncation/fallback, 8-percent status replay and checkpoint at step=' + $result.Checkpoint.Counter)

. (Join-Path $PSScriptRoot 'test_battle_skill_replay.ps1')

function New-StatusReplay($id, $rate, $cap, $increase = 0) {
    $status = [Zjcs.Combat.Official.FightBattleStatusState]@{
        InstanceId = $id; ClassId = $id; OwnerId = 1; CreatorId = 1
        StartRound = 0; DurationRound = -1; CurDurationRound = -1
    }
    $status.SkillReplay = [Zjcs.Combat.Official.FightBattleStatusReplayState]@{
        Rate = $rate; MaxCountEachRound = $cap; AddRateWhenNotHit = $increase
    }
    return $status
}
function New-StatusReplaySkill {
    $skill = New-ReplaySkill $false 0 0 1
    $skill.HasReplay = $false
    return $skill
}
function Replay-Casts($result) { @($result.Event.SkillCasts | Where-Object IsReplay) }

foreach ($id in @(60150, 24053, 24589, 24596, 25329)) {
    Assert-Replay ($null -ne $catalog.CreateStatus($id, 1, 1).SkillReplay) "Original status replay $id was omitted."
}
$original = $catalog.CreateStatus(25329, 1, 1).SkillReplay
Assert-Replay ([math]::Abs($original.Rate - 0.4) -lt 0.00001 -and [math]::Abs($original.AddRateWhenNotHit - 0.2) -lt 0.00001) 'Original failure-rate increment was lost.'
$replacement = [System.Collections.Generic.List[Zjcs.Combat.Official.FightStatusReplacementValue]]::new()
$replacement.Add([Zjcs.Combat.Official.FightStatusReplacementValue]@{PropertyId=5001; RawValue=6000; IsFloat=$true})
$replacement.Add([Zjcs.Combat.Official.FightStatusReplacementValue]@{PropertyId=5002; RawValue=2; IsFloat=$false})
$replaced = $catalog.CreateStatus(60150, 1, 1, 11, 0, 0, 1, 1, 0, $null, 0, $replacement).SkillReplay
Assert-Replay ([math]::Abs($replaced.Rate - 0.6) -lt 0.00001 -and $replaced.MaxCountEachRound -eq 2) 'Typed original status replacement properties were lost.'

# Two simultaneous successes must be queued before either continuation is run.
$first = New-StatusReplay 9901001 1 2
$second = New-StatusReplay 9901002 1 1
$result = Invoke-ReplayBattle (New-StatusReplaySkill) 0 @() @($first, $second)
$casts = Replay-Casts $result
Assert-Replay (($casts.ReplayStatusInstanceId -join ',') -eq '9901001,9901002,9901001') 'Status replay did not retain FIFO continuation order.'
Assert-Replay ($result.Checkpoint.Units[0].SkillState.Slots[0].ReleasedTimes -eq 4) 'Replay releases did not enter the original release counter.'
Assert-Replay ($first.SkillReplay.CountEachRound -eq 2 -and $second.SkillReplay.CountEachRound -eq 1) 'Independent status caps were merged.'

$skill = New-StatusReplaySkill
$skill.ReplayMaxCountEachRound = 1
$result = Invoke-ReplayBattle $skill 0 @() @((New-StatusReplay 9901011 1 2), (New-StatusReplay 9901012 1 2))
Assert-Replay ((Replay-Casts $result).Count -eq 1) 'Independent statuses bypassed the skill round cap.'

$result = Invoke-ReplayBattle (New-StatusReplaySkill) 0 @() @((New-StatusReplay 9901021 1 5)) {
    param($cp, $executor) $cp.Units[0].SkillState.Slots[0].LimitedTimes = 2
}
Assert-Replay ((Replay-Casts $result).Count -eq 1 -and $result.Checkpoint.Units[0].SkillState.Slots[0].ReleasedTimes -eq 2) 'Status replay bypassed LimitedTimes.'
$unlimited = New-StatusReplay 9901022 1 2
$unlimited.SkillReplay.IsCheckSkillLimitedTimes = $false
$result = Invoke-ReplayBattle (New-StatusReplaySkill) 0 @() @($unlimited) {
    param($cp, $executor) $cp.Units[0].SkillState.Slots[0].LimitedTimes = 1
}
Assert-Replay ((Replay-Casts $result).Count -eq 2) 'Explicitly unchecked status replay was blocked by LimitedTimes.'

# Both the normal cast and direct replays enter the same original slot release counter.
$result = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 30 @() @() {
    param($cp, $executor) $cp.Units[0].SkillState.Slots[0].LimitedTimes = 2
}
Assert-Replay ((Replay-Casts $result).Count -eq 1 -and $result.Checkpoint.Units[0].SkillState.Slots[0].ReleasedTimes -eq 2) 'Direct replay did not account for the initial release before checking LimitedTimes.'

# A pre-migration checkpoint has no replay state; its original configuration is hydrated.
$legacy = $catalog.CreateStatus(24596, 1, 1)
$legacy.SkillReplay = $null
$result = Invoke-ReplayBattle (New-StatusReplaySkill) 0 @() @($legacy)
Assert-Replay ($null -ne $legacy.SkillReplay -and [math]::Abs($legacy.SkillReplay.Rate - 0.6) -lt 0.00001 -and $legacy.SkillReplay.MaxCountEachRound -eq 1) 'An old checkpoint did not recover its original status replay definition.'

# Gates run before probability, failure increments, trigger counts, or RNG consumption.
$control = Invoke-ReplayBattle (New-StatusReplaySkill) 0
foreach ($gate in @('policy', 'action', 'phase', 'tags', 'cap')) {
    $skill = New-StatusReplaySkill
    $status = New-StatusReplay 9901031 0.5 3 0.2
    switch ($gate) {
        policy { $skill.CanTriggerChild = $false }
        action { $status.SkillReplay.FilterTypes.Add($skill.ActionType) }
        phase { $status.SkillReplay.TriggerInGameStatusList.Add(1) }
        tags {
            $status.SkillReplay.TagQuery = $catalog.CreateStatus(24053, 1, 1).SkillReplay.TagQuery
            $skill.GameplayTags = $catalog.SkillGameplayTags(12110)
        }
        cap { $status.SkillReplay.MaxCountEachRound = 0 }
    }
    $result = Invoke-ReplayBattle $skill 0 @() @($status)
    Assert-Replay ((Replay-Casts $result).Count -eq 0 -and $status.SkillReplay.ExtraRate -eq 0 -and $status.SkillReplay.CountEachRound -eq 0) "Blocked $gate gate changed status state."
    Assert-Replay ($result.Checkpoint.RandomSeed -eq $control.Checkpoint.RandomSeed) "Blocked $gate gate consumed RNG."
}
$skill = New-StatusReplaySkill
$skill.CanTriggerChild = $false
$skill.CanTriggerStatusEntityIds.Add(9901041)
$result = Invoke-ReplayBattle $skill 0 @() @((New-StatusReplay 9901041 1 1))
Assert-Replay ((Replay-Casts $result).Count -eq 1) 'The status whitelist did not permit replay.'

# Failure progress and an independent finite cap survive a live executor checkpoint.
$failure = New-StatusReplay 9901051 0 1 0.25
$result = Invoke-ReplayBattle (New-StatusReplaySkill) 0 @() @($failure) {
    param($cp, $executor) $script:liveStatusReplayExecutor = $executor
}
Assert-Replay ($failure.SkillReplay.ExtraRate -eq 0.25 -and $failure.SkillReplay.CountEachRound -eq 0) 'Failed replay did not retain its extra probability.'
$saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint)
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
Assert-Replay ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored) -eq $saved) 'Status replay checkpoint was not stable.'
$restoredFailure = $restored.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($value) $value.InstanceId -eq 9901051})
Assert-Replay ($restoredFailure.SkillReplay.ExtraRate -eq 0.25) 'Checkpoint lost accumulated failure probability.'

$formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$restoredExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
for ($step = 2; $step -le 5; $step++) {
    $continued = $liveStatusReplayExecutor.Step(0, $step)
    $resumed = $restoredExecutor.Step(0, $step)
    Assert-Replay ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($continued.Checkpoint) -eq
        [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($resumed.Checkpoint)) 'Resuming changed random replay continuation, counters or receipts.'
}


$first.SkillReplay.CountRound = -1
$first.SkillReplay.CountEachRound = 77
$first.SkillReplay.ExtraRate = 0.75
$result = Invoke-ReplayBattle (New-StatusReplaySkill) 0 @() @($first)
Assert-Replay ($first.SkillReplay.CountEachRound -eq 2 -and $first.SkillReplay.ExtraRate -eq 0) 'A new personal round did not reset the count or success did not reset extra rate.'

# Original lifecycle copies must not share mutable replay filters or progress.
$template = $catalog.CreateStatus(24053, 1, 1)
$unit = New-Unit 1 1 0 0 100 100
$applied = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($unit, $template, 0, 9901061)
$applied.SkillReplay.TagQuery[0].Tags[0] = 0
$applied.SkillReplay.ExtraRate = 0.5
Assert-Replay ($template.SkillReplay.TagQuery[0].Tags[0] -eq 536870912 -and $template.SkillReplay.ExtraRate -eq 0) 'Applied status replay aliases the template.'
Write-Output 'Battle status replay checks passed: original contracts, typed replacements, independent FIFO chains, gates, skill limits, failure probability, lifecycle and checkpoints.'

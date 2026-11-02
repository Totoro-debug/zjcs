. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path (@($sources) + (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimationTimeline.cs'))

function Assert-Replay($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
function Get-Sha($path) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $path))
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')

$official = $catalog.CreateSkill(13220, 0, 0, 1, 1, 1, $null)
Assert-Replay ($official.HasReplay -and $official.ReplayStatusId -eq 24031) 'Official replay status mapping mismatch'
Assert-Replay ($official.ReplayStackCount -eq 10 -and $official.ReplayRelation -eq 1) 'Official replay condition mismatch'
Assert-Replay ($official.ReplayConsumeStack -and $official.ReplayConsumeCount -eq 10) 'Official replay consume mapping mismatch'

function New-Unit($id, $side, $x, $y, $hp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = $speed
    return $unit
}

function New-ReplaySkill($consume, $needed, $stacks, $relation) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = 930; $skill.ItemId = 931; $skill.TargetType = 1
    $skill.ActionDuration = 1.0; $skill.Duration = 1.2
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 932; $hit.DamageId = 933; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 5; $hit.TargetMode = 0
    $skill.Hits.Add($hit)
    $skill.HasReplay = $true; $skill.ReplayStatusId = 24031; $skill.ReplayStackCount = $needed
    $skill.ReplayRelation = $relation; $skill.ReplayConsumeStack = $consume
    $skill.ReplayConsumeCount = $stacks
    return $skill
}

function Invoke-ReplayBattle($skill, $stacks, $moreSkills = @(), $replayStatuses = @(), $configure = $null) {
    $player = New-Unit 1 1 0 0 1000 100
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    foreach ($extra in $moreSkills) { $player.Skills.Add($extra) }
    $status = $catalog.CreateStatus(24031, 1, 1)
    $status.StackCount = $stacks
    $player.Statuses.Add($status)
    foreach ($replayStatus in $replayStatuses) { $player.Statuses.Add($replayStatus) }
    $enemy = New-Unit 2 2 1 0 5000 1
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    if ($configure -ne $null) { & $configure $cp $executor }
    return $executor.Step(0, 1)
}

# Enough stacks: the skill replays once and consumes the configured stacks.
$replayed = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 10
$casts = @($replayed.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 -and $_.Slot -eq 0 })
Assert-Replay ($casts.Count -eq 2) "Replay cast count is $($casts.Count) instead of 2"
Assert-Replay (($replayed.Checkpoint.Units[0].Statuses | Where-Object { $_.ClassId -eq 24031 }).Count -eq 0) 'Replay did not consume the status stacks'

# Real cumulative-consumption passive must observe replay costs, without consuming charges
# independently. Disable its already-exercised round-start grant in this isolated fixture.
$consumeListener = $catalog.CreateStatus(25278, 1, 1, 991, 25278, 10662, 6, 17, 10, $null)
$consumeListener.HasRoundStartTrigger = $false
$consumptionResult = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 10 @() @($consumeListener)
$consumptionBuff = $consumptionResult.Checkpoint.Units[0].Statuses | Where-Object ClassId -eq 24187 | Select-Object -First 1
Assert-Replay ($null -ne $consumptionBuff -and $consumptionBuff.StackCount -eq 5 -and
    $consumeListener.ConsumeStackCountProgress -eq 0) 'Replay costs did not notify the cumulative-consumption passive exactly once'

# Not enough stacks: no replay.
$notEnough = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 5
Assert-Replay (@($notEnough.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 1) 'Replay fired without enough stacks'

# Each completed replay rechecks the remaining stacks.
$continuous = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 20
Assert-Replay (@($continuous.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 3) 'Twenty stacks did not produce two replays'
Assert-Replay ($continuous.Checkpoint.Units[0].SkillState.ReplayCounts[0].Count -eq 2) 'Replay counter omitted a replay'
Assert-Replay ((@($continuous.Event.SkillCasts | Where-Object IsReplay).Delay -join ',') -eq '1,2') 'Continuous replay casts did not wait for preceding action ends'
Assert-Replay (($continuous.Event.DamageEvents.Delay -join ',') -eq '0,1,2') 'Replay damage did not inherit its cast start time'
Assert-Replay ([Math]::Abs($continuous.Event.Duration - 3.2) -lt 0.00001 -and
    [Math]::Abs($continuous.Event.ActionDuration - 3.0) -lt 0.00001) 'Replay action and lifetime totals were not retained separately'

# AllEnded tails belong to each individual cast, without another tail on the whole queue.
$tailSkill = New-ReplaySkill $true 10 10 1
$tailSkill.ActionDuration = -1; $tailSkill.Duration = -1
$tailSkill.AllEndedActionDuration = 0.4; $tailSkill.AllEndedDuration = 0.9
$tailSkill.Hits[0].Delay = 0.2
$tailResult = Invoke-ReplayBattle $tailSkill 20
$tailCasts = @($tailResult.Event.SkillCasts | Where-Object IsReplay)
Assert-Replay ([Math]::Abs($tailCasts[0].Delay - 0.6) -lt 0.00001 -and
    [Math]::Abs($tailCasts[1].Delay - 1.2) -lt 0.00001) 'Relative action-end replay delays changed'
Assert-Replay ([Math]::Abs($tailResult.Event.Duration - 2.3) -lt 0.00001 -and
    [Math]::Abs($tailResult.Event.ActionDuration - 1.8) -lt 0.00001) 'Lifetime tail was duplicated after replay'

# A configured per-round cap stops an otherwise persistent condition.
$cappedSkill = New-ReplaySkill $false 1 1 1
$cappedSkill.ReplayMaxCountEachRound = 3
$capped = Invoke-ReplayBattle $cappedSkill 20
Assert-Replay (@($capped.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 4) 'Configured replay cap was ignored'
Assert-Replay ($capped.Checkpoint.Units[0].SkillState.ReplayCounts[0].Count -eq 3) 'Configured cap counter mismatch'

$disabledSkill = New-ReplaySkill $true 10 10 1
$disabledSkill.ReplayMaxCountEachRound = 0
$disabled = Invoke-ReplayBattle $disabledSkill 20
Assert-Replay (@($disabled.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 1) 'Zero replay cap did not disable replay'
Assert-Replay (($disabled.Checkpoint.Units[0].Statuses | Where-Object { $_.ClassId -eq 24031 }).StackCount -eq 20) 'Blocked replay consumed stacks'

# A long finite chain uses an iterative continuation, with no hidden one-level cap.
$long = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 2000
Assert-Replay (@($long.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 201) 'Long finite replay chain was truncated'
Assert-Replay ($official.ReplayMaxCountEachRound -eq 1) 'Original replay maximum was not mapped'
$roundtrip = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($capped.Checkpoint))
Assert-Replay ($roundtrip.Units[0].SkillState.ReplayCounts[0].Count -eq 3) 'Replay count was lost during checkpoint restoration'
Assert-Replay ($roundtrip.Units[0].Skills[0].ReplayMaxCountEachRound -eq 3) 'Replay cap was lost during checkpoint restoration'
$rateSkill = New-ReplaySkill $false 1 1 1
$rateSkill.ReplayUsesRate = $true; $rateSkill.ReplayChance = 1.0; $rateSkill.ReplayMaxCountEachRound = 2
$rate = Invoke-ReplayBattle $rateSkill 0
Assert-Replay (@($rate.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 3) 'Certain probability replay did not reach configured cap'
$rateSkill.ReplayChance = 0.0
$zeroRate = Invoke-ReplayBattle $rateSkill 0
Assert-Replay (@($zeroRate.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 1) 'Zero probability replay fired'
$officialRate = $catalog.CreateSkill(64120, 0, 0, 1, 1, 0, $null)
Assert-Replay ($officialRate.ReplayUsesRate -and $officialRate.ReplayChance -eq 0.6 -and $officialRate.ReplayType -eq 2) 'Original probability/type mapping mismatch'
Assert-Replay ($official.ReplayReplacementEntities.Count -eq 1 -and $official.ReplayReplacementEntities[0] -eq 13223) 'Original ordered replacement mapping mismatch'
$slotTrigger = New-ReplaySkill $false 1 1 1
$slotTrigger.ReplayUsesRate = $true; $slotTrigger.ReplayChance = 1.0
$slotTrigger.ReplaySelectMatchingSlot = $true; $slotTrigger.ReplaySlotElementTypes.Add(101)
$slotTarget = New-ReplaySkill $false 1 1 1
$slotTarget.EntityId = 940; $slotTarget.Slot = 1; $slotTarget.ElementType = 101; $slotTarget.HasReplay = $false
$slotResult = Invoke-ReplayBattle $slotTrigger 0 @($slotTarget)
$selectedCasts = @($slotResult.Event.SkillCasts | Where-Object { $_.IsReplay })
Assert-Replay ($selectedCasts.Count -eq 1 -and $selectedCasts[0].SkillEntityId -eq 940 -and $selectedCasts[0].Slot -eq 1) 'Conditional provider replayed itself instead of the matching slot'
$slotTarget.ElementType = 103
$noSlot = Invoke-ReplayBattle $slotTrigger 0 @($slotTarget)
Assert-Replay (@($noSlot.Event.SkillCasts | Where-Object { $_.IsReplay }).Count -eq 0) 'Conditional provider selected an ineligible element'
$originalProvider = $catalog.CreateSkill(62990, 0, 0, 1, 1, 0, $null)
Assert-Replay ($originalProvider.ReplaySelectMatchingSlot -and ($originalProvider.ReplaySlotElementTypes -join ',') -eq '101,102') 'Original conditional provider mapping mismatch'
$replaceSkill = New-ReplaySkill $true 10 10 1
$replaceSkill.ReplayReplacementEntities.Add(13223)
$replacement = Invoke-ReplayBattle $replaceSkill 20
$replacementCasts = @($replacement.Event.SkillCasts | Where-Object { $_.IsReplay })
Assert-Replay ($replacementCasts.Count -eq 1 -and $replacementCasts[0].SkillEntityId -eq 13223) 'Replay replacement did not execute the actual replacement entity'
Assert-Replay (($replacement.Checkpoint.Units[0].Statuses | Where-Object { $_.ClassId -eq 24031 }).StackCount -eq 10) 'Replacement with zero continuation consumed a second stack payment'
# Force only the five original variants' probability in memory.
$oldCatalog = $catalog
$needle = '"Replay":{"Rate":0.0,"ReplaceSkillArrayOnReplay":[13104,13105,13106,13107,13108]}'
$replacementText = $needle.Replace('"Rate":0.0', '"Rate":1.0')
$skillJson = (Read-Text 'unity/Assets/Official/Combat/skill-rules.json').Replace($needle, $replacementText)
$battleJson = (Read-Text 'unity/Assets/Official/Combat/battle-rules.json').Replace($needle, $replacementText)
function Hash-Text($text) {
    return ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($text))) -replace '-', '').ToLowerInvariant()
}
try {
    $catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skillJson, (Hash-Text $skillJson), '854_156383', $battleJson, (Hash-Text $battleJson), '854_156383')
    $sequenceSkill = $catalog.CreateSkill(13104, 0, 0, 1, 1, 0, $null)
    $sequence = Invoke-ReplayBattle $sequenceSkill 0
    $ids = @($sequence.Event.SkillCasts | Where-Object { $_.IsReplay } | ForEach-Object { $_.SkillEntityId })
    Assert-Replay (($ids -join ',') -eq '13104,13105,13106,13107,13108') "Ordered replacement sequence mismatch: $ids"
    Assert-Replay ($sequence.Checkpoint.Units[0].SkillState.ReplayCounts[0].Count -eq 5) 'Variants did not share their original slot counter'
    $start = [double]$sequenceSkill.ActionDuration
    foreach ($cast in @($sequence.Event.SkillCasts | Where-Object IsReplay)) {
        $variant = $catalog.CreateSkill($cast.SkillEntityId, 0, 0, 1, 1, 0, $null)
        Assert-Replay ([Math]::Abs($cast.Delay - $start) -lt 0.00001) "Variant $($cast.SkillEntityId) did not wait for the preceding variant's action"
        $hits = @($sequence.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq $cast.SkillEntityId })
        if ($cast.SkillEntityId -ne 13104 -and $hits.Count -gt 0) {
            Assert-Replay (($hits | Where-Object Delay -lt $start).Count -eq 0) "Variant $($cast.SkillEntityId) start=$start hits=$($hits.Delay -join ',') did not retain cast timing"
        }
        $start += $variant.ActionDuration
    }
} finally { $catalog = $oldCatalog }

# A real move and its real action-start listener must remain local to each replay
# until the entire cast is placed on the FIFO timeline.
$movingReplay = New-ReplaySkill $true 10 10 1
$movingReplay.EntityId = 13500
$move = $catalog.CreateSkill(13500, -1, 0, 1, 1, 1, $null).SourceMoves[0]
$move.Conditions[0].Distance = 99 # Only the fixture keeps all three moves reachable.
$movingReplay.SourceMoves.Add($move)
$moving = Invoke-ReplayBattle $movingReplay 20 @() @() {
    param($cp, $executor)
    $cp.Units[0].X = 4; $cp.Units[1].X = 5
    $listener = $catalog.CreateStatus(24346, 1, 2, 9900996)
    $listener.StackCount = 1
    $cp.Units[0].Statuses.Add($listener)
}
Assert-Replay ($moving.Event.Movements.Count -eq 3) 'The moving replay fixture did not execute all three original moves'
$listenerCasts = @($moving.Event.SkillCasts | Where-Object SkillEntityId -eq 58980)
$listenerHits = @($moving.Event.DamageEvents | Where-Object SkillEntityId -eq 58980)
Assert-Replay ($listenerCasts.Count -eq 3 -and $listenerHits.Count -eq 3) 'Original nested movement listeners were lost'
for ($i = 0; $i -lt 3; $i++) {
    $expected = 0.8 + $i
    Assert-Replay ([Math]::Abs($moving.Event.Movements[$i].Delay - $expected) -lt 0.00001 -and
        [Math]::Abs($listenerCasts[$i].Delay - $expected) -lt 0.00001 -and
        [Math]::Abs($listenerHits[$i].Delay - $expected) -lt 0.00001) 'Nested movement, cast or damage was offset more or less than once'
}
Assert-Replay ([Math]::Abs($moving.Event.Duration - 3.3) -lt 0.00001) 'The last replay lost its nested movement-listener lifetime'
$playables = Get-Content (Join-Path $root 'unity/Assets/Game/Resources/Combat/skill-playables.json') -Raw | ConvertFrom-Json
$data = [Zjcs.Presentation.Combat.OfficialBattleSkillPlayableCatalogData]::new()
foreach ($record in $playables.skills | Where-Object skill_entity_id -eq 13500) {
    $entry = [Zjcs.Presentation.Combat.OfficialBattleSkillPlayableRecord]::new()
    $entry.skill_entity_id = $record.skill_entity_id
    foreach ($cue in $record.playables) {
        $entry.playables.Add([Zjcs.Presentation.Combat.OfficialBattleSkillPlayableCue]@{
            kind = $cue.kind; time = $cue.time; animation = $cue.animation
        })
    }
    $data.skills.Add($entry)
}
$cues = @([Zjcs.Presentation.Combat.OfficialBattleAnimationTimeline]::Build($moving.Event, $data) | Where-Object { $null -ne $_.SkillPlayable })
Assert-Replay (($cues.Delay -join ',') -eq '0,1,2') 'Original 13500 Playables did not follow replay cast starts'
$savedTiming = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($moving.Checkpoint)
Assert-Replay ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($savedTiming)) -eq $savedTiming) 'Replay event timing was lost during checkpoint restoration'
Write-Output 'Battle skill replay checks passed: continuous stacks/probability, configured caps, conditional slots, replacement entity and checkpoint counters.'

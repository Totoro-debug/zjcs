. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimationTimeline.cs'))
function Assert-Timing($condition, $message) { if (-not $condition) { throw $message } }
function Text($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skillJson = Text 'skill-rules'; $battleJson = Text 'battle-rules'; $rules = Text 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash $skillJson), '854_156383', $battleJson, (Hash $battleJson), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash $rules), '854_156383')
function Battle($immediate = $false, $nested = $false, $restore = $false, $listenerId = 1) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 8; DefaultGroundType = 2 }
    foreach ($id in 1, 2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $u.Side = $id; $u.X = 6 - $id; $u.Y = 2; $u.CharacterType = 1
        $u.Hp = $u.MaxHp = 100000; $u.Level = $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 1000 })
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        $cp.Units.Add($u)
    }
    $status = $catalog.CreateStatus(12353, $listenerId, $listenerId, 10)
    $status.MoveNearSkillTriggerImmediately = $immediate
    Assert-Timing ($status.MoveNearSkillTriggerDelayTime -eq 1.0) 'Original trap delay changed'
    if ($nested) {
        # Keep the real range trigger, but substitute a real moving skill to exercise
        # nested movement-start listeners within the delayed cast.
        $status.MoveNearSkills[0].EntityId = 13500
        $listener = $catalog.CreateStatus(24346, (3 - $listenerId), $listenerId, 11)
        $listener.StackCount = 1
        $cp.Units[2 - $listenerId].Statuses.Add($listener)
    }
    $cp.Units[$listenerId - 1].Statuses.Add($status)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    }
    $result = $runner.Step(0, 1)
    $entity = $(if ($nested) { 13500 } else { 12570 })
    $casts = @($result.Event.SkillCasts | Where-Object SkillEntityId -eq $entity)
    $hits = @($result.Event.DamageEvents | Where-Object SkillEntityId -eq $entity)
    Assert-Timing ($casts.Count -eq 1 -and ($nested -or $hits.Count -gt 0)) "Delayed skill $entity did not execute"
    $offset = $(if ($immediate) { 0.0 } else { 1.0 })
    Assert-Timing ($casts[0].Delay -eq $offset) 'Configured delayed cast lost its start time'
    if (-not $nested) {
        Assert-Timing ([Math]::Abs($hits[0].Delay - ($offset + 0.2)) -lt 0.00001) 'The trap hit lost its 0.2s delay relative to the delayed skill'
        Assert-Timing ([Math]::Abs($result.Event.Duration - ($offset + 0.7)) -lt 0.00001) 'The delayed skill lifetime lost or counted its trigger delay twice'
    }
    return $result
}
$delayed = Battle
$immediate = Battle $true
Assert-Timing ($delayed.Checkpoint.Units[0].Hp -eq $immediate.Checkpoint.Units[0].Hp) 'Timing changed the trap damage amount'
$otherOwner = Battle $false $false $false 2
Assert-Timing ($otherOwner.Event.ActorId -eq 1 -and
    $otherOwner.Event.SkillCasts[0].ActorId -eq 1 -and
    $otherOwner.Event.SkillCasts[0].TargetIds.Contains(2) -and
    $otherOwner.Checkpoint.Units[1].Statuses[0].MoveNearTriggeredCount -eq 1) 'Status startup waited for its owner to take a personal turn'
$inactive = $otherOwner.Checkpoint.Units[1]
$inactiveStatus = $inactive.Statuses[0]
$inactiveStatus.MoveNearStartTriggered = $false; $inactiveStatus.MoveNearTriggeredCount = 0
$inactive.Ended = $true
$pending = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectMoveNearSkills(
    $otherOwner.Checkpoint.Units[0], $otherOwner.Checkpoint.Units,
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::MoveNearEventStart, $null,
    [Zjcs.Combat.Official.FightRandomStream]::new(23))
Assert-Timing ($pending.Count -eq 0 -and -not $inactiveStatus.MoveNearStartTriggered) 'An inactive wave consumed its status startup'
$inactive.Ended = $false
$pending = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectMoveNearSkills(
    $otherOwner.Checkpoint.Units[0], $otherOwner.Checkpoint.Units,
    [Zjcs.Combat.Official.FightStatusTriggerRuntime]::MoveNearEventStart, $null,
    [Zjcs.Combat.Official.FightRandomStream]::new(23))
Assert-Timing ($pending.Count -eq 1 -and $inactiveStatus.MoveNearStartTriggered) 'An activated owner did not retain its pending startup'
$nested = Battle $false $true
$nestedImmediate = Battle $true $true
Assert-Timing ($nested.Event.Movements.Count -gt 0 -and
    $nested.Event.Movements.Count -eq $nestedImmediate.Event.Movements.Count) 'Delayed nested movement was lost'
for ($i = 0; $i -lt $nested.Event.Movements.Count; $i++) {
    Assert-Timing ([Math]::Abs($nested.Event.Movements[$i].Delay - $nestedImmediate.Event.Movements[$i].Delay - 1.0) -lt 0.00001) 'Nested movement did not inherit exactly one trigger delay'
}
$nestedCasts = @($nested.Event.SkillCasts | Where-Object SkillEntityId -eq 58980)
$immediateCasts = @($nestedImmediate.Event.SkillCasts | Where-Object SkillEntityId -eq 58980)
Assert-Timing ($nestedCasts.Count -gt 0 -and $nestedCasts.Count -eq $immediateCasts.Count) 'Nested movement listener did not execute'
for ($i = 0; $i -lt $nestedCasts.Count; $i++) {
    Assert-Timing ([Math]::Abs($nestedCasts[$i].Delay - $immediateCasts[$i].Delay - 1.0) -lt 0.00001) 'Nested cast did not inherit exactly one trigger delay'
}
$nestedHits = @($nested.Event.DamageEvents | Where-Object SkillEntityId -eq 58980)
$immediateHits = @($nestedImmediate.Event.DamageEvents | Where-Object SkillEntityId -eq 58980)
Assert-Timing ($nestedHits.Count -gt 0 -and $nestedHits.Count -eq $immediateHits.Count) 'Nested movement listener damage was lost'
for ($i = 0; $i -lt $nestedHits.Count; $i++) {
    Assert-Timing ([Math]::Abs($nestedHits[$i].Delay - $immediateHits[$i].Delay - 1.0) -lt 0.00001) 'Nested damage did not inherit exactly one trigger delay'
}
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
$cues = @([Zjcs.Presentation.Combat.OfficialBattleAnimationTimeline]::Build($nested.Event, $data) | Where-Object { $null -ne $_.SkillPlayable })
Assert-Timing ($cues.Count -gt 0 -and $cues[0].Delay -eq 1.0) 'Original 13500 animation started before its delayed cast'
$restored = Battle $false $true $true
Assert-Timing ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($nested.Checkpoint)) 'Trigger timing changed after checkpoint restoration'
$legacy = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($nested.Checkpoint) | ConvertFrom-Json -AsHashtable
foreach ($receipt in $legacy.checkpoint.Receipts) {
    foreach ($cast in $receipt.Event.SkillCasts) { $cast.Remove('Delay') }
}
$loadedLegacy = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(($legacy | ConvertTo-Json -Depth 100 -Compress))
Assert-Timing (@($loadedLegacy.Receipts[0].Event.SkillCasts | Where-Object Delay -ne 0).Count -eq 0) 'A legacy receipt without cast start times was rejected or assigned a nonzero delay'
Assert-Timing ($loadedLegacy.Receipts[0].Event.DamageEvents[0].Delay -eq $nested.Event.DamageEvents[0].Delay) 'Legacy compatibility changed an existing damage delay'
Write-Output 'Trigger timing passed: real 12353 -> 12570 hit at 1.2s; immediate switch; nested movement, listener casts and original Playable; deterministic restoration and legacy receipts.'

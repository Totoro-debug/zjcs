. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Assert-Start($condition, $message) { if (-not $condition) { throw $message } }
function Text($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skillJson = Text 'skill-rules'; $battleJson = Text 'battle-rules'; $rules = Text 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash $skillJson), '854_156383', $battleJson, (Hash $battleJson), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash $rules), '854_156383')
function Battle($restore = $false, $replay = $false, $producer = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 8; DefaultGroundType = 2 }
    foreach ($id in 1, 2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $u.Side = $id; $u.X = $id + 2; $u.Y = 2; $u.CharacterType = 1
        $u.Hp = $u.MaxHp = 100000; $u.Level = $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 1000 })
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        $cp.Units.Add($u)
    }
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.EntityId = 990001; $skill.ItemId = 990001; $skill.Slot = 0; $skill.TargetType = 1
    $skill.ActionDuration = 1; $skill.Duration = 1.2
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 990002; $hit.DamageId = 990002; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 5; $hit.Delay = 0.4
    $hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{
        StatusId = 12353; TargetType = 2; AffectedByProp = $false
    })
    $skill.Hits.Add($hit)
    if ($replay) {
        $skill.HasReplay = $true; $skill.ReplayMaxCountEachRound = 1; $skill.ReplayUsesRate = $true
        # A replay reapplies the same entity/skill origin: renew its existing trap
        # without manufacturing another status-start event.
    }
    $cp.Units[0].Skills.Add($skill)
    $cp.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0
    $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    }
    if ($producer) {
        $source = [Zjcs.Combat.Official.FightBattleStatusState]::new()
        $source.ClassId = $source.InstanceId = 990003; $source.OwnerId = 2; $source.CreatorId = 1
        $source.DurationRound = $source.CurDurationRound = -1; $source.StartRound = 0
        $source.HasActionExist = $true; $source.ActionExistRulesSupported = $true
        $source.ActionExistStatusId = 12353
        $cp.Units[1].Statuses.Add($source)
    }
    $result = $runner.Step(0, 1)
    $casts = @($result.Event.SkillCasts | Where-Object SkillEntityId -eq 12570)
    $hits = @($result.Event.DamageEvents | Where-Object SkillEntityId -eq 12570)
    Assert-Start ($casts.Count -gt 0 -and $hits.Count -eq $casts.Count) 'A trap created by the hit did not execute in the same step'
    Assert-Start (@($casts | Where-Object { [Math]::Abs($_.Delay - 1.4) -lt 0.00001 }).Count -eq 1) 'Startup did not inherit the parent hit plus trigger delay'
    Assert-Start (@($hits | Where-Object { [Math]::Abs($_.Delay - 1.6) -lt 0.00001 }).Count -eq 1) 'Startup hit time was lost or counted twice'
    Assert-Start ($result.Event.Duration -ge 2.0999) 'Startup lifetime was omitted from the step'
    if ($producer) {
        Assert-Start ($casts.Count -eq 2 -and $source.ActionExistAppliedStatusInstanceId -gt 0) 'Nested startup duplicated or lost the action-existence producer instance'
        $cp.Units[1].Statuses.Remove($source) | Out-Null
    }
    $callback = $cp.Units[0].GetType().GetField('StatusStarted', [Reflection.BindingFlags]'Instance,NonPublic')
    foreach ($unit in $cp.Units) { Assert-Start ($null -eq $callback.GetValue($unit)) 'Executor callback escaped the step' }
    $saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    Assert-Start (-not $saved.Contains('StatusStarted')) 'Runtime callback entered the save format'
    $cp.Units[0].Skills.Clear()
    [void]$runner.Step(0, 2)
    Assert-Start (@($cp.Receipts[1].Event.SkillCasts | Where-Object SkillEntityId -eq 12570).Count -eq 0) 'New status startup ran again on the next turn'
    try { [void]$runner.Step(99, 3); throw 'An invalid sequence was accepted' }
    catch { if ($_.Exception.Message -eq 'An invalid sequence was accepted') { throw } }
    foreach ($unit in $cp.Units) { Assert-Start ($null -eq $callback.GetValue($unit)) 'A failed step retained its callback' }
    return $result
}
$normal = Battle
$restored = Battle $true
Assert-Start ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($normal.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored.Checkpoint)) 'Mid-hit startup changed after restoration'
$replayed = Battle $false $true
$replayCasts = @($replayed.Event.SkillCasts | Where-Object SkillEntityId -eq 12570)
Assert-Start ($replayCasts.Count -eq 1 -and [Math]::Abs($replayCasts[0].Delay - 1.4) -lt 0.00001 `
    -and @($replayed.Checkpoint.Units[1].Statuses | Where-Object ClassId -eq 12353).Count -eq 1) `
    'Same-origin replay renewed the trap by creating a duplicate entity/start event'
[void](Battle $false $false $true)

# The start event belongs to the new entity, not each application or added stack.
$owner = [Zjcs.Combat.Official.FightBattleUnitState]::new(); $owner.Id = 1; $owner.Hp = $owner.MaxHp = 100
$template = $catalog.CreateStatus(12353, 1, 1, 1, 990001)
$script:startCount = 0
$callback = $owner.GetType().GetField('StatusStarted', [Reflection.BindingFlags]'Instance,NonPublic')
$callback.SetValue($owner, [Action[Zjcs.Combat.Official.FightBattleUnitState,Zjcs.Combat.Official.FightBattleStatusState]]{ param($u,$s) $script:startCount++ })
$first = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $template, 0, 1)
$first.ActionBehOfSameStatus = 2
[void][Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $template, 0, 2)
Assert-Start ($script:startCount -eq 1) 'Same-origin renewal restarted the status'
$first.IsOpenStack = $true; $first.MaxStackedCount = 10
[void][Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $template, 0, 2)
Assert-Start ($script:startCount -eq 1) 'Adding a stack restarted the same entity'
$first.IsOpenStack = $false; $first.ActionBehOfSameStatus = 1
[void][Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $template, 0, 2)
Assert-Start ($script:startCount -eq 1 -and $owner.Statuses.Count -eq 1 `
    -and [object]::ReferenceEquals($owner.Statuses[0], $first) -and $first.StackCount -eq 2) `
    'Same-origin renewal replaced the entity or discarded preserved stacks'
$other = $catalog.CreateStatus(12353, 1, 1, 3, 990004)
[void][Zjcs.Combat.Official.FightStatusLifecycle]::Apply($owner, $other, 0, 3)
Assert-Start ($script:startCount -eq 2 -and $owner.Statuses.Count -eq 2) `
    'A different source skill failed to create and start its independent status'
$callback.SetValue($owner, $null)
$near = $owner.Statuses[0]; $near.MoveNearMaxCountEachRound = 0
$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.Hp = $enemy.MaxHp = 100; $enemy.X = 1; $enemy.CharacterType = 1
$owner.Side = 1
$units = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($owner); $units.Add($enemy)
$invocations = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectMoveNearSkills($owner,
    $units, [Zjcs.Combat.Official.FightStatusTriggerRuntime]::MoveNearEventStart, $null,
    [Zjcs.Combat.Official.FightRandomStream]::new(23), $near)
Assert-Start ($invocations.Count -eq 1) 'Official MaxCountEachRound=0 was treated as a disabled trigger'
Write-Output 'Status startup passed: mid-hit official 12353 -> 12570, parent/trigger/hit times, same-origin replay renewal, preserved stacks, independent skill origins, no next-turn duplication, scoped callback and deterministic save.'

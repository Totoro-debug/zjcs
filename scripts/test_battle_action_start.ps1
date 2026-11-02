param([switch]$LethalOnly)
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Get-FightCombatSources)
function Assert-ActionStart($condition, $message) { if (-not $condition) { throw $message } }
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Text($text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
}
$skillJson = Read-Rule 'skill-rules'
$battleJson = Read-Rule 'battle-rules'
function Catalog($battleText) {
    [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        $skillJson, (Hash-Text $skillJson), '854_156383',
        $battleText, (Hash-Text $battleText), '854_156383')
}
$catalog = Catalog $battleJson
$rules = Read-Rule 'rules'
$formula = $catalog.CreateDamageFormula($rules, (Hash-Text $rules), '854_156383')
function Battle($stackCount, $selectedCatalog, $restore = $false, $enableFactor = $true, $actorHp = 100000) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    foreach ($id in 1, 2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $id; $u.Side = $id; $u.X = $id - 1; $u.CharacterType = 1
        $u.Hp = $(if ($id -eq 1) { $actorHp } else { 100000 })
        $u.MaxHp = 100000; $u.Level = 1; $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 1000 })
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        $cp.Units.Add($u)
    }
    $main = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $main.EntityId = 9900001; $main.Slot = 0; $main.TargetType = 4
    $main.ActionType = 6; $main.CanTriggerChild = $true
    $main.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
        EntityId = 9900002; DamageId = 9900003; DamageType = 8; DamageParam = 7
    })
    $cp.Units[0].Skills.Add($main)
    $cp.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $status = $selectedCatalog.CreateStatus(24346, 1, 2, 10, 0, 0, 6, 17, 10, $null)
    $status.StackCount = $stackCount
    $status.ActionStartSkills[0].StackedCountToDamageFactor = $enableFactor
    $cp.Units[0].Statuses.Add($status)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $selectedCatalog)
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $selectedCatalog)
    }
    $result = $runner.Step(0, 1)
    $hits = @($result.Event.DamageEvents | Where-Object SkillEntityId -eq 58980)
    Assert-ActionStart ($hits.Count -eq 1 -and $hits[0].SourceId -eq 2 -and $hits[0].TargetId -eq 1) 'Original 24346 did not trigger one creator-to-applicator hit'
    return @{ Result = $result; Damage = [Math]::Abs($hits[0].Damage) }
}
$lethal = Battle 1 $catalog $false $true 1
Assert-ActionStart ($lethal.Result.Checkpoint.Units[0].Hp -eq 0 -and
    @($lethal.Result.Event.DamageEvents | Where-Object SkillEntityId -eq 9900001).Count -eq 0) 'An actor killed by an action-start trigger continued its original hit'
if ($LethalOnly) {
    Write-Output 'Lethal action-start regression passed: the real nested damage kills the caster; the original hit does not settle.'
    return
}
$original = $catalog.CreateStatus(24346, 1, 2, 10)
Assert-ActionStart ($original.ActionStartRulesSupported -and $original.ActionStartSkills[0].StackedCountToDamageFactor) 'Original stacking action-start trigger remains unsupported'
$skill = $catalog.CreateSkill(58980, -1, 0, 6, 17, 10, $null)
$hits = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)
Assert-ActionStart ($hits.Count -eq 1 -and $hits[0].UseDamageFactor) 'Official hit 58981 lost UseDamageFactor'
$one = Battle 1 $catalog
$three = Battle 3 $catalog
Assert-ActionStart ($one.Damage -gt 0 -and $three.Damage -eq 3 * $one.Damage) 'Three status stacks did not multiply triggered skill damage by three'
Assert-ActionStart (@($three.Result.Event.DamageEvents | Where-Object {
    $_.SkillEntityId -eq 9900001 -and $_.Damage -eq -7
}).Count -eq 1) 'A nested trigger leaked its damage factor into the original cast'
$disabled = Battle 3 $catalog $false $false
Assert-ActionStart ($disabled.Damage -eq $one.Damage) 'Stack scaling applied with StackedCountToDamageFactor disabled'
$restored = Battle 3 $catalog $true
Assert-ActionStart ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored.Result.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($three.Result.Checkpoint)) 'Stacked trigger changed after checkpoint restore'

# The official switches are independent: a hit can opt out even when its status sends a factor.
$fixture = $battleJson | ConvertFrom-Json -AsHashtable
$fixture.entities['58981'].FightHitFixedComponent.UseDamageFactor = $false
$noFactorCatalog = Catalog ($fixture | ConvertTo-Json -Depth 100 -Compress)
$noFactor = Battle 3 $noFactorCatalog
Assert-ActionStart ($noFactor.Damage -eq $one.Damage) 'Hit ignored its UseDamageFactor=false switch'

# Unsupported custom damage must be rejected even when PropTarget is null. The old
# parenthesization silently accepted it and then executed ordinary damage instead.
$fixture = $battleJson | ConvertFrom-Json -AsHashtable
$trigger = $fixture.entities['24346'].FightStatusActionStartComponent.Skills[0]
$trigger.PropTarget = $null; $trigger.CustomDamageInfo.BaseCurrentHp = $true
$unsupported = Catalog ($fixture | ConvertTo-Json -Depth 100 -Compress)
Assert-ActionStart (-not $unsupported.CreateStatus(24346, 1, 2, 10).ActionStartRulesSupported) 'Custom damage bypassed support checks through a null PropTarget'
$trigger.CustomDamageInfo.BaseCurrentHp = $false
$trigger.TargetWithoutStatusIds = @(26004)
$unsupported = Catalog ($fixture | ConvertTo-Json -Depth 100 -Compress)
Assert-ActionStart (-not $unsupported.CreateStatus(24346, 1, 2, 10).ActionStartRulesSupported) 'Unsupported target-status exclusions were silently ignored'
Write-Output "Battle action-start checks passed: original 24346 -> 58980, damage $($one.Damage) -> $($three.Damage), independent hit/status switches, cultivation, deterministic restore and unsupported-field checks."

function Movement-Battle($kind, $canTrigger = $true, $whitelist = $false, $restore = $false,
    $blocked = $false, $lethal = $false, $nested = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width = 12; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    foreach ($id in 1, 2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $id; $u.Side = $id; $u.X = $(if ($id -eq 1) { 3 } else { 5 })
        $u.Y = 2; $u.CharacterType = 1; $u.Hp = $u.MaxHp = 100000
        $u.Level = $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 1000 })
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        $cp.Units.Add($u)
    }
    $main = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $main.EntityId = 9900001; $main.Slot = 0; $main.TargetType = 1
    $main.ActionType = 2; $main.CanTriggerChild = $canTrigger
    if ($whitelist) { $main.CanTriggerStatusEntityIds.Add(24346) }
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]@{
        EntityId = 9900002; DamageId = 9900003; DamageType = 8; DamageParam = 7
    }
    $main.Hits.Add($hit)
    $ownerId = 1
    if ($kind -eq 1) {
        $cp.Units[0].X = 1; $cp.Units[0].MoveDist = 3
        $main.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    } elseif ($kind -eq 6) {
        $move = $catalog.CreateSkill(13500, -1, 0, 1, 1, 1, $null).SourceMoves[0]
        $main.SourceMoves.Add($move)
        if ($blocked) { $cp.Units[0].X = 0; $cp.Units[1].X = 2 }
    } elseif ($kind -eq 7 -or $kind -eq 8) {
        $ownerId = 2
        $officialSkillId = $(if ($kind -eq 7) { 13550 } else { 64360 })
        $officialMoveId = $(if ($kind -eq 7) { 13553 } else { 64363 })
        $originalHit = $catalog.CreateSkill($officialSkillId, -1, 0, 1, 1, 1, $null).HitGraphEntities |
            Where-Object { $null -ne $_.HitMove -and $_.HitMove.MoveBehaviourId -eq $officialMoveId } |
            Select-Object -First 1
        Assert-ActionStart ($null -ne $originalHit.HitMove) "Official movement action $officialMoveId is missing from skill $officialSkillId"
        $hit.HitMove = $originalHit.HitMove
        Assert-ActionStart ($catalog.EntityActionType($hit.HitMove.MoveBehaviourId) -eq $kind) 'Movement action type was not taken from its original entity'
        if ($kind -eq 7) { $cp.Units[1].X = 4 }
    } elseif ($kind -eq 9) {
        # No skill is ready: the existing offline retreat action must notify as Move, too.
        $cp.Units[0].MoveDist = 2
    }
    if ($cp.Units[0].MoveDist -gt 0) {
        $cp.Units[0].Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
            Id = $catalog.PropertyType('MoveDist'); Value = $cp.Units[0].MoveDist
        })
    }
    $cp.Units[0].Skills.Add($main)
    $cp.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
        Slot = 0; LimitedTimes = $(if ($kind -eq 9) { 0 } else { -1 })
    })
    $creatorId = 3 - $ownerId
    $status = $catalog.CreateStatus(24346, $ownerId, $creatorId, 10, 0, 0, 6, 17, 10, $null)
    $status.StackCount = 3
    if ($nested) { $status.ActionStartSkills[0].EntityId = 13500 }
    $cp.Units[$ownerId - 1].Statuses.Add($status)
    if ($lethal) { $cp.Units[$ownerId - 1].Hp = $cp.Units[$ownerId - 1].Turn.Hp = 1 }
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
    if ($nested) {
        $nestedMoves = @($result.Event.Movements | Where-Object UnitId -eq 2)
        Assert-ActionStart ($result.Event.Movements.Count -eq 2 -and $nestedMoves.Count -eq 1) 'Nested movement was duplicated or lost'
        Assert-ActionStart ([Math]::Abs($nestedMoves[0].Delay - 2 * $main.SourceMoves[0].Delay) -lt 0.000001) 'The outer movement overwrote the nested action delay'
        Assert-ActionStart (@($result.Event.SkillCasts | Where-Object {
            $_.SkillEntityId -eq 13500 -and $_.ActorId -eq 2 -and $_.TargetIds.Contains(1)
        }).Count -eq 1) 'Nested movement skill changed its original status source or target'
        return $result
    }
    $triggerHits = @($result.Event.DamageEvents | Where-Object SkillEntityId -eq 58980)
    $expected = $(if ($blocked -or (($kind -ne 1 -and $kind -ne 9) -and -not $canTrigger -and -not $whitelist)) { 0 } else { 1 })
    Assert-ActionStart ($triggerHits.Count -eq $expected) "Movement $kind triggered $($triggerHits.Count) times, expected $expected; paths=$($result.Event.Movements.Count), moveDist=$($cp.Units[0].MoveDist), position=$($cp.Units[0].X),$($cp.Units[0].Y)"
    Assert-ActionStart ($result.Event.Movements.Count -eq $(if ($blocked) { 0 } else { 1 })) 'The passive gate changed the directly configured movement'
    if ($kind -ne 9 -and -not $lethal) {
        Assert-ActionStart (@($result.Event.DamageEvents | Where-Object {
            $_.SkillEntityId -eq 9900001 -and $_.TargetId -eq 2 -and $_.Damage -eq -7
        }).Count -eq 1) 'The movement passive changed or suppressed the original hit'
    }
    if ($expected -gt 0) {
        Assert-ActionStart ($triggerHits[0].SourceId -eq $creatorId -and $triggerHits[0].TargetId -eq $ownerId) 'Movement trigger changed the original source or owner'
        if (-not $lethal) { Assert-ActionStart ([Math]::Abs($triggerHits[0].Damage) -eq $three.Damage) 'Movement lost its three-stack damage factor' }
        if ($kind -eq 6) { Assert-ActionStart ($triggerHits[0].Delay -ge 0.8) 'Triggered damage lost its source movement delay' }
    }
    return $result
}
$movementResults = @{}
foreach ($kind in 1, 6, 7, 8, 9) { $movementResults[$kind] = Movement-Battle $kind }
$null = Movement-Battle 1 $false
$null = Movement-Battle 6 $true $false $false $false $false $true
foreach ($kind in 6, 7, 8) {
    $null = Movement-Battle $kind $false
    $null = Movement-Battle $kind $false $true
}
$null = Movement-Battle 6 $true $false $false $true
$movementRestored = Movement-Battle 1 $true $false $true
Assert-ActionStart ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($movementRestored.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($movementResults[1].Checkpoint)) 'Movement trigger changed after checkpoint restore'
$movementLethal = Movement-Battle 1 $true $false $false $false $true
Assert-ActionStart ($movementLethal.Checkpoint.Units[0].Hp -eq 0 -and
    @($movementLethal.Event.SkillCasts | Where-Object SkillEntityId -eq 9900001).Count -eq 0) 'Actor killed on movement start released its waiting skill'
Write-Output 'Movement action-start checks passed: automatic movement, retreat, original skill movement / knockback / drag, whitelist, blocked path, nested timing, delayed damage, lethal movement and checkpoint restore.'

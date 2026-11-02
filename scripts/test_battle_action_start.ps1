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

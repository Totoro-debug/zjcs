. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

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

function Invoke-ReplayBattle($skill, $stacks) {
    $player = New-Unit 1 1 0 0 1000 100
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $status = $catalog.CreateStatus(24031, 1, 1)
    $status.StackCount = $stacks
    $player.Statuses.Add($status)
    $enemy = New-Unit 2 2 1 0 5000 1
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    return $executor.Step(0, 1)
}

# Enough stacks: the skill replays once and consumes the configured stacks.
$replayed = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 10
$casts = @($replayed.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 -and $_.Slot -eq 0 })
Assert-Replay ($casts.Count -eq 2) "Replay cast count is $($casts.Count) instead of 2"
Assert-Replay (($replayed.Checkpoint.Units[0].Statuses | Where-Object { $_.ClassId -eq 24031 }).Count -eq 0) 'Replay did not consume the status stacks'

# Not enough stacks: no replay.
$notEnough = Invoke-ReplayBattle (New-ReplaySkill $true 10 10 1) 5
Assert-Replay (@($notEnough.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 1) 'Replay fired without enough stacks'

# Without consuming, the replayed skill must not replay again (single level).
$nested = Invoke-ReplayBattle (New-ReplaySkill $false 1 1 1) 20
Assert-Replay (@($nested.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 930 }).Count -eq 2) 'Replay cascaded beyond one level'

Write-Output 'Battle skill replay checks passed: official mapping, stack condition, consumption and single-level replay.'

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Child($condition, $message) { if (-not $condition) { throw $message } }
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

$officialHit = $catalog.CreateSkill(12730, 0, 0, 1, 1, 1, $null).HitGraphEntities |
    Where-Object { $_.EntityId -eq 12731 } | Select-Object -First 1
Assert-Child ($null -ne $officialHit -and $officialHit.ChildSkillId -eq 12733) 'Official child skill mapping mismatch'
Assert-Child ([Math]::Abs($officialHit.ChildSkillLessThanHpPer - 0.3) -lt 0.0001 -and [Math]::Abs($officialHit.ChildSkillRate - 1) -lt 0.0001) 'Official child skill condition mismatch'

function New-Unit($id, $side, $x, $y, $hp, $maxHp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $maxHp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = $speed
    return $unit
}

function New-ChildSkill($childId, $rate, $per) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = 940; $skill.ItemId = 941; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 942; $hit.DamageId = 943; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $hit.ChildSkillId = $childId; $hit.ChildSkillRate = $rate; $hit.ChildSkillLessThanHpPer = $per
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-ChildBattle($skill, $enemyHp, $enemyMaxHp) {
    $player = New-Unit 1 1 0 0 5000 5000 100
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $enemy = New-Unit 2 2 1 0 $enemyHp $enemyMaxHp 1
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 29
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    return $executor.Step(0, 1)
}

# The damaged enemy releases the child skill (10640) at the player.
$result = Invoke-ChildBattle (New-ChildSkill 10640 1.0 -1) 5000 5000
$childCasts = @($result.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 10640 -and $_.ActorId -eq 2 })
Assert-Child ($childCasts.Count -eq 1) 'Child skill was not released by the damaged target'

# HP condition: only below 30% of maximum HP.
$high = Invoke-ChildBattle (New-ChildSkill 10640 1.0 0.3) 4000 5000
Assert-Child (@($high.Event.SkillCasts | Where-Object { $_.Slot -eq -1 }).Count -eq 0) 'Child skill fired above its HP threshold'
$low = Invoke-ChildBattle (New-ChildSkill 10640 1.0 0.3) 100 5000
Assert-Child (@($low.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 10640 }).Count -eq 1) 'Child skill did not fire below its HP threshold'

# Zero rate never triggers, and child skills do not cascade.
$zero = Invoke-ChildBattle (New-ChildSkill 10640 0.0 -1) 5000 5000
Assert-Child (@($zero.Event.SkillCasts | Where-Object { $_.Slot -eq -1 }).Count -eq 0) 'Zero-rate child skill was released'
$nested = Invoke-ChildBattle (New-ChildSkill 12730 1.0 -1) 5000 5000
Assert-Child (@($nested.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 12730 }).Count -eq 1) 'Child skill cascade was not limited to one level'

Write-Output 'Battle child skill checks passed: official mapping, damaged-target cast, HP condition, zero rate and single-level cascade.'

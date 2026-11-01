. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Extra($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
$skillSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'))
$battleSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/battle-rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    $skillSource.OutputSha256, $skillSource.Manifest,
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    $battleSource.OutputSha256, $battleSource.Manifest)

$official = $catalog.CreateSkill(12090, 0, 0, 1, 1, 1, $null)
Assert-Extra ($official.ExtraSkills.Count -eq 2) 'Official extra skill list mismatch'
Assert-Extra ($official.ExtraSkills[0].SkillId -eq 12093 -and [Math]::Abs($official.ExtraSkills[0].Rate - 0.6) -lt 0.0001) 'Official extra skill entry mismatch'

function New-Unit($id, $side, $x, $y, $hp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = $speed
    return $unit
}

function New-BaseSkill($extraId, $rate) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = 920; $skill.ItemId = 921; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 922; $hit.DamageId = 923; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 10; $hit.TargetMode = 0
    $skill.Hits.Add($hit)
    if ($extraId -gt 0) {
        $skill.ExtraSkills.Add([Zjcs.Combat.Official.FightBattleSkillExtra]@{ SkillId = $extraId; Rate = $rate })
    }
    return $skill
}

function Invoke-ExtraBattle($skill) {
    $player = New-Unit 1 1 0 0 1000 100
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $enemy = New-Unit 2 2 1 0 5000 1
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 17
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    return $executor.Step(0, 1)
}

# A rate-1 extra skill is released at action end and recorded as Slot -1.
$result = Invoke-ExtraBattle (New-BaseSkill 10640 1.0)
$extras = @($result.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 10640 -and $_.ActorId -eq 1 })
Assert-Extra ($extras.Count -eq 1) 'Extra skill was not released'
Assert-Extra ($result.Event.SkillCasts.Count -eq 2) 'Base or extra cast event count mismatch'

# A zero rate never releases the extra skill.
$zero = Invoke-ExtraBattle (New-BaseSkill 10640 0.0)
Assert-Extra (@($zero.Event.SkillCasts | Where-Object { $_.Slot -eq -1 }).Count -eq 0) 'Zero-rate extra skill was released'

# The confirmed offline rule follows the extra list on 12090 after it is released as an extra.
$nested = Invoke-ExtraBattle (New-BaseSkill 12090 1.0)
$nestedExtras = @($nested.Event.SkillCasts | Where-Object { $_.Slot -eq -1 })
Assert-Extra ($nestedExtras.Count -gt 1 -and $nestedExtras[0].SkillEntityId -eq 12090) 'Configured extra skill cascade was truncated'
Assert-Extra (@($nestedExtras | Where-Object { $_.SkillEntityId -in @(12093, 12094) }).Count -gt 0) 'Nested official extra skills were not executed'

# Attached action-end skills use the triggering skill's cultivation too.
$cultivated = New-BaseSkill 63434 1.0
$cultivated.Rank = 6; $cultivated.Level = 17; $cultivated.SubRank = 10
$inherited = Invoke-ExtraBattle $cultivated
$status = @($inherited.Checkpoint.Units | ForEach-Object { $_.Statuses } |
    Where-Object { $_.ClassId -eq 24658 -and $_.RootSkillId -eq 63434 })
Assert-Extra ($status.Count -gt 0) 'Official attached skill did not apply its configured status'
Assert-Extra ($status[0].SourceSkillRank -eq 6 -and $status[0].SourceSkillLevel -eq 17 -and $status[0].SourceSubRank -eq 10) 'Attached skill did not inherit its triggering skill cultivation'

Write-Output 'Battle extra skill checks passed: official mapping, rate release, zero rate, configured cascade and inherited cultivation.'

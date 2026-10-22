$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Limit($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Unit($id, $side, $x, $y, $hp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = $speed
    return $unit
}

function New-ZeroDamageSkill() {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = 700; $skill.ItemId = 701; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 702; $hit.DamageId = 703; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 0; $hit.TargetMode = 0
    $skill.Hits.Add($hit)
    return $skill
}

function New-LimitBattle($playerLimitEnabled, $playerLimit) {
    $player = New-Unit 1 1 0 0 1000 100
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add((New-ZeroDamageSkill))
    $enemy = New-Unit 2 2 1 0 10000 50
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.RandomSeed = 1; $checkpoint.MaxRound = 20
    $checkpoint.IsPlayerRoundLimit = $playerLimitEnabled
    $checkpoint.PlayerRoundLimit = $playerLimit
    $checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0; $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
    return $checkpoint
}

$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())

$limited = New-LimitBattle $true 2
$limitedExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $limited, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$first = $limitedExecutor.Step(0, 1)
Assert-Limit (-not $first.Checkpoint.Finished -and -not $first.Checkpoint.Outcome.Finished) 'Battle ended before the configured personal round limit'
$limitedExecutor.Step(0, 2) | Out-Null
$second = $limitedExecutor.Step(0, 3)
Assert-Limit ($second.Checkpoint.Finished -and $second.Checkpoint.Outcome.Reason -eq 4) 'Personal round limit did not end the battle with reason 4'
Assert-Limit (-not $second.Checkpoint.Outcome.Won -and $second.Checkpoint.Outcome.Round -eq $second.Event.GlobalRound) 'Personal round limit outcome was not recorded as a loss at the current round'
$rejected = $false
try { $null = $limitedExecutor.Step(0, 4) } catch { $rejected = $true }
Assert-Limit $rejected 'Finished personal-round-limit battle accepted another step'

$unlimited = New-LimitBattle $false 2
$unlimitedExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $unlimited, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$null = $unlimitedExecutor.Step(0, 1)
$null = $unlimitedExecutor.Step(0, 2)
$unlimitedSecond = $unlimitedExecutor.Step(0, 3)
Assert-Limit (-not $unlimitedSecond.Checkpoint.Finished) 'Disabled personal round limit ended the battle'

$zeroLimit = New-LimitBattle $true 0
$zeroExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $zeroLimit, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$null = $zeroExecutor.Step(0, 1)
$null = $zeroExecutor.Step(0, 2)
$zeroSecond = $zeroExecutor.Step(0, 3)
Assert-Limit (-not $zeroSecond.Checkpoint.Finished) 'Zero personal round limit was treated as an active limit'

Write-Output 'Battle stage limit checks passed: personal round limit reason, stop after limit, disabled and zero limits.'

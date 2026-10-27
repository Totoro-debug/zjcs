. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Report($condition, $message) { if (-not $condition) { throw $message } }

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1; $player.Hp = 1000; $player.MaxHp = 1000
$player.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $player.Turn.Id = 1; $player.Turn.Hp = 1000; $player.Turn.Speed = 100
$player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 900; $skill.ItemId = 901; $skill.TargetType = 1
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = 902; $hit.DamageId = 903; $hit.DamageType = 8
$hit.HasCustomDamage = $true; $hit.CustomDamage = 1200; $hit.TargetMode = 0
$skill.Hits.Add($hit)
$player.Skills.Add($skill)
$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2; $enemy.X = 1; $enemy.Y = 0
$enemy.Hp = 1000; $enemy.MaxHp = 1000
$enemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $enemy.Turn.Id = 2; $enemy.Turn.Hp = 1000; $enemy.Turn.Speed = 1
$cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$cp.StageId = 7; $cp.MaxRound = 10; $cp.RandomSeed = 3
$cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
$cp.Units.Add($player); $cp.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$result = $executor.Step(0, 1)
Assert-Report ($cp.Finished -and $cp.Outcome.Won) 'Battle did not finish with a victory for the report test'

$report = [Zjcs.Combat.Official.FightBattleReportBuilder]::Build($cp)
Assert-Report ($report.StageId -eq 7 -and $report.Won -and $report.Reason -eq $cp.Outcome.Reason) 'Report header mismatch'
Assert-Report ($report.Units.Count -eq 2) 'Report unit row count mismatch'
$playerRow = $report.Unit(1)
$enemyRow = $report.Unit(2)
Assert-Report ($null -ne $playerRow -and $null -ne $enemyRow) 'Report lost a unit row'
Assert-Report ($playerRow.Casts -eq 1 -and $playerRow.Hits -eq 1 -and $playerRow.DamageDealt -eq 1000) 'Report player row mismatch'
Assert-Report ($enemyRow.DamageTaken -eq 1000 -and -not $enemyRow.Alive -and $enemyRow.Hp -eq 0) 'Report enemy row mismatch'
Assert-Report ($report.TotalDamageDealt -eq 1000 -and $report.TotalDamageTaken -eq 1000) 'Report totals mismatch'
Assert-Report ($report.Skills.Count -eq 1 -and $report.Skills[0].Casts -eq 1 -and $report.Skills[0].Hits -eq 1 -and $report.Skills[0].Damage -eq 1000) 'Report skill row mismatch'
Assert-Report ($report.PlayerHp -eq 1000 -and $report.PlayerMaxHp -eq 1000) 'Report player HP mismatch'

Write-Output 'Battle report checks passed: header, unit rows, skill rows, totals, HP and outcome.'

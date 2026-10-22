$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Timing($condition, $message) {
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

function New-FixedHit($entityId, $damageId, $damage) {
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = $entityId; $hit.DamageId = $damageId; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = $damage; $hit.TargetMode = 0
    return $hit
}

$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())

# Configured delays on the skill's hit list drive both the damage event timeline and step length.
$player = New-Unit 1 1 0 0 1000 100
$player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 100; $skill.ItemId = 200; $skill.TargetType = 1
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$skill.ActionDuration = 1.2
$skill.AllRelevantEndedDuration = 0.4
$firstHit = New-FixedHit 300 400 10
$firstHit.Delay = 0.2
$secondHit = New-FixedHit 301 401 10
$secondHit.Delay = 0.6
$skill.Hits.Add($firstHit)
$skill.Hits.Add($secondHit)
$player.Skills.Add($skill)
$enemy = New-Unit 2 2 1 0 100 50
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.RandomSeed = 1
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$result = $executor.Step(0, 1)
Assert-Timing ($result.Event.DamageEvents.Count -eq 2) 'Timed skill did not resolve both configured hits'
Assert-Timing ([Math]::Abs($result.Event.DamageEvents[0].Delay - 0.2) -lt 0.0001 -and [Math]::Abs($result.Event.DamageEvents[1].Delay - 0.6) -lt 0.0001) 'Hit delay was not recorded on the damage events'
Assert-Timing ([Math]::Abs($result.Event.Duration - 1.0) -lt 0.0001) 'Step duration did not use the last hit delay plus AllRelevantEndedDuration'
Assert-Timing ([Math]::Abs($result.Event.ActionDuration - 1.2) -lt 0.0001) 'Step action duration did not use the configured ActionDuration'

# Root references carry their own delay and anchor, not the shared hit entity's defaults.
$rootedPlayer = New-Unit 1 1 0 0 1000 100
$rootedPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$rootedPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$rootedSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$rootedSkill.Slot = 0; $rootedSkill.EntityId = 110; $rootedSkill.ItemId = 210; $rootedSkill.TargetType = 1
$rootedSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$rootedSkill.Duration = 0.5
$rootedSkill.HitGraphRootEntityIds.Add(500)
$root = [Zjcs.Combat.Official.FightBattleHitRoot]::new()
$root.EntityId = 500; $root.Delay = 0.3; $root.Anchor = 1
$rootedSkill.HitGraphRoots.Add($root)
$entity = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$entity.EntityId = 500; $entity.HasFixedDamage = $true; $entity.DamageType = 8; $entity.DamageParam = 10
$entity.Delay = 0.05
$rootedSkill.HitGraphEntities.Add($entity)
$rootedPlayer.Skills.Add($rootedSkill)
$rootedEnemy = New-Unit 2 2 1 0 100 50
$rootedCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$rootedCheckpoint.StageId = 1; $rootedCheckpoint.RandomSeed = 1
$rootedCheckpoint.Units.Add($rootedPlayer); $rootedCheckpoint.Units.Add($rootedEnemy)
$rootedWave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$rootedWave.Index = 0; $rootedWave.UnitIds.Add(2); $rootedCheckpoint.Waves.Add($rootedWave)
$rootedExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $rootedCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$rootedResult = $rootedExecutor.Step(0, 1)
Assert-Timing ($rootedResult.Event.DamageEvents.Count -eq 1) 'Rooted hit graph did not resolve its configured root'
Assert-Timing ([Math]::Abs($rootedResult.Event.DamageEvents[0].Delay - 0.3) -lt 0.0001 -and $rootedResult.Event.DamageEvents[0].Anchor -eq 1) 'Root reference delay or anchor was not applied to the first hit node'
Assert-Timing ([Math]::Abs($rootedResult.Event.Duration - 0.5) -lt 0.0001) 'Step duration did not fall back to the configured skill Duration'

# Movement adds its own presentation time when the skill has to travel first.
$walkPlayer = New-Unit 1 1 0 0 1000 100
$walkPlayer.MoveDist = 4
$walkPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$walkPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$walkSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$walkSkill.Slot = 0; $walkSkill.EntityId = 120; $walkSkill.ItemId = 220; $walkSkill.TargetType = 1
$walkSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$walkSkill.Duration = 0.2
$walkSkill.Hits.Add((New-FixedHit 302 402 10))
$walkPlayer.Skills.Add($walkSkill)
$walkEnemy = New-Unit 2 2 3 0 100 50
$walkCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$walkCheckpoint.StageId = 1; $walkCheckpoint.RandomSeed = 1
$walkCheckpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$walkCheckpoint.Grid.Width = 10; $walkCheckpoint.Grid.Height = 10; $walkCheckpoint.Grid.DefaultGroundType = 2
$walkCheckpoint.Units.Add($walkPlayer); $walkCheckpoint.Units.Add($walkEnemy)
$walkWave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$walkWave.Index = 0; $walkWave.UnitIds.Add(2); $walkCheckpoint.Waves.Add($walkWave)
$walkExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $walkCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$walkResult = $walkExecutor.Step(0, 1)
Assert-Timing ($walkResult.Event.Movements.Count -eq 1 -and $walkResult.Event.Movements[0].Path.Count -eq 3) 'Skill did not move into range before hitting'
Assert-Timing ($walkResult.Event.Duration -ge 1.0) 'Step duration did not include the movement time of two cells'

Write-Output 'Battle step timing checks passed: configured hit delays, root anchor/delay, action duration, lifetime duration and movement pacing.'

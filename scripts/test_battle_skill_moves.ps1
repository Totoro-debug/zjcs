$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Move($condition, $message) {
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

function New-Skill($entityId, $rangeX) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = $entityId; $skill.ItemId = $entityId + 1000; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new($rangeX, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = $entityId + 1; $hit.DamageId = $entityId + 2; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-Battle($player, $enemy, $extra = $null) {
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.RandomSeed = 1
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 12; $checkpoint.Grid.Height = 12; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
    if ($extra -ne $null) { foreach ($unit in $extra) { $checkpoint.Units.Add($unit) } }
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0; $wave.UnitIds.Add($enemy.Id); $checkpoint.Waves.Add($wave)
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
        $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
    return $executor.Step(0, 1)
}

# Distance -1 pulls the caster next to the target.
$pullPlayer = New-Unit 1 1 0 0 1000 100
$pullPlayer.MoveDist = 0
$pullPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$pullPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$pullSkill = New-Skill 3001 4
$pullMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
$pullMove.Distance = -1; $pullMove.Direction = 1; $pullMove.TowardsTargetPos = $true
$pullSkill.SourceMoves.Add($pullMove)
$pullPlayer.Skills.Add($pullSkill)
$pullEnemy = New-Unit 2 2 4 0 5000 50
$pullResult = Invoke-Battle $pullPlayer $pullEnemy
Assert-Move ($pullPlayer.X -eq 3 -and $pullPlayer.Y -eq 0) 'Distance -1 did not pull the caster next to the target'
Assert-Move ($pullResult.Event.Movements.Count -eq 1 -and $pullResult.Event.Movements[0].Path.Count -eq 4) 'Pull movement path mismatch'
Assert-Move ($pullResult.Event.Movements[0].Path[3].X -eq 3 -and $pullResult.Event.DamageEvents.Count -eq 1) 'Pull movement end cell or hit mismatch'

# Positive distance moves toward the target.
$dashPlayer = New-Unit 1 1 0 0 1000 100
$dashPlayer.MoveDist = 0
$dashPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$dashPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$dashSkill = New-Skill 3002 4
$dashMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
$dashMove.Distance = 2; $dashMove.Direction = 1; $dashMove.TowardsTargetPos = $true
$dashSkill.SourceMoves.Add($dashMove)
$dashPlayer.Skills.Add($dashSkill)
$dashEnemy = New-Unit 2 2 4 0 5000 50
$dashResult = Invoke-Battle $dashPlayer $dashEnemy
Assert-Move ($dashPlayer.X -eq 2 -and $dashResult.Event.Movements[0].Path.Count -eq 3) 'Positive-distance skill move mismatch'

# Occupied destination falls back to a shorter legal distance.
$blockedPlayer = New-Unit 1 1 0 0 1000 100
$blockedPlayer.MoveDist = 0
$blockedPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$blockedPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$blockedSkill = New-Skill 3003 4
$blockedMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
$blockedMove.Distance = 2; $blockedMove.Direction = 1; $blockedMove.TowardsTargetPos = $true
$blockedSkill.SourceMoves.Add($blockedMove)
$blockedPlayer.Skills.Add($blockedSkill)
$blockedEnemy = New-Unit 2 2 4 0 5000 50
$blockedAlly = New-Unit 3 1 2 0 500 50
Assert-Move ($blockedAlly.X -eq 2) 'Ally placement for the blocked move is wrong'
$blockedResult = Invoke-Battle $blockedPlayer $blockedEnemy @($blockedAlly)
Assert-Move ($blockedPlayer.X -eq 1) 'Skill move did not fall back to a shorter legal distance'

# Unsupported cases are skipped instead of guessing.
$skipPlayer = New-Unit 1 1 0 0 1000 100
$skipPlayer.MoveDist = 0
$skipPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$skipPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$skipSkill = New-Skill 3004 4
$conditional = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
$conditional.Distance = 2; $conditional.Direction = 1; $conditional.TowardsTargetPos = $true
$conditional.HasConditions = $true
$skipSkill.SourceMoves.Add($conditional)
$unsupported = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
$unsupported.Distance = 2; $unsupported.Direction = 0; $unsupported.TowardsTargetPos = $true
$skipSkill.SourceMoves.Add($unsupported)
$skipPlayer.Skills.Add($skipSkill)
$skipEnemy = New-Unit 2 2 4 0 5000 50
$skipResult = Invoke-Battle $skipPlayer $skipEnemy
Assert-Move ($skipPlayer.X -eq 0 -and $skipResult.Event.Movements.Count -eq 0) 'Conditional or unsupported skill move was applied'

Write-Output 'Battle skill move checks passed: pull to target, positive distance, occupied-cell fallback and unsupported-case skipping.'

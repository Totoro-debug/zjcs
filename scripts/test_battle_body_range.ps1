. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)

function Assert-Body($condition, $message) { if (-not $condition) { throw $message } }
function New-BodyUnit($id, $side, $x, $y) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.X = $x; $unit.Y = $y
    $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.Hp = 1000; $unit.MaxHp = 1000
    $unit.Turn.Id = $id; $unit.Turn.Hp = 1000; $unit.Turn.Speed = 101 - $id
    return $unit
}
function New-BodyBoss {
    $unit = New-BodyUnit 2 2 5 4
    $unit.BodyOffsets.Clear()
    for ($x = -2; $x -le 2; $x++) {
        for ($y = -2; $y -le 2; $y++) {
            $unit.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x, $y))
        }
    }
    return $unit
}
function New-BodySkill {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    # Synthetic fixed-damage fixture isolates geometry. The Play Mode acceptance
    # separately uses the untouched original 10011 skill and its damage formula.
    $skill.Slot = 0; $skill.EntityId = 9000; $skill.ItemId = 19000
    $skill.TargetType = 1; $skill.SkillTargetType = 1
    foreach ($point in @(@(1,0), @(0,1), @(-1,0), @(0,-1))) {
        $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new($point[0], $point[1]))
    }
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 9001; $hit.DamageId = 9002; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 17; $hit.TargetMode = 1; $hit.Anchor = 1
    # Several covered cells belong to the same boss; it must take one hit.
    foreach ($point in @(@(1,0), @(1,1), @(2,0), @(0,1), @(0,-1), @(-1,0))) {
        $hit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new($point[0], $point[1]))
    }
    $skill.Hits.Add($hit)
    return $skill
}
function New-BodyBattle($player, $enemy) {
    $skill = New-BodySkill
    $player.Skills.Add($skill)
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
        Slot = 0; Cooldown = 3; LimitedTimes = 2; ResetCooldownAtStart = $true
    })
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.RandomSeed = 43
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{
        Width = 12; Height = 10; DefaultGroundType = 2
    }
    $checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = 0; $wave.UnitIds.Add($enemy.Id); $checkpoint.Waves.Add($wave)
    return $checkpoint
}
function Invoke-BodyStep($checkpoint) {
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
        [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
        [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint,
        $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
    return $executor.Step(0, 1)
}

# The target centre is three cells away, while its nearest body tile is adjacent.
$player = New-BodyUnit 1 1 2 4
$boss = New-BodyBoss
$battle = New-BodyBattle $player $boss
$result = Invoke-BodyStep $battle
Assert-Body ($result.Event.DamageEvents.Count -eq 1 -and $boss.Hp -eq 983) 'Melee did not hit the boss body edge exactly once'
Assert-Body ($result.Event.Movements.Count -eq 0) 'An already reachable body edge caused movement'
Assert-Body ($player.SkillState.Slots[0].ReleasedTimes -eq 1) 'Body coverage multiplied the skill release count'
$cooldown = [Zjcs.Combat.Official.FightSkillState]::new($player.SkillState)
Assert-Body ($cooldown.LeftCooldown(0) -gt 0) 'Body-edge hit did not consume the skill cooldown'
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($battle))
Assert-Body ($restored.Units[1].BodyOffsets.Count -eq 25 -and $restored.Units[1].Hp -eq 983) 'Checkpoint lost the body footprint or damage'
Assert-Body ($restored.Units[0].SkillState.Slots[0].ReleasedTimes -eq 1) 'Cold resume lost skill release state'
Assert-Body ([Zjcs.Offline.Combat.OfflineBattlePartEnd]::Finish($restored)) 'Could not settle body-edge combat'
$report = [Zjcs.Combat.Official.FightBattleReportBuilder]::Build($restored)
$playerReport = @($report.Units | Where-Object UnitId -eq 1)[0]
Assert-Body ($playerReport.DamageDealt -eq 17) 'Body cells multiplied report damage'

# Approach the body edge, with the boss footprint blocking every interior tile.
$movingPlayer = New-BodyUnit 1 1 0 4; $movingPlayer.MoveDist = 2
$movingBoss = New-BodyBoss
$movingBattle = New-BodyBattle $movingPlayer $movingBoss
$movingResult = Invoke-BodyStep $movingBattle
Assert-Body ($movingPlayer.X -eq 2 -and $movingPlayer.Y -eq 4) 'Approach did not stop at a legal body-edge casting position'
Assert-Body ($movingResult.Event.Movements.Count -eq 1 -and $movingResult.Event.Movements[0].Path.Count -eq 3) 'Body-edge approach exceeded movement distance'
Assert-Body ($movingResult.Event.DamageEvents.Count -eq 1 -and $movingBoss.Hp -eq 983) 'Approach failed to cast against the boss body'
foreach ($point in $movingResult.Event.Movements[0].Path) {
    Assert-Body ($point.X -lt 3) 'Approach entered the occupied boss body'
}

# A blocked nearest position must use another reachable edge, not teleport through it.
$blockedPlayer = New-BodyUnit 1 1 0 4; $blockedPlayer.MoveDist = 3
$blockedBattle = New-BodyBattle $blockedPlayer (New-BodyBoss)
$blockedBattle.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,4))
$blockedResult = Invoke-BodyStep $blockedBattle
Assert-Body ($blockedResult.Event.DamageEvents.Count -eq 1) 'A blocked closest tile prevented a reachable alternate edge cast'
foreach ($movement in $blockedResult.Event.Movements) {
    Assert-Body ($movement.Path.Count -le 4) 'Alternate approach exceeded movement distance'
    foreach ($point in $movement.Path) {
        Assert-Body (-not ($point.X -eq 2 -and $point.Y -eq 4) -and $point.X -lt 3) 'Alternate approach crossed an obstacle or occupied body'
    }
}

# A complete barrier cannot be crossed merely because a body cell is in range beyond it.
$walledPlayer = New-BodyUnit 1 1 0 4; $walledPlayer.MoveDist = 3
$walledBattle = New-BodyBattle $walledPlayer (New-BodyBoss)
for ($y=0; $y -lt 10; $y++) { $walledBattle.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,$y)) }
$walledResult = Invoke-BodyStep $walledBattle
Assert-Body ($walledResult.Event.DamageEvents.Count -eq 0 -and $walledPlayer.SkillState.Slots[0].ReleasedTimes -eq 0) 'An unreachable target consumed a cast or took damage'
foreach ($movement in $walledResult.Event.Movements) {
    foreach ($point in $movement.Path) { Assert-Body ($point.X -lt 2) 'Approach crossed a full blocking wall' }
}

# Range originates at the caster centre; the caster's own body must not extend it.
$widePlayer = New-BodyUnit 1 1 1 4
$widePlayer.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$wideBattle = New-BodyBattle $widePlayer (New-BodyBoss)
$wideResult = Invoke-BodyStep $wideBattle
Assert-Body ($wideResult.Event.DamageEvents.Count -eq 0) 'Caster body incorrectly extended the skill range'

# Single-cell combat retains the same hit, cooldown and stationary release.
$smallPlayer = New-BodyUnit 1 1 2 4
$smallEnemy = New-BodyUnit 2 2 3 4
$smallResult = Invoke-BodyStep (New-BodyBattle $smallPlayer $smallEnemy)
Assert-Body ($smallResult.Event.DamageEvents.Count -eq 1 -and $smallEnemy.Hp -eq 983 -and $smallResult.Event.Movements.Count -eq 0) 'Single-cell melee behavior changed'

# Configured range order and shortest approach both compare body cells.
$priorityPlayer = New-BodyUnit 1 1 2 4
$nearCentre = New-BodyUnit 2 2 4 4
$farCentre = New-BodyBoss; $farCentre.Id = 3
$prioritySkill = New-BodySkill
$prioritySkill.Range.Clear()
$prioritySkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$prioritySkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,0))
$targets = [Zjcs.Combat.Official.FightBattleUnitState[]]@($priorityPlayer,$nearCentre,$farCentre)
$chosen = [Zjcs.Combat.Official.FightBattleTargeting]::Select($priorityPlayer,$targets,1,0,$false,$false,$prioritySkill)
Assert-Body ($chosen.Count -eq 1 -and $chosen[0].Id -eq 3) 'Range order compared target centres instead of body edges'
$prioritySkill.AiPriorityTypes.Add(0)
$prioritySkill.Range.RemoveAt(1)
$chosen = [Zjcs.Combat.Official.FightBattleTargeting]::Select($priorityPlayer,$targets,1,0,$false,$false,$prioritySkill)
Assert-Body ($chosen.Count -eq 1 -and $chosen[0].Id -eq 3) 'Shortest-approach priority ignored the target body'

Write-Output 'Body range checks passed: melee edge hit, one release/CD/report, cold footprint restore, legal approach, alternate edge, wall, caster-centre origin, single-cell regression and target priority.'

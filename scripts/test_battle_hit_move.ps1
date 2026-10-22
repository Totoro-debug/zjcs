$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Move($condition, $message) { if (-not $condition) { throw $message } }
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

$officialHit = $catalog.CreateSkill(12100, 1, 1, 1, 1, 0, $null).HitGraphEntities | Where-Object { $_.EntityId -eq 12102 } | Select-Object -First 1
Assert-Move ($null -ne $officialHit -and $null -ne $officialHit.HitMove) 'Official hit displacement was not mapped'
Assert-Move ($officialHit.HitMove.Distance -eq 1 -and $officialHit.HitMove.Direction -eq 1 -and -not $officialHit.HitMove.TowardsTargetPos) 'Official hit displacement fields mismatch'

function New-Unit($id, $side, $x, $y, $hp) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $id; $u.Side = $side; $u.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $u.X = $x; $u.Y = $y; $u.Hp = $hp; $u.MaxHp = $hp
    $u.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $u.Turn.Id = $id; $u.Turn.Hp = $hp; $u.Turn.Speed = 100 - $id
    return $u
}

function New-MoveSkill($rangeX, $distance, $towards, $conditions) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new($rangeX, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 8201; $hit.DamageId = 8202; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $hit.HitMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
    $hit.HitMove.Distance = $distance; $hit.HitMove.Direction = 1; $hit.HitMove.TowardsTargetPos = $towards
    $hit.HitMove.HasConditions = $conditions
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-MoveBattle($skill, $player, $enemy) {
    $skill.Slot = 0; $skill.EntityId = 8200; $skill.ItemId = 8200
    $player.Skills.Clear(); $player.Skills.Add($skill)
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 21
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width = 10; $cp.Grid.Height = 10; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $f = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    return [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $f, [Zjcs.Combat.Official.FightDamageSettlement]::new()).Step(0, 1)
}

$player = New-Unit 1 1 0 0 1000
$enemy = New-Unit 2 2 1 0 5000
$push = Invoke-MoveBattle (New-MoveSkill 1 1 $false $false) $player $enemy
Assert-Move ($enemy.X -eq 2 -and $enemy.Y -eq 0) 'Knockback did not push the target away'
Assert-Move ($push.Event.Movements.Count -eq 1 -and $push.Event.Movements[0].UnitId -eq 2 -and $push.Event.Movements[0].Path.Count -eq 2) 'Knockback movement event mismatch'

$pullPlayer = New-Unit 1 1 0 0 1000
$pullEnemy = New-Unit 2 2 2 0 5000
$pull = Invoke-MoveBattle (New-MoveSkill 2 1 $true $false) $pullPlayer $pullEnemy
Assert-Move ($pullEnemy.X -eq 1) 'Towards-target displacement did not pull the target closer'

$blockedPlayer = New-Unit 1 1 0 0 1000
$blockedEnemy = New-Unit 2 2 9 0 5000
$blocked = Invoke-MoveBattle (New-MoveSkill 9 1 $false $false) $blockedPlayer $blockedEnemy
Assert-Move ($blockedEnemy.X -eq 9 -and $blocked.Event.Movements.Count -eq 0) 'Displacement moved the target out of the grid'

$conditionPlayer = New-Unit 1 1 0 0 1000
$conditionEnemy = New-Unit 2 2 1 0 5000
$conditional = Invoke-MoveBattle (New-MoveSkill 1 1 $false $true) $conditionPlayer $conditionEnemy
Assert-Move ($conditionEnemy.X -eq 1 -and $conditional.Event.Movements.Count -eq 0) 'Conditional displacement was applied'

Write-Output 'Battle hit displacement checks passed: official mapping, knockback, pull, blocked destination and move conditions.'

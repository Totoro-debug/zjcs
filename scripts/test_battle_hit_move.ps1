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

$officialConditionalMove = $catalog.CreateSkill(13500, 0, 13500, 1, 1, 0, $null).SourceMoves |
    Select-Object -First 1
$officialConditionMapped = $officialConditionalMove.Conditions.Count -eq 1 `
    -and $officialConditionalMove.Conditions[0].Type -eq 'SkillTargetDistanceMoveCondition' `
    -and $officialConditionalMove.Conditions[0].Distance -eq 3
Assert-Move $officialConditionMapped 'Official skill 13500 move condition was not mapped'

$effectSchema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$effectPropertyNames = @('BaseEffectRate', 'EffectRate', 'BaseEffectDodge', 'EffectDodge',
    'EffectRateValue', 'EffectDodgeValue')
foreach ($name in $effectPropertyNames) {
    $id = $catalog.PropertyType($name)
    Assert-Move ($id -gt 0) "Official move-rate property is missing: $name"
    $effectSchema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name = $name; Id = $id })
    if ($name -eq 'EffectRateValue' -or $name -eq 'EffectDodgeValue')
        { $effectSchema.FloatPropertyIds.Add($id) }
}
$moveFormula = [Zjcs.Combat.Official.FightDamageFormula]::new($effectSchema,
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())

function Set-MoveProperty($properties, $name, $value) {
    $id = $catalog.PropertyType($name)
    foreach ($property in $properties) {
        if ($property.Id -eq $id) { $property.Value = $value; return }
    }
    $properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $id; Value = $value })
}

$officialHit = $catalog.CreateSkill(12100, 1, 1, 1, 1, 0, $null).HitGraphEntities | Where-Object { $_.EntityId -eq 12102 } | Select-Object -First 1
Assert-Move ($null -ne $officialHit -and $null -ne $officialHit.HitMove) 'Official hit displacement was not mapped'
Assert-Move ($officialHit.HitMove.Distance -eq 1 -and $officialHit.HitMove.Direction -eq 1 -and -not $officialHit.HitMove.TowardsTargetPos) 'Official hit displacement fields mismatch'
$pathSideSkill = $catalog.CreateSkill(56230, 0, 56230, 1, 1, 0, $null)
$pathSideHit = $pathSideSkill.HitGraphEntities | Where-Object { $_.EntityId -eq 56231 } | Select-Object -First 1
Assert-Move ($null -ne $pathSideHit -and $pathSideHit.HitMove.MoveBehaviourId -eq 56234 -and $pathSideHit.HitMove.Direction -eq 2) 'Official path-side displacement was not mapped'
Assert-Move ($null -ne $pathSideHit.HitMove -and $pathSideHit.HitMove.BasePercent -ge 0 -and $pathSideHit.HitMove.AffectedByProp) 'Official path-side movement probability fields were not mapped'

function New-Unit($id, $side, $x, $y, $hp) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $id; $u.Side = $side; $u.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $u.X = $x; $u.Y = $y; $u.Hp = $hp; $u.MaxHp = $hp
    $u.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $u.Turn.Id = $id; $u.Turn.Hp = $hp; $u.Turn.Speed = 100 - $id
    return $u
}

function New-MoveSkill($rangeX, $distance, $towards, $conditions, $direction = 1, $rangeY = 0,
    $basePercent = 1.0, $affectedByProp = $false, $conditionDistance = -1) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new($rangeX, $rangeY))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 8201; $hit.DamageId = 8202; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $hit.HitMove = [Zjcs.Combat.Official.FightBattleSkillMove]::new()
    $hit.HitMove.Distance = $distance; $hit.HitMove.Direction = $direction; $hit.HitMove.TowardsTargetPos = $towards
    $hit.HitMove.BasePercent = $basePercent; $hit.HitMove.AffectedByProp = $affectedByProp
    $hit.HitMove.HasConditions = $conditions
    if ($conditionDistance -ge 0) {
        $condition = [Zjcs.Combat.Official.FightBattleSkillMoveCondition]::new()
        $condition.Type = 'SkillTargetDistanceMoveCondition'; $condition.Distance = $conditionDistance
        $hit.HitMove.Conditions.Add($condition)
    }
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
    return [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $moveFormula, [Zjcs.Combat.Official.FightDamageSettlement]::new()).Step(0, 1)
}

$player = New-Unit 1 1 0 0 1000
$enemy = New-Unit 2 2 1 0 5000
$push = Invoke-MoveBattle (New-MoveSkill 1 1 $false $false) $player $enemy
Assert-Move ($enemy.X -eq 2 -and $enemy.Y -eq 0) 'Knockback did not push the target away'
Assert-Move ($push.Event.Movements.Count -eq 1 -and $push.Event.Movements[0].UnitId -eq 2 -and $push.Event.Movements[0].Path.Count -eq 2) 'Knockback movement event mismatch'

$zeroChancePlayer = New-Unit 1 1 0 0 1000
$zeroChanceEnemy = New-Unit 2 2 1 0 5000
$zeroChance = Invoke-MoveBattle (New-MoveSkill 1 1 $false $false 1 0 0.0) $zeroChancePlayer $zeroChanceEnemy
Assert-Move ($zeroChanceEnemy.X -eq 1 -and $zeroChance.Event.Movements.Count -eq 0) 'Zero-probability displacement was applied'

$propertyChancePlayer = New-Unit 1 1 0 0 1000
$propertyChanceEnemy = New-Unit 2 2 1 0 5000
Set-MoveProperty $propertyChancePlayer.Properties 'BaseEffectRate' 100
Set-MoveProperty $propertyChancePlayer.Properties 'EffectRateValue' 20000
Set-MoveProperty $propertyChanceEnemy.Properties 'BaseEffectDodge' 100
Set-MoveProperty $propertyChanceEnemy.Properties 'EffectDodge' 0
$propertyChance = Invoke-MoveBattle (New-MoveSkill 1 1 $false $false 1 0 0.0 $true) $propertyChancePlayer $propertyChanceEnemy
Assert-Move ($propertyChanceEnemy.X -eq 2 -and $propertyChance.Event.Movements.Count -eq 1) 'Effect-rate properties did not raise the displacement chance'

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

$distanceConditionPlayer = New-Unit 1 1 0 0 1000
$distanceConditionEnemy = New-Unit 2 2 3 0 5000
$distanceCondition = Invoke-MoveBattle (New-MoveSkill 3 1 $false $true 1 0 1.0 $false 3) `
    $distanceConditionPlayer $distanceConditionEnemy
Assert-Move ($distanceConditionEnemy.X -eq 4 -and $distanceCondition.Event.Movements.Count -eq 1) `
    'Hit displacement did not satisfy SkillTargetDistanceMoveCondition at its distance boundary'

$overDistanceConditionPlayer = New-Unit 1 1 0 0 1000
$overDistanceConditionEnemy = New-Unit 2 2 4 0 5000
$overDistanceCondition = Invoke-MoveBattle (New-MoveSkill 4 1 $false $true 1 0 1.0 $false 3) `
    $overDistanceConditionPlayer $overDistanceConditionEnemy
Assert-Move ($overDistanceConditionEnemy.X -eq 4 -and $overDistanceCondition.Event.Movements.Count -eq 0) `
    'Hit displacement ignored SkillTargetDistanceMoveCondition beyond its distance threshold'

$sidePlayer = New-Unit 1 1 0 4 1000
$sideEnemy = New-Unit 2 2 1 4 5000
$side = Invoke-MoveBattle (New-MoveSkill 1 1 $true $false 2) $sidePlayer $sideEnemy
Assert-Move ($sideEnemy.X -eq 1 -and $sideEnemy.Y -eq 5) 'Path-side displacement did not choose the first positive-Y side'

$sideFallbackPlayer = New-Unit 1 1 0 9 1000
$sideFallbackEnemy = New-Unit 2 2 1 9 5000
$sideFallback = Invoke-MoveBattle (New-MoveSkill 1 1 $true $false 2) $sideFallbackPlayer $sideFallbackEnemy
Assert-Move ($sideFallbackEnemy.X -eq 1 -and $sideFallbackEnemy.Y -eq 8) 'Path-side displacement did not fall back to the opposite side when the first side was out of bounds'

$verticalPlayer = New-Unit 1 1 4 4 1000
$verticalEnemy = New-Unit 2 2 4 5 5000
$vertical = Invoke-MoveBattle (New-MoveSkill 0 1 $true $false 2 1) $verticalPlayer $verticalEnemy
Assert-Move ($verticalEnemy.X -eq 5 -and $verticalEnemy.Y -eq 5) 'Vertical path-side displacement did not prefer positive X'

$diagonalPlayer = New-Unit 1 1 2 2 1000
$diagonalEnemy = New-Unit 2 2 3 3 5000
$diagonal = Invoke-MoveBattle (New-MoveSkill 1 1 $true $false 2 1) $diagonalPlayer $diagonalEnemy
Assert-Move ($diagonalEnemy.X -eq 3 -and $diagonalEnemy.Y -eq 4) 'Diagonal path-side displacement did not use the deterministic horizontal-axis tie break'

Write-Output 'Battle hit displacement checks passed: official mapping, probability and EffectRate, knockback, pull, blocked destination, move conditions and deterministic path-side movement.'

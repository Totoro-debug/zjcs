. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Hemo($condition, $message) { if (-not $condition) { throw $message } }
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

$officialHit = $catalog.CreateSkill(13420, 1, 1, 1, 1, 0, $null).HitGraphEntities |
    Where-Object { $_.EntityId -eq 13421 } | Select-Object -First 1
Assert-Hemo ($null -ne $officialHit -and $officialHit.HemophagiaProperty -eq 213) 'Official lifesteal property was not mapped'

function New-Unit($id, $side, $x, $y, $hp, $maxHp) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $id; $u.Side = $side; $u.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $u.X = $x; $u.Y = $y; $u.Hp = $hp; $u.MaxHp = $maxHp
    $u.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $u.Turn.Id = $id; $u.Turn.Hp = $hp; $u.Turn.Speed = 100 - $id
    return $u
}

function New-HemoSkill($damage, $actionType) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 8401; $hit.DamageId = 8402; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = $damage; $hit.TargetMode = 0
    $hit.ActionType = $actionType; $hit.HemophagiaProperty = 213
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-HemoBattle($skill, $player, $enemy) {
    $player.Skills.Clear(); $player.Skills.Add($skill)
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 41
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width = 10; $cp.Grid.Height = 10; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $f = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    return [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $f, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog).Step(0, 1)
}

$player = New-Unit 1 1 0 0 500 1000
$player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 213; Value = 2000 })
$enemy = New-Unit 2 2 1 0 5000 5000
$result = Invoke-HemoBattle (New-HemoSkill 100 4) $player $enemy
Assert-Hemo ($player.Hp -eq 520) "Lifesteal healed to $($player.Hp) instead of 520"
$cureEvents = @($result.Event.DamageEvents | Where-Object { $_.IsCure })
Assert-Hemo ($cureEvents.Count -eq 1 -and $cureEvents[0].Damage -eq 20 -and $cureEvents[0].TargetId -eq 1) 'Lifesteal cure event mismatch'
Assert-Hemo ($result.Event.DamageEvents.Count -eq 2) 'Lifesteal did not keep the damage event'

# Healing is capped at the maximum HP.
$cappedPlayer = New-Unit 1 1 0 0 990 1000
$cappedPlayer.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 213; Value = 2000 })
$cappedEnemy = New-Unit 2 2 1 0 5000 5000
$capped = Invoke-HemoBattle (New-HemoSkill 1000 4) $cappedPlayer $cappedEnemy
Assert-Hemo ($cappedPlayer.Hp -eq 1000) 'Lifesteal exceeded the maximum HP'
$cappedCure = @($capped.Event.DamageEvents | Where-Object { $_.IsCure })
Assert-Hemo ($cappedCure.Count -eq 1 -and $cappedCure[0].Damage -eq 10) 'Capped lifesteal cure event mismatch'

# Without the property nothing is healed.
$plainPlayer = New-Unit 1 1 0 0 500 1000
$plainEnemy = New-Unit 2 2 1 0 5000 5000
$plain = Invoke-HemoBattle (New-HemoSkill 100 4) $plainPlayer $plainEnemy
Assert-Hemo ($plainPlayer.Hp -eq 500 -and @($plain.Event.DamageEvents | Where-Object { $_.IsCure }).Count -eq 0) 'Lifesteal healed without its property'

# Cure hits do not lifesteal.
$curePlayer = New-Unit 1 1 0 0 500 1000
$curePlayer.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 213; Value = 2000 })
$cureEnemy = New-Unit 2 2 1 0 5000 5000
$cure = Invoke-HemoBattle (New-HemoSkill 100 5) $curePlayer $cureEnemy
Assert-Hemo ($curePlayer.Hp -eq 500 -and @($cure.Event.DamageEvents | Where-Object { $_.IsCure }).Count -eq 1) 'Lifesteal applied to a cure hit'

Write-Output 'Battle lifesteal checks passed: official property mapping, percent heal, cap, missing property and cure hits.'

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Disperse($condition, $message) {
    if (-not $condition) { throw $message }
}

function Read-Text($path) {
    return [System.IO.File]::ReadAllText((Join-Path $root $path))
}

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

$effectSchema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$effectPropertyNames = @('BaseEffectRate', 'EffectRate', 'BaseEffectDodge', 'EffectDodge',
    'EffectRateValue', 'EffectDodgeValue')
foreach ($name in $effectPropertyNames) {
    $id = $catalog.PropertyType($name)
    Assert-Disperse ($id -gt 0) "Official disperse-rate property is missing: $name"
    $effectSchema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name = $name; Id = $id })
    if ($name -eq 'EffectRateValue' -or $name -eq 'EffectDodgeValue')
        { $effectSchema.FloatPropertyIds.Add($id) }
}
$disperseFormula = [Zjcs.Combat.Official.FightDamageFormula]::new($effectSchema,
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())

# Official mapping: skill 12940 -> hit 12941 -> damage 12942 disperses types 2/3, one random status.
$officialHit = $catalog.CreateSkill(12940, 1, 1, 1, 1, 0, $null).HitGraphEntities |
    Where-Object { $_.EntityId -eq 12941 } | Select-Object -First 1
Assert-Disperse ($null -ne $officialHit) 'Official disperse hit was not mapped'
Assert-Disperse ($officialHit.DisperseStatusTypes.Count -eq 2 -and $officialHit.DisperseStatusTypes.Contains(2) -and $officialHit.DisperseStatusTypes.Contains(3)) 'Official disperse status types mismatch'
Assert-Disperse ($officialHit.DisperseCount -eq 1 -and $officialHit.DisperseRandom -and [Math]::Abs($officialHit.DisperseChance - 1) -lt 0.0001) 'Official disperse count/chance mismatch'

function New-Status($classId, $statusType, $dispersible) {
    $status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $status.InstanceId = $classId
    $status.ClassId = $classId
    $status.StatusType = $statusType
    $status.CanDispersed = $dispersible
    $status.DurationRound = -1
    $status.CurDurationRound = -1
    $status.StartRound = -1
    $status.OwnerId = 2
    $status.CreatorId = 2
    return $status
}

function New-DisperseSkill($chance, $types, $count, $random, $affectedByProp = $false) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 8101; $hit.DamageId = 8102; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $hit.DisperseChance = $chance; $hit.DisperseCount = $count; $hit.DisperseRandom = $random
    $hit.DisperseAffectedByProp = $affectedByProp
    foreach ($type in $types) { $hit.DisperseStatusTypes.Add($type) }
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-DisperseBattle($skill, $player, $enemy) {
    $skill.Slot = 0; $skill.EntityId = 8100; $skill.ItemId = 8100
    $player.Skills.Clear()
    $player.Skills.Add($skill)
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 13
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $checkpoint.Grid.Width = 10; $checkpoint.Grid.Height = 10; $checkpoint.Grid.DefaultGroundType = 2
    $checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2)
    $checkpoint.Waves.Add($wave)
    return [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $disperseFormula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new()).Step(0, 1)
}

function Set-DisperseProperty($properties, $name, $value) {
    $id = $catalog.PropertyType($name)
    foreach ($property in $properties) {
        if ($property.Id -eq $id) { $property.Value = $value; return }
    }
    $properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $id; Value = $value })
}

function New-Player {
    $player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $player.Id = 1; $player.Side = 1; $player.CharacterType = 1; $player.Hp = 1000; $player.MaxHp = 1000
    $player.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $player.Turn.Id = 1; $player.Turn.Hp = 1000; $player.Turn.Speed = 100
    return $player
}

function New-Enemy {
    $enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2; $enemy.X = 1; $enemy.Y = 0; $enemy.Hp = 5000; $enemy.MaxHp = 5000
    $enemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $enemy.Turn.Id = 2; $enemy.Turn.Hp = 5000; $enemy.Turn.Speed = 1
    return $enemy
}

# Count 0 disperses every matching dispersible status; protected and other types stay.
$player = New-Player; $enemy = New-Enemy
$enemy.Statuses.Add((New-Status 9001 2 $true))
$enemy.Statuses.Add((New-Status 9002 3 $true))
$enemy.Statuses.Add((New-Status 9003 2 $false))
$enemy.Statuses.Add((New-Status 9004 1 $true))
$removed = Invoke-DisperseBattle (New-DisperseSkill 1.0 @(2,3) 0 $false) $player $enemy
Assert-Disperse ($removed.Event.DispersedUnitIds.Count -eq 1 -and $removed.Event.DispersedUnitIds[0] -eq 2) 'One successful disperse operation must emit one target notification, even when two statuses are removed'
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($removed.Checkpoint))
Assert-Disperse ($restored.Receipts[0].Event.DispersedUnitIds.Count -eq 1 -and $restored.Receipts[0].Event.DispersedUnitIds[0] -eq 2) 'Disperse notification did not survive checkpoint persistence'
Assert-Disperse (($enemy.Statuses | Where-Object { $_.ClassId -eq 9001 }).Count -eq 0) 'Dispersible type-2 status survived'
Assert-Disperse (($enemy.Statuses | Where-Object { $_.ClassId -eq 9002 }).Count -eq 0) 'Dispersible type-3 status survived'
Assert-Disperse (($enemy.Statuses | Where-Object { $_.ClassId -eq 9003 }).Count -eq 1) 'Protected status was dispersed'
Assert-Disperse (($enemy.Statuses | Where-Object { $_.ClassId -eq 9004 }).Count -eq 1) 'Unrelated status type was dispersed'

# Count 1 removes exactly one status.
$player = New-Player; $enemy = New-Enemy
$enemy.Statuses.Add((New-Status 9101 2 $true))
$enemy.Statuses.Add((New-Status 9102 2 $true))
[void](Invoke-DisperseBattle (New-DisperseSkill 1.0 @(2) 1 $true) $player $enemy)
Assert-Disperse ($enemy.Statuses.Count -eq 1) 'Count-limited disperse removed the wrong number of statuses'

# Zero chance and non-matching types remove nothing.
$player = New-Player; $enemy = New-Enemy
$enemy.Statuses.Add((New-Status 9201 2 $true))
$failed = Invoke-DisperseBattle (New-DisperseSkill 0.0 @(2) 1 $false) $player $enemy
Assert-Disperse ($failed.Event.DispersedUnitIds.Count -eq 0) 'Failed probability emitted a disperse notification'
Assert-Disperse ($enemy.Statuses.Count -eq 1) 'Zero-chance disperse removed a status'
$unmatched = Invoke-DisperseBattle (New-DisperseSkill 1.0 @(3) 1 $false) $player $enemy
Assert-Disperse ($unmatched.Event.DispersedUnitIds.Count -eq 0) 'No removable status emitted a disperse notification'
Assert-Disperse ($enemy.Statuses.Count -eq 1) 'Disperse removed a status of another type'

# EffectRate can raise a configured zero chance; missing rate inputs fail closed.
$player = New-Player; $enemy = New-Enemy
$enemy.Statuses.Add((New-Status 9301 2 $true))
Set-DisperseProperty $player.Properties 'BaseEffectRate' 100
Set-DisperseProperty $player.Properties 'EffectRateValue' 20000
Set-DisperseProperty $enemy.Properties 'BaseEffectDodge' 100
Set-DisperseProperty $enemy.Properties 'EffectDodge' 0
[void](Invoke-DisperseBattle (New-DisperseSkill 0.0 @(2) 1 $false $true) $player $enemy)
Assert-Disperse ($enemy.Statuses.Count -eq 0) 'EffectRate properties did not raise disperse chance'

$player = New-Player; $enemy = New-Enemy
$enemy.Statuses.Add((New-Status 9302 2 $true))
[void](Invoke-DisperseBattle (New-DisperseSkill 1.0 @(2) 1 $false $true) $player $enemy)
Assert-Disperse ($enemy.Statuses.Count -eq 1) 'Missing EffectRate inputs did not fail closed'

Write-Output 'Battle status dispel checks passed: official mapping, type filter, dispersible flag, count limit, random pick, chance and EffectRate.'

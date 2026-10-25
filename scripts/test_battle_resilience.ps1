$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Resilience($condition, $message) {
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

function Add-Property($unit, $id, $value) {
    $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
        Id = $id; Value = $value
    })
}

$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')

$noResilienceProps = $catalog.CalculateMonsterProps(3289, 0, 1, 1)
$resilienceProps = $catalog.CalculateMonsterProps(3289, 6, 1, 1)
Assert-Resilience ((-not $noResilienceProps.ContainsKey(6)) -or $noResilienceProps[6] -eq 0) `
    'Monster 3289 group 0 unexpectedly has resilience.'
Assert-Resilience ($resilienceProps.ContainsKey(6) -and $resilienceProps[6] -gt 0) `
    'Monster 3289 group 6 did not calculate its official resilience property.'
$resilienceInfo = $catalog.MonsterResilienceInfo(3289, 6)
Assert-Resilience ($resilienceInfo.DisappearSkillId -eq 10130 `
    -and $resilienceInfo.DisappearStatusIds.Contains(24278) `
    -and $resilienceInfo.ResetRound -eq 2) 'Monster 3289 resilience response config mismatch.'

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
foreach ($entry in @(
    @{ Name = 'Attack'; Id = 1 },
    @{ Name = 'MaxHp'; Id = 3 },
    @{ Name = 'Resilience'; Id = 6 },
    @{ Name = 'BreakResiliencePercent'; Id = 73 },
    @{ Name = 'BreakResilience'; Id = 113 }
)) {
    $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{
        Name = $entry.Name; Id = $entry.Id
    })
}
$schema.FloatPropertyIds.Add(73)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    $schema, [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1
$player.Hp = 1000; $player.MaxHp = 1000; $player.MoveDist = 0
$player.Turn.Id = 1; $player.Turn.Hp = 1000; $player.Turn.Speed = 100
Add-Property $player 1 100
Add-Property $player 3 1000
Add-Property $player 73 0
Add-Property $player 113 10
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
    Slot = 0; LimitedTimes = -1
})
$attackSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$attackSkill.Slot = 0; $attackSkill.EntityId = 900; $attackSkill.ItemId = 901
$attackSkill.TargetType = 1
$attackSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = 902; $hit.DamageId = 903; $hit.DamageType = 8
$hit.ActionType = 4; $hit.TargetMode = 0; $hit.DamageParam = 10000
$hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.BreakResilience = $true
$attackSkill.Hits.Add($hit)
$player.Skills.Add($attackSkill)

$monster = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$monster.Id = 2; $monster.Side = 2; $monster.CharacterType = 2
$monster.MonsterId = 3289; $monster.MonsterGroupNo = 6; $monster.Level = 1; $monster.SubRank = 1
$monster.Hp = 1000; $monster.MaxHp = 1000; $monster.MoveDist = 0
$monster.Turn.Id = 2; $monster.Turn.Hp = 1000; $monster.Turn.Speed = 1
Add-Property $monster 1 10
Add-Property $monster 3 1000
Add-Property $monster 6 10

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 20; $checkpoint.RandomSeed = 7
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 8; $checkpoint.Grid.Height = 8; $checkpoint.Grid.DefaultGroundType = 2
$player.X = 0; $player.Y = 0; $monster.X = 1; $monster.Y = 0
$checkpoint.Units.Add($player); $checkpoint.Units.Add($monster)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add(2); $checkpoint.Waves.Add($wave)
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
Assert-Resilience ($monster.MaxResilience -eq 10 -and $monster.Resilience -eq 10) `
    'Executor did not initialize resilience from property 6.'

$breakStep = $null
$breakCounter = 0
for ($counter = 1; $counter -le 30; $counter++) {
    $step = $executor.Step(0, $counter)
    if ($monster.Resilience -eq 0) { $breakStep = $step; $breakCounter = $counter; break }
}
Assert-Resilience ($null -ne $breakStep) 'Configured break hit did not empty the resilience bar.'
Assert-Resilience (($monster.Statuses | Where-Object { $_.ClassId -eq 24278 }).Count -eq 1) `
    'Resilience break did not apply status 24278.'
Assert-Resilience (($breakStep.Event.SkillCasts | Where-Object {
    $_.SkillEntityId -eq 10130 -and $_.ActorId -eq 2
}).Count -eq 1) 'Resilience break did not cast the configured self skill.'
$cappedDamageEvents = @($breakStep.Event.DamageEvents | Where-Object {
    $_.SkillEntityId -eq 10130 -and $_.TargetId -eq 2 -and $_.AbsoluteDamage -eq -50
})
Assert-Resilience ($cappedDamageEvents.Count -gt 0) `
    ("Triggered self-skill damage did not use the official max-HP / breaker-attack cap: " `
        + (ConvertTo-Json @($breakStep.Event.DamageEvents | Where-Object {
            $_.SkillEntityId -eq 10130
        } | Select-Object SkillEntityId, HitEntityId, TargetId, Damage, AbsoluteDamage) -Compress))

$currentCounter = $breakCounter
for ($counter = $currentCounter + 1; $counter -le 100 -and $monster.SkillState.Round -lt 2; $counter++) {
    [void]$executor.Step(0, $counter)
}
Assert-Resilience ($monster.SkillState.Round -ge 2) 'Monster did not complete two personal rounds.'
Assert-Resilience ($monster.Resilience -eq $monster.MaxResilience -and $monster.BrokenRound -eq -1) `
    'Resilience did not restore after two personal rounds.'

Write-Output 'Battle resilience checks passed: pinned group config, property-6 initialization, break hit, status and skill trigger, damage cap, and personal-round reset.'

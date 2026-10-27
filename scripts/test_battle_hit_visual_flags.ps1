. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Flag($condition, $message) {
    if (-not $condition) { throw $message }
}

$assetRoot = Join-Path $root 'unity/Assets/Official/Combat'
$skillJson = Get-Content (Join-Path $assetRoot 'skill-rules.json') -Raw
$battleJson = Get-Content (Join-Path $assetRoot 'battle-rules.json') -Raw
$skillManifest = Get-Content (Join-Path $assetRoot 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$battleManifest = Get-Content (Join-Path $assetRoot 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, $skillManifest.output_sha256, $skillManifest.manifest,
    $battleJson, $battleManifest.output_sha256, $battleManifest.manifest)

$trueSkill = $catalog.CreateSkill(12310, 0, 12310, 1, 1, 0, $null)
$trueHit = $trueSkill.HitGraphEntities | Where-Object EntityId -eq 12311 | Select-Object -First 1
Assert-Flag ($null -ne $trueHit -and $trueHit.TrueDamage) 'Official hit 12311 lost IsTrueDamage.'
$breakSkill = $catalog.CreateSkill(10130, 0, 10130, 1, 1, 0, $null)
$breakHit = $breakSkill.HitGraphEntities | Where-Object EntityId -eq 10131 | Select-Object -First 1
Assert-Flag ($null -ne $breakHit -and $breakHit.BreakResilience) 'Official hit 10131 lost IsBreakResilience.'

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1
$player.Hp = 100; $player.MaxHp = 100; $player.Turn.Id = 1; $player.Turn.Hp = 100; $player.Turn.Speed = 100
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$skill.Slot = 0; $skill.EntityId = 6001; $skill.ItemId = 6001; $skill.TargetType = 1
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId = 6010; $hit.DamageId = 6011; $hit.ActionType = 4; $hit.DamageType = 8
$hit.HasCustomDamage = $true; $hit.CustomDamage = 40
$hit.IgnoreShield = $true; $hit.TrueDamage = $true; $hit.BreakResilience = $true
$skill.Hits.Add($hit); $player.Skills.Add($skill)

$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2
$enemy.X = 1; $enemy.Hp = 100; $enemy.MaxHp = 100
$enemy.Turn.Id = 2; $enemy.Turn.Hp = 100; $enemy.Turn.Speed = 99
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.RandomSeed = 7
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 4; $checkpoint.Grid.Height = 2; $checkpoint.Grid.DefaultGroundType = 2
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.UnitIds.Add($enemy.Id); $checkpoint.Waves.Add($wave)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$result = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new()).Step(0, 1)
$damage = $result.Event.DamageEvents | Select-Object -First 1
Assert-Flag ($null -ne $damage -and $damage.IgnoreShield -and $damage.TrueDamage -and $damage.BreakResilience) 'Official hit flags did not propagate into the resolved damage event.'
$saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint)
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$replayedDamage = $restored.Receipts[0].Event.DamageEvents[0]
Assert-Flag ($replayedDamage.IgnoreShield -and $replayedDamage.TrueDamage -and $replayedDamage.BreakResilience) 'Checkpoint replay dropped official hit flags.'

Write-Output 'Battle hit flag checks passed: official 12311/10131 mapping, event propagation and checkpoint replay.'

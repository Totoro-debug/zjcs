$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-RoundStart($condition, $message) { if (-not $condition) { throw $message } }
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
$status = $catalog.CreateStatus(25256, 1, 1, 1)
Assert-RoundStart ($status.HasRoundStartTrigger -and $status.RoundStartRulesSupported -and $status.RoundStartListenTarget -eq 2 -and $status.RoundStartSkills.Count -eq 1 -and $status.RoundStartSkills[0].EntityId -eq 12980) 'Official status 25256 round-start configuration did not map to skill 12980'

$actor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$actor.Id = 1; $actor.Side = 1; $actor.CharacterType = 1; $actor.Hp = 1000; $actor.MaxHp = 1000; $actor.Level = 1
$actor.Turn.Id = 1; $actor.Turn.Hp = 1000; $actor.Turn.Speed = 100
$actor.Statuses.Add($status)

$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2; $enemy.Hp = 1000; $enemy.MaxHp = 1000; $enemy.Level = 1
$enemy.X = 5; $enemy.Y = 5; $enemy.Turn.Id = 2; $enemy.Turn.Hp = 1000; $enemy.Turn.Speed = 1

$summon = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$summon.Id = 3; $summon.Side = 1; $summon.CharacterType = 2; $summon.MasterId = 1
$summon.Hp = 1000; $summon.MaxHp = 1000; $summon.Level = 1; $summon.X = 1
$summon.Turn.Id = 3; $summon.Turn.MasterId = 1; $summon.Turn.Hp = 1000; $summon.Turn.Speed = 1

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 5
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 8; $checkpoint.Grid.Height = 8; $checkpoint.Grid.DefaultGroundType = 2
$checkpoint.Units.Add($actor); $checkpoint.Units.Add($enemy); $checkpoint.Units.Add($summon)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$wave.Index = 0; $wave.Spawned = $true
$wave.UnitIds.Add(1); $wave.UnitIds.Add(2); $wave.UnitIds.Add(3)
$checkpoint.Waves.Add($wave)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(),
    [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
$result = $runner.Step(0, 1)
$cast = @($result.Event.SkillCasts | Where-Object { $_.ActorId -eq 1 -and $_.SkillEntityId -eq 12980 -and $_.Slot -eq -1 })
Assert-RoundStart ($cast.Count -eq 1 -and $cast[0].TargetIds -contains 3) 'Status 25256 did not execute skill 12980 against the applicator summon at round start'
Assert-RoundStart ($status.RoundStartTriggeredCount -eq 1) 'Status 25256 round-start trigger count was not advanced'

$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint))
$savedStatus = $loaded.Units[0].Statuses | Where-Object { $_.ClassId -eq 25256 } | Select-Object -First 1
Assert-RoundStart ($null -ne $savedStatus -and $savedStatus.RoundStartTriggeredCount -eq 1 -and $savedStatus.RoundStartSkills[0].EntityId -eq 12980) 'Round-start trigger state did not survive checkpoint persistence'

Write-Output 'Battle status round-start checks passed: fixed 25256 mapping, applicator timing, triggered skill targeting and checkpoint persistence.'

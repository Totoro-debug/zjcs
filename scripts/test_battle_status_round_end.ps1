. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-RoundEnd($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
function Get-Sha($path) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $path))
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}
function New-Unit([int]$id, [int]$side, [int]$speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($id -eq 1) { 1 } else { 2 }
    $unit.Hp = 1000; $unit.MaxHp = 1000; $unit.Level = 1; $unit.SubRank = 1
    $unit.Turn.Id = $id; $unit.Turn.Hp = 1000; $unit.Turn.Speed = $speed
    return $unit
}

$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')
$status = $catalog.CreateStatus(25177, 3, 1, 1)
$hasStatusConfig = $status.HasRoundEndTrigger -and $status.RoundEndRulesSupported -and
    $status.RoundEndListenTarget -eq 4 -and $status.RoundEndSkills.Count -eq 1 -and
    $status.RoundEndSkills[0].EntityId -eq 50180 -and $status.RoundEndSkills[0].Source -eq 2 -and
    $status.RoundEndSkills[0].Target -eq 5 -and $status.RoundEndSkills[0].TargetPos -eq 1
Assert-RoundEnd $hasStatusConfig 'Official 25177 round-end configuration did not map to its configured source, target and position'

$player = New-Unit 1 1 100
$player.X = 1; $player.Y = 2
$summon = New-Unit 3 1 1
$summon.MasterId = 1; $summon.Turn.MasterId = 1; $summon.X = 1; $summon.Y = 1
$summon.Statuses.Add($status)
$enemy = New-Unit 2 2 1
$enemy.X = 6; $enemy.Y = 6
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 19
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width = 8; $checkpoint.Grid.Height = 8; $checkpoint.Grid.DefaultGroundType = 2
$checkpoint.Units.Add($player); $checkpoint.Units.Add($summon); $checkpoint.Units.Add($enemy)
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
$cast = @($result.Event.SkillCasts | Where-Object {
    $_.ActorId -eq 3 -and $_.SkillEntityId -eq 50180 -and $_.TargetIds -contains 1
})
$triggerExecuted = $result.Event.ActorId -eq 1 -and $cast.Count -eq 1 -and
    $status.RoundEndTriggeredCount -eq 1
Assert-RoundEnd $triggerExecuted 'Status 25177 did not cast skill 50180 from its applicator onto the round-end listener'

$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint))
$savedStatus = $loaded.Units[1].Statuses | Where-Object { $_.ClassId -eq 25177 } | Select-Object -First 1
$checkpointPersisted = $null -ne $savedStatus -and $savedStatus.RoundEndTriggeredCount -eq 1 -and
    $savedStatus.RoundEndSkills[0].EntityId -eq 50180
Assert-RoundEnd $checkpointPersisted 'Round-end trigger state did not survive checkpoint persistence'

Write-Output 'Battle status round-end checks passed: fixed 25177 source/target mapping, master-turn timing, trigger-target position and checkpoint persistence.'

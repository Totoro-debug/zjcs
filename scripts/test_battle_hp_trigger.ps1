$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Trigger($condition, $message) { if (-not $condition) { throw $message } }
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

# Official mapping: status 60680 triggers 60681 when the owner falls below 15% HP.
$official = $catalog.CreateStatus(60680, 1, 1)
Assert-Trigger ([Math]::Abs($official.HpTriggerPer - 0.15) -lt 0.0001) 'Official HP trigger ratio mismatch'
Assert-Trigger ($official.HpTriggerStatuses.Count -ge 1 -and $official.HpTriggerStatuses[0] -eq 60681) 'Official HP trigger status list mismatch'

function New-Unit($hp, $maxHp) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = 1; $unit.Side = 1; $unit.Hp = $hp; $unit.MaxHp = $maxHp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $unit.Turn.Id = 1; $unit.Turn.Hp = $hp; $unit.Turn.Speed = 10
    return $unit
}

function New-TriggerStatus($per, $statusId, $maxCount, $atStart) {
    $status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $status.InstanceId = 1; $status.ClassId = 50000; $status.OwnerId = 1; $status.CreatorId = 1
    $status.DurationRound = -1; $status.CurDurationRound = -1; $status.StartRound = 0
    $status.HpTriggerPer = $per; $status.HpTriggerMaxCount = $maxCount
    $status.HpTriggerAtStart = $atStart
    $status.HpTriggerStatuses.Add($statusId)
    return $status
}

# Below the threshold the configured status is applied and counted.
$unit = New-Unit 40 100
$unit.Statuses.Add((New-TriggerStatus 0.5 26004 -1 $false))
$applied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($unit, $catalog, $false)
Assert-Trigger ($applied -eq 1 -and $unit.Statuses[0].HpTriggeredCount -eq 1) 'HP trigger did not fire'
Assert-Trigger (($unit.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'HP trigger did not apply its status'
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($unit, $catalog, $false)
Assert-Trigger (($unit.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1 -and $unit.Statuses[0].HpTriggeredCount -eq 2) 'HP trigger duplicated a non-stacking status'

# Above the threshold nothing happens; MaxCount limits repeated triggers.
$healthy = New-Unit 80 100
$healthy.Statuses.Add((New-TriggerStatus 0.5 26004 -1 $false))
Assert-Trigger ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($healthy, $catalog, $false) -eq 0) 'HP trigger fired above its threshold'

$limited = New-Unit 40 100
$limited.Statuses.Add((New-TriggerStatus 0.5 26005 1 $false))
Assert-Trigger ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($limited, $catalog, $false) -eq 1) 'Limited HP trigger did not fire'
Assert-Trigger ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($limited, $catalog, $false) -eq 0) 'HP trigger ignored MaxCount'

# TryAtStart statuses fire from the executor's opening pass.
$start = New-Unit 40 100
$start.Statuses.Add((New-TriggerStatus 0.5 26005 -1 $true))
$start.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$enemy = New-Unit 1000 1000
$enemy.Id = 2; $enemy.Side = 2; $enemy.X = 1; $enemy.Y = 0
$enemy.Turn.Id = 2; $enemy.Turn.Hp = 1000; $enemy.Turn.Speed = 1
$cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 5
$cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
$cp.Units.Add($start); $cp.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
[void][Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
Assert-Trigger (($start.Statuses | Where-Object { $_.ClassId -eq 26005 }).Count -eq 1) 'Executor did not run the HP trigger opening pass'

Write-Output 'Battle HP trigger checks passed: official mapping, threshold, trigger status application, MaxCount and executor opening pass.'

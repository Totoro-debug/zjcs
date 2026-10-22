$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Stage($condition, $message) {
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

# Official rules parsing: stage -> fight_stage_status class and its view list.
$stageCatalog = [Zjcs.Combat.Official.FightStageStatusCatalog]::Parse((Read-Text 'assets/official/rules.json'))
Assert-Stage ($stageCatalog.ClassIdOfStage(1001) -eq 0) 'Stage 1001 unexpectedly maps to a stage status row'
Assert-Stage ($stageCatalog.ClassIdOfStage(9910) -eq 1) 'Stage 9910 stage status mapping mismatch'
$official = $stageCatalog.Find(1)
Assert-Stage ($null -ne $official) 'Stage status class 1 is missing'
Assert-Stage ($official.Status.Count -eq 2 -and $official.Status.Contains(26004) -and $official.Status.Contains(26030)) 'Stage status class 1 status list mismatch'
Assert-Stage ([Math]::Abs($official.GlobalRoundStatusDelay - 12) -lt 0.0001 -and $official.ReleaseEveryGlobalRound) 'Stage status class 1 global round cadence mismatch'
$view = $stageCatalog.ViewStatusList(1)
Assert-Stage ($view.Contains(26004) -and $view.Contains(26030) -and $view.Contains(26005)) 'View status list lost configured statuses'
Assert-Stage (-not $view.Contains(26025)) 'View status list ignored ViewStatusBlackList'

# Real combat catalog for status templates.
$skillRules = Read-Text 'unity/Assets/Official/Combat/skill-rules.json'
$battleRules = Read-Text 'unity/Assets/Official/Combat/battle-rules.json'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillRules, (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    $battleRules, (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')

$config = [Zjcs.Combat.Official.FightBattleStageStatusConfig]::new()
$config.ClassId = 1
$config.Status.Add(26004)
$character = [Zjcs.Combat.Official.FightBattleStatusGroup]::new()
$character.Key = 2
$character.Status.Add(26030)
$config.CharacterStatus.Add($character)
$config.GlobalRoundStatus.Add(26005)
$faction = [Zjcs.Combat.Official.FightBattleStatusGroup]::new()
$faction.Key = 1
$faction.Status.Add(26025)
$config.FactionGlobalRoundStatus.Add($faction)
$config.GlobalRoundStatusDelay = 1.0
$config.ReleaseEveryGlobalRound = $true
$config.ViewStatusBlackList.Add(26025)

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1; $player.Hp = 1000; $player.MaxHp = 1000
$player.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $player.Turn.Id = 1; $player.Turn.Hp = 1000; $player.Turn.Speed = 100
$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.CharacterType = 2; $enemy.Hp = 5000; $enemy.MaxHp = 5000
$enemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $enemy.Turn.Id = 2; $enemy.Turn.Hp = 5000; $enemy.Turn.Speed = 50
$units = [Zjcs.Combat.Official.FightBattleUnitState[]]@($player, $enemy)

[Zjcs.Combat.Official.FightStageStatusRuntime]::ApplyStart($config, $catalog, $units, 0)
Assert-Stage (($player.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'Stage status was not applied to the player'
Assert-Stage (($enemy.Statuses | Where-Object { $_.ClassId -eq 26004 }).Count -eq 1) 'Stage status was not applied to the enemy'
Assert-Stage (($enemy.Statuses | Where-Object { $_.ClassId -eq 26030 }).Count -eq 1) 'Character status was not applied to its character type'
Assert-Stage (($player.Statuses | Where-Object { $_.ClassId -eq 26030 }).Count -eq 0) 'Character status leaked to another character type'
Assert-Stage (($player.Statuses | Where-Object { $_.ClassId -eq 26005 }).Count -eq 0) 'Global round status was applied at battle start'

[Zjcs.Combat.Official.FightStageStatusRuntime]::ApplyGlobalRound($config, $catalog, $units, 1)
Assert-Stage (($player.Statuses | Where-Object { $_.ClassId -eq 26005 }).Count -eq 1) 'Global round status was not applied to the player'
Assert-Stage (($enemy.Statuses | Where-Object { $_.ClassId -eq 26025 }).Count -eq 1) 'Faction global round status was not applied to the enemy side'
Assert-Stage (($player.Statuses | Where-Object { $_.ClassId -eq 26025 }).Count -eq 0) 'Faction global round status leaked to the other side'
[Zjcs.Combat.Official.FightStageStatusRuntime]::ApplyGlobalRound($config, $catalog, $units, 2)
Assert-Stage (($player.Statuses | Where-Object { $_.ClassId -eq 26005 }).Count -eq 1) 'Global round status was applied outside its cadence'

$visible = [Zjcs.Combat.Official.FightStageStatusRuntime]::ViewStatusIds($config)
Assert-Stage ($visible.Contains(26004) -and $visible.Contains(26030) -and $visible.Contains(26005)) 'Visible stage status ids lost configured entries'
Assert-Stage (-not $visible.Contains(26025)) 'Visible stage status ids ignored the blacklist'

# Checkpoint validation and serialization keep the stage status configuration.
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 5
$checkpoint.Units.Add($player); $checkpoint.Units.Add($enemy)
$wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2)
$checkpoint.Waves.Add($wave)
$checkpoint.StageStatus = $config
[Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($checkpoint)
$json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint)
$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
$roundTripOk = $null -ne $loaded.StageStatus -and $loaded.StageStatus.ClassId -eq 1 -and $loaded.StageStatus.Status.Count -eq 1 -and $loaded.StageStatus.CharacterStatus.Count -eq 1 -and $loaded.StageStatus.FactionGlobalRoundStatus.Count -eq 1 -and [Math]::Abs($loaded.StageStatus.GlobalRoundStatusDelay - 1) -lt 0.0001
Assert-Stage $roundTripOk 'Stage status did not survive the checkpoint round trip'
$invalid = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
$invalid.StageStatus.GlobalRoundStatusDelay = -1
$rejected = $false
try { [Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($invalid) } catch { $rejected = $true }
Assert-Stage $rejected 'Negative stage status delay was accepted'

Write-Output 'Battle stage status checks passed: official class mapping, view list blacklist, start application, global-round cadence, faction scope, checkpoint validation and round trip.'

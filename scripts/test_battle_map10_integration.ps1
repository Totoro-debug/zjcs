$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Integration($condition, $message) { if (-not $condition) { throw $message } }
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

$map = Get-Content (Join-Path $root 'unity/Assets/Official/World/Map/10/Resources/Map10/battle-runtime.json') -Raw | ConvertFrom-Json
$roster = $map.monsterRosters |
    Sort-Object -Property @{ Expression = { ($_.Waves | ForEach-Object { $_.Units.Count }) | Measure-Object -Sum | Select-Object -ExpandProperty Sum } } -Descending |
    Select-Object -First 1
Assert-Integration ($null -ne $roster) 'Map10 battle roster is missing'
$stage = $map.stages | Where-Object { $_.Type -eq 1 -and $_.GameId -eq $roster.GameId } | Select-Object -First 1
Assert-Integration ($null -ne $stage) 'Map10 battle stage for the largest roster is missing'
$scene = $map.battleScenes | Where-Object { $_.GameId -eq $stage.GameId } | Select-Object -First 1
Assert-Integration ($null -ne $roster -and $null -ne $scene) 'Map10 battle roster or scene is missing'

$grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$grid.Width = [int]$scene.FightGridGroundComponent.Size.X
$grid.Height = [int]$scene.FightGridGroundComponent.Size.Y
$grid.OriginX = [single]$scene.FightGridGroundComponent.OriginPos.X
$grid.OriginY = [single]$scene.FightGridGroundComponent.OriginPos.Y
$grid.OriginZ = [single]$scene.FightGridGroundComponent.OriginPos.Z
$grid.DefaultGroundType = 2
$moveMask = 0
foreach ($cell in $scene.FightGridGroundComponent.PosInfos) {
    $grid.Cells.Add([Zjcs.Combat.Official.FightBattleGridCell]@{
        X = [int]$cell.Pos.X; Y = [int]$cell.Pos.Y; GroundType = [int]$cell.GroundType; Height = [single]$cell.Height
    })
    if ($cell.Height -ge 999) { $grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new([int]$cell.Pos.X, [int]$cell.Pos.Y)) }
    elseif ($cell.GroundType -ge 0 -and $cell.GroundType -lt 31) { $moveMask = $moveMask -bor (1 -shl [int]$cell.GroundType) }
}
Assert-Integration ($moveMask -ne 0) 'Map10 battle grid has no walkable ground'
$entry = $scene.FightComponent.FactionInfoList | Where-Object { $_.Faction -eq 0 } | Select-Object -First 1

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = [int]$stage.Id
$checkpoint.MaxRound = if ($stage.MaxRound -gt 0) { [int]$stage.MaxRound } else { [int]$roster.MaxRound }
$checkpoint.RandomSeed = 12345
$checkpoint.Grid = $grid

$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1; $player.Side = 1; $player.CharacterType = 1; $player.Level = 30; $player.SubRank = 3
$player.X = [int]$entry.EntryPosList[0].X; $player.Y = [int]$entry.EntryPosList[0].Y
$player.Hp = 5000; $player.MaxHp = 5000; $player.LastMaxHp = 5000; $player.MoveMask = $moveMask
$player.MoveDist = 8
$player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 50000 })
$player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 2; Value = 50 })
$player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 3; Value = 5000 })
$player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 4; Value = 50 })
$player.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$player.Turn.Id = 1; $player.Turn.Hp = 5000; $player.Turn.Speed = 50
$player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$playerSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$playerSkill.Slot = 0; $playerSkill.EntityId = 12860; $playerSkill.ItemId = 10012; $playerSkill.TargetType = 1
$playerSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$playerSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(2, 0))
$playerHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$playerHit.EntityId = 12861; $playerHit.DamageId = 12869; $playerHit.DamageType = 8
$playerHit.HasCustomDamage = $true; $playerHit.CustomDamage = 500; $playerHit.TargetMode = 0; $playerHit.Delay = 0.3
$playerSkill.Hits.Add($playerHit)
$player.Skills.Add($playerSkill)
$player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = $playerSkill.LimitedTimes })
$checkpoint.Units.Add($player)

$nextId = 2
$totalMonsters = 0
foreach ($waveData in $roster.Waves | Sort-Object Index) {
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
    $wave.Index = [int]$waveData.Index
    $wave.Spawned = $wave.Index -eq 0
    foreach ($source in $waveData.Units) {
        $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $unit.Id = $nextId; $nextId++
        $unit.Side = 2; $unit.CharacterType = 2
        $unit.MonsterId = [int]$source.MonsterId; $unit.MonsterType = [int]$source.MonsterType
        $unit.Level = [Math]::Round($source.Level); $unit.SubRank = [int]$source.SubRank
        $unit.Wave = [int]$waveData.Index
        $unit.X = [int]$source.Position.X; $unit.Y = [int]$source.Position.Y
        $unit.MoveMask = $moveMask
        foreach ($property in $source.Properties) {
            $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = [int]$property.Id; Value = [long]$property.Value })
        }
        $maxHp = 0
        foreach ($property in $unit.Properties) { if ($property.Id -eq 3) { $maxHp = $property.Value } }
        Assert-Integration ($maxHp -gt 0) "Map10 monster $($source.MonsterId) has no MaxHp"
        $unit.Hp = $maxHp; $unit.MaxHp = $maxHp; $unit.LastMaxHp = $maxHp
        $unit.MoveDist = 5
        $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
        $unit.Turn.Id = $unit.Id; $unit.Turn.Hp = $maxHp
        $unit.Turn.Speed = 20
        foreach ($property in $unit.Properties) { if ($property.Id -eq 4) { $unit.Turn.Speed = $property.Value } }
        $unit.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
        foreach ($active in $source.ActiveSkills) {
            $skill = $catalog.CreateSkill([int]$active.EntityId, [int]$active.Slot, [int]$active.ItemId,
                [Math]::Max(1, [int]$active.Rank), [Math]::Max(1, $unit.Level), [Math]::Max(1, $unit.SubRank), $null)
            $unit.Skills.Add($skill)
            $unit.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
                Slot = $skill.Slot; LimitedTimes = $skill.LimitedTimes
            })
        }
        $checkpoint.Units.Add($unit)
        $wave.UnitIds.Add($unit.Id)
        $totalMonsters++
    }
    $checkpoint.Waves.Add($wave)
}
Assert-Integration ($totalMonsters -ge 2) 'Map10 roster has too few monsters for the integration check'

$formula = $catalog.CreateDamageFormula((Read-Text 'unity/Assets/Official/Combat/rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/rules.json'), '854_156383')
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)

$steps = 0
$damageEvents = 0
$firstResult = $null
$replayChecked = $false
while (-not $checkpoint.Finished -and $steps -lt 200) {
    $result = $executor.Step(0, $steps + 1)
    $steps++
    $damageEvents += $result.Event.DamageEvents.Count
    foreach ($unit in $checkpoint.Units) {
        Assert-Integration ($unit.Hp -ge 0 -and $unit.Hp -le $unit.MaxHp) "Unit $($unit.Id) HP $($unit.Hp) left [0,$($unit.MaxHp)]"
    }
    if ($firstResult -eq $null) {
        $firstResult = $result
        $json = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint)
        $restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $replay = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog).Step(0, 1)
        Assert-Integration ($replay.Replayed) 'Map10 battle step replay was not detected'
        Assert-Integration ($replay.Event.DamageEvents.Count -eq $result.Event.DamageEvents.Count) 'Map10 replay damage count differs'
        $replayChecked = $true
    }
}

Assert-Integration $replayChecked 'Map10 battle did not execute a replayable step'
Assert-Integration ($steps -gt 0) 'Map10 battle did not advance'
Assert-Integration ($damageEvents -gt 0 -or $checkpoint.Finished) 'Map10 battle produced neither damage nor an outcome'
if ($checkpoint.Finished) {
    Assert-Integration ($null -ne $checkpoint.Outcome) 'Finished Map10 battle has no outcome'
}
$playerEvents = 0
$playerDamage = 0
foreach ($receipt in $checkpoint.Receipts) { foreach ($evt in $receipt.Event.DamageEvents) { if ($evt.SourceId -eq 1) { $playerEvents++; $playerDamage += [Math]::Abs([long]$evt.Damage);  } } }
$deadMonsters = @($checkpoint.Units | Where-Object { $_.Side -ne 1 -and $_.Hp -le 0 }).Count
$aliveMonsters = @($checkpoint.Units | Where-Object { $_.Side -ne 1 -and $_.Hp -gt 0 }).Count
Write-Output "Map10 battle integration passed: $steps steps, $damageEvents damage events, playerEvents=$playerEvents, deadMonsters=$deadMonsters, aliveMonsters=$aliveMonsters, wave=$($checkpoint.WaveIndex), finished=$($checkpoint.Finished), won=$($checkpoint.Outcome.Won), reason=$($checkpoint.Outcome.Reason), round=$($checkpoint.Outcome.Round)."

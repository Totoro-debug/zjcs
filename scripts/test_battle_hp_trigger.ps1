. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Trigger($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
$skillSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'))
$battleSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/battle-rules-source-manifest.json'))
$formulaSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    $skillSource.OutputSha256, $skillSource.Manifest,
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    $battleSource.OutputSha256, $battleSource.Manifest)

# Official mapping: status 60680 triggers 60681 when the owner falls below 15% HP.
$official = $catalog.CreateStatus(60680, 1, 1)
Assert-Trigger ([Math]::Abs($official.HpTriggerPer - 0.15) -lt 0.0001) 'Official HP trigger ratio mismatch'
Assert-Trigger ($official.HpTriggerStatuses.Count -ge 1 -and $official.HpTriggerStatuses[0] -eq 60681) 'Official HP trigger status list mismatch'
$officialSkillStatus = $catalog.CreateStatus(25070, 1, 1)
Assert-Trigger ([Math]::Abs($officialSkillStatus.HpTriggerPer - 0.15) -lt 0.0001) 'Official HP trigger skill ratio mismatch'
Assert-Trigger ($officialSkillStatus.HpTriggerSkills.Count -eq 1 -and $officialSkillStatus.HpTriggerSkills[0] -eq 50010) 'Official HP trigger skill list mismatch'

function New-Unit($hp, $maxHp) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = 1; $unit.Side = 1; $unit.Hp = $hp; $unit.MaxHp = $maxHp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new(); $unit.Turn.Id = 1; $unit.Turn.Hp = $hp; $unit.Turn.Speed = 10
    return $unit
}

function New-TriggerStatus($per, $statusId, $maxCount, $atStart) {
    $status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $status.InstanceId = 1; $status.ClassId = 999999; $status.OwnerId = 1; $status.CreatorId = 1
    $status.RootSkillId = 999990; $status.ApplyingSkillItemId = 999991
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
$startExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $cp, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
[void]$startExecutor.Step(0, 1)
Assert-Trigger (($start.Statuses | Where-Object { $_.ClassId -eq 26005 }).Count -eq 1) 'Executor did not run the HP trigger opening pass'

# Official status 60680 applies 60681; 25070 requests skill 50010 below 15%.
$officialUnit = New-Unit 100 1000
$officialStatus = $catalog.CreateStatus(60680, 1, 1)
$officialUnit.Statuses.Add($officialStatus)
$officialApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($officialUnit, $catalog, $false)
Assert-Trigger ($officialApplied -eq 1) 'Official HP trigger did not fire'
Assert-Trigger (($officialUnit.Statuses | Where-Object { $_.ClassId -eq 60681 }).Count -eq 1) 'Official HP trigger did not apply 60681'
$skillUnit = New-Unit 100 1000
$skillUnit.Statuses.Add($officialSkillStatus)
$triggerSkills = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusStateTrigger]]::new()
$skillApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($skillUnit, $catalog, $false, $triggerSkills)
Assert-Trigger ($skillApplied -eq 1 -and $triggerSkills.Count -eq 1 -and $triggerSkills[0].EntityId -eq 50010) 'Official HP trigger did not request skill 50010'
Assert-Trigger ($triggerSkills[0].SourceSubRank -eq 0) 'HP trigger collection replaced a stored zero sub-rank'

# Executor: an enemy hit drops the player below the threshold and the trigger skill is cast.
function Invoke-HpTriggerBattle($skillId, $rank = 1, $level = 1, $subRank = 0,
    $useOfficialFormula = $false, $restoreBeforeHit = $false) {
    $triggerPlayer = New-Unit 100 1000
    $triggerPlayer.Level = 83; $triggerPlayer.SubRank = 23
    $triggerPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $triggerStatus = New-TriggerStatus 0.5 26004 1 $false
    $triggerStatus.HpTriggerStatuses.Clear()
    $triggerStatus.HpTriggerSkills.Add($skillId)
    $triggerStatus.SourceSkillRank = $rank; $triggerStatus.SourceSkillLevel = $level
    $triggerStatus.SourceSubRank = $subRank
    $triggerPlayer.Statuses.Add($triggerStatus)
    $attacker = New-Unit 1000 1000
    $attacker.Id = 2; $attacker.Side = 2; $attacker.X = 1; $attacker.Y = 0
    $attacker.Turn.Id = 2; $attacker.Turn.Hp = 1000; $attacker.Turn.Speed = 1
    $attacker.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $attacker.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $attackSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $attackSkill.Slot = 0; $attackSkill.EntityId = 910; $attackSkill.ItemId = 911; $attackSkill.TargetType = 1
    $attackSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(-1, 0))
    $attackHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $attackHit.EntityId = 912; $attackHit.DamageId = 913; $attackHit.DamageType = 8
    $attackHit.HasCustomDamage = $true; $attackHit.CustomDamage = 50; $attackHit.TargetMode = 0
    $attackSkill.Hits.Add($attackHit)
    $attacker.Skills.Add($attackSkill)
    $triggerCp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $triggerCp.StageId = 1; $triggerCp.MaxRound = 10; $triggerCp.RandomSeed = 29
    $triggerCp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $triggerCp.Grid.Width = 8; $triggerCp.Grid.Height = 8; $triggerCp.Grid.DefaultGroundType = 2
    $triggerCp.Units.Add($triggerPlayer); $triggerCp.Units.Add($attacker)
    $triggerWave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $triggerWave.Index = 0; $triggerWave.UnitIds.Add(2); $triggerCp.Waves.Add($triggerWave)
    $battleFormula = $formula
    if ($useOfficialFormula) {
        $triggerPlayer.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('Attack'); Value = 100 })
        $triggerPlayer.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('MaxHp'); Value = 1000 })
        $battleFormula = $catalog.CreateDamageFormula((Read-Text 'unity/Assets/Official/Combat/rules.json'),
            $formulaSource.OutputSha256, $formulaSource.Manifest)
    }
    $triggerExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($triggerCp, $battleFormula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    if ($restoreBeforeHit) {
        $triggerCp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($triggerCp))
        $triggerExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($triggerCp, $battleFormula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
        $triggerPlayer = $triggerCp.Units[0]; $triggerStatus = $triggerPlayer.Statuses[0]
    }
    $triggerStep = $null
    for ($index = 1; $index -le 40; $index++) {
        $step = $triggerExecutor.Step(0, $index)
        if ($triggerPlayer.Hp -ne 100) { $triggerStep = $step; break }
    }
    Assert-Trigger ($null -ne $triggerStep) 'Trigger test enemy never damaged the player'
    Assert-Trigger ($triggerPlayer.Hp -eq 50) "Trigger test player HP is $($triggerPlayer.Hp) instead of 50"
    $triggerCast = @($triggerStep.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq $skillId -and $_.Slot -eq -1 -and $_.ActorId -eq 1 })
    Assert-Trigger ($triggerCast.Count -eq 1) 'Executor did not record the trigger skill cast'
    Assert-Trigger ($triggerStatus.HpTriggeredCount -eq 1) "Executor did not count HP trigger $skillId exactly once"
    return $triggerStep
}

[void](Invoke-HpTriggerBattle 10640)

# A derived self-buff keeps the originating skill's cultivation rather than the character's.
$sourceSubRank = 10
$cultivated = Invoke-HpTriggerBattle 63434 6 17 $sourceSubRank $false $true
$buff = @($cultivated.Checkpoint.Units[0].Statuses | Where-Object { $_.ClassId -eq 24658 })
Assert-Trigger ($buff.Count -eq 1 -and $buff[0].CreatorId -eq 1) 'HP-triggered skill did not apply its self-buff'
Assert-Trigger ($buff[0].SourceSkillRank -eq 6 -and $buff[0].SourceSkillLevel -eq 17 -and $buff[0].SourceSubRank -eq $sourceSubRank) 'HP-triggered skill lost parent cultivation or used character level/sub-rank'
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cultivated.Checkpoint))
$savedBuff = @($restored.Units[0].Statuses | Where-Object { $_.ClassId -eq 24658 })
Assert-Trigger ($savedBuff[0].SourceSkillRank -eq 6 -and $savedBuff[0].SourceSkillLevel -eq 17 -and $savedBuff[0].SourceSubRank -eq $sourceSubRank) 'HP-triggered skill cultivation did not survive checkpoint restore'

$low = Invoke-HpTriggerBattle 11050 1 1 1 $true
$high = Invoke-HpTriggerBattle 11050 6 17 10 $true $true
$lowDamage = @($low.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 11050 -and $_.SourceId -eq 1 -and $_.TargetId -eq 2 })
$highDamage = @($high.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 11050 -and $_.SourceId -eq 1 -and $_.TargetId -eq 2 })
Assert-Trigger ($lowDamage.Count -gt 0 -and $highDamage.Count -gt 0 -and [Math]::Abs($highDamage[0].Damage) -gt [Math]::Abs($lowDamage[0].Damage)) 'HP-triggered skill cultivation did not affect official damage calculation'

Write-Output 'Battle HP trigger checks passed: official mapping, threshold, trigger status application, MaxCount, opening pass, inherited cultivation, official damage and checkpoint restore.'

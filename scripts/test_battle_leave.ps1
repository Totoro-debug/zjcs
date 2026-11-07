. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)
$rules = [Zjcs.Combat.Official.FightBattleLeaveRules]
$phase = [Zjcs.Combat.Official.FightBattlePhase]
function Assert-Leave($condition, $message) { if (-not $condition) { throw $message } }
foreach ($value in [Enum]::GetValues($phase)) {
    $preparing = $value -eq $phase::Preparing
    $playing = $value -eq $phase::Play -or $value -eq $phase::PlayStart
    Assert-Leave ($rules::CanLeave($value, $true, $true) -eq ($preparing -or $playing)) "Unexpected leave visibility in $value"
    Assert-Leave ($rules::CanLeave($value, $true, $false) -eq $preparing) "Preparation permission leaked into $value"
    Assert-Leave ($rules::CanLeave($value, $false, $true) -eq $playing) "Play permission leaked into $value"
    Assert-Leave (-not $rules::CanLeave($value, $false, $false)) "Disabled leave accepted $value"
}
Assert-Leave ($null -eq $rules::ConfirmationKey(28, $phase::Preparing, $null)) 'Arena preparation should leave directly'
Assert-Leave ($rules::ConfirmationKey(28, $phase::Play, $null) -eq 'LeaveGame.ConfirmText') 'Arena play needs the official failure confirmation'
Assert-Leave ($rules::ConfirmationKey(11, $phase::Preparing, $null) -eq 'ui_daily_dungeon_end_panel_leave_game_tip') 'Daily dungeon always confirms'
Assert-Leave ($rules::ConfirmationKey(8, $phase::Preparing, $null) -eq 'ui_voidfissure_leave_confirm') 'Missing void part must preserve the official non-first-layer branch'
Assert-Leave ($null -eq $rules::ConfirmationKey(8, $phase::Preparing, 1)) 'First void layer preparation should leave directly'
Assert-Leave ($rules::ConfirmationKey(8, $phase::Play, 1) -eq 'LeaveGame.ConfirmText') 'First void layer play needs the ordinary confirmation'
Assert-Leave ($rules::ConfirmationKey(8, $phase::Play, 2) -eq 'ui_voidfissure_leave_confirm') 'Deep void layer needs its own wording'
Assert-Leave ($null -eq $rules::ConfirmationKey(3, $phase::Play, $null)) 'Demon battlefield (including Fantasy Tower stages) leaves directly'
Assert-Leave ($null -eq $rules::ConfirmationKey(500, $phase::Play, $null)) 'Free battle leaves directly'
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 4800; $checkpoint.RandomSeed = 42
$checkpoint.PreparationPending = $true; $checkpoint.PreparationSecondsRemaining = 10
$checkpoint.Outcome.Round = 2
$checkpoint.Units.Add([Zjcs.Combat.Official.FightBattleUnitState]@{ Id = 1; Side = 1; Hp = 55; MaxHp = 100 })
$checkpoint.Units.Add([Zjcs.Combat.Official.FightBattleUnitState]@{ Id = 2; Side = 2; Hp = 800; MaxHp = 1000 })
foreach ($unit in $checkpoint.Units) { $unit.Turn.Id = $unit.Id; $unit.Turn.Hp = $unit.Hp }
$checkpoint.Statistics.Units.Add([Zjcs.Combat.Official.FightBattleUnitStatistics]@{ UnitId = 1; DamageDealt = 200 })
$checkpoint.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; Cooldown = 3; LimitedTimes = -1 })
Assert-Leave ([Zjcs.Offline.Combat.OfflineBattlePartEnd]::Finish($checkpoint)) 'Partial battle did not finish'
Assert-Leave ($checkpoint.PartEnded -and $checkpoint.Finished -and $checkpoint.Outcome.CanSettle -and -not $checkpoint.Outcome.Won) 'Partial battle fabricated a victory or invalid result'
Assert-Leave ($checkpoint.Counter -eq 0 -and $checkpoint.Sequence -eq 0 -and $checkpoint.Units[0].SkillState.Slots[0].Cooldown -eq 3 -and $checkpoint.Units[1].Hp -eq 800) 'Partial settlement advanced a turn, cooldown or enemy death'
Assert-Leave (-not $checkpoint.PreparationPending -and $checkpoint.PreparationSecondsRemaining -eq 0) 'Partial settlement retained an auto-start countdown'
$saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint)
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$report = [Zjcs.Combat.Official.FightBattleReportBuilder]::Build($restored)
Assert-Leave ($restored.PartEnded -and $report.Units[0].DamageDealt -eq 200 -and $report.PlayerHp -eq 55 -and $report.EnemyHp -eq 800 -and $report.Round -eq 2) 'Cold partial settlement lost damage or HP statistics'
Assert-Leave (-not [Zjcs.Offline.Combat.OfflineBattlePartEnd]::Finish($restored)) 'Partial settlement accepted a duplicate request'
$preparation = [Zjcs.Combat.Official.FightBattlePhaseMachine]::new(10)
$preparation.Begin($false)
Assert-Leave ($preparation.BeginBattleEnd() -and -not $preparation.Ready()) 'Preparation exit started a turn instead of settling'
Write-Output 'Battle leave checks passed: phase permissions, solo preparation/play, daily dungeon, void layers, demon battlefield and free battle.'

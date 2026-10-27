. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Phase($condition, $message) {
    if (-not $condition) { throw $message }
}

$machine = [Zjcs.Combat.Official.FightBattlePhaseMachine]::new(2.0)
$machine.Begin($false)
Assert-Phase ($machine.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::Preparing -and $machine.CountdownRemaining -eq 2.0) 'Battle phase machine did not enter the official prepare countdown'
$machine.Tick(0.5)
Assert-Phase ($machine.CountdownRemaining -eq 1.5) 'Battle prepare countdown did not decrease'
$autoStartCount = 0
$machine.add_AutoStartRequested([Action] { $script:autoStartCount++ })
$machine.Tick(2.0)
Assert-Phase ($machine.CountdownRemaining -eq 0 -and $autoStartCount -eq 1) 'Battle prepare countdown did not request auto start once'
$machine.Tick(1.0)
Assert-Phase ($autoStartCount -eq 1) 'Battle prepare countdown requested auto start repeatedly'
Assert-Phase ($machine.Ready() -and $machine.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::PlayStart) 'Battle did not start play after the prepare countdown'
Assert-Phase ($machine.StartPlay() -and $machine.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::Play) 'Battle phase machine did not enter Play'
Assert-Phase ($machine.Pause() -and $machine.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::Pause) 'Battle phase machine did not enter Pause from Play'
Assert-Phase ($machine.Resume() -and $machine.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::Play) 'Battle phase machine did not resume from Pause'
Assert-Phase (-not $machine.Resume()) 'Battle resumed from a non-paused phase'
Assert-Phase ($machine.BeginEntryTransition() -and $machine.FinishEntryTransition() -and $machine.StartPlay()) 'Battle entry transition order mismatch'
Assert-Phase ($machine.BeginBattleEnd() -and $machine.BeginBattleEndTransition()) 'Battle end transition order mismatch'
Assert-Phase ($machine.FinishBattleEndTransition() -and $machine.Finish()) 'Battle release transition order mismatch'
Assert-Phase ($machine.IsFinished -and $machine.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::End) 'Battle phase machine did not reach End'
Assert-Phase (-not $machine.StartPlay() -and -not $machine.Pause()) 'Finished battle accepted another play transition'

$skipped = [Zjcs.Combat.Official.FightBattlePhaseMachine]::new()
$skipped.Begin($true)
Assert-Phase ($skipped.Phase -eq [Zjcs.Combat.Official.FightBattlePhase]::PlayStart) 'SkipPrepareStatus did not skip preparation'
Assert-Phase ($skipped.BeginBattleEnd()) 'Battle could not end directly from PlayStart'

# Official StartGameBtn supports preparation without a server-supplied PrepareStartTime.
# No deadline must leave the player free to position units and press the start button.
$manual = [Zjcs.Combat.Official.FightBattlePhaseMachine]::new()
$manual.Begin($false)
$manual.Tick(100)
Assert-Phase ($manual.IsPreparing -and $manual.CountdownRemaining -eq 0) 'A missing deadline silently skipped manual preparation'
Assert-Phase ($manual.Ready() -and $manual.StartPlay()) 'Manual preparation could not start via the official start action'

Assert-Phase (-not [Zjcs.Combat.Official.FightBattlePhaseMachine]::IsLegal(
    [Zjcs.Combat.Official.FightBattlePhase]::Preparing, [Zjcs.Combat.Official.FightBattlePhase]::Play)) 'Battle phase machine allowed Preparing to jump to Play'
Assert-Phase (-not [Zjcs.Combat.Official.FightBattlePhaseMachine]::IsLegal(
    [Zjcs.Combat.Official.FightBattlePhase]::End, [Zjcs.Combat.Official.FightBattlePhase]::Play)) 'Battle phase machine allowed a transition out of End'
Assert-Phase ([Zjcs.Combat.Official.FightBattlePhaseMachine]::IsLegal(
    [Zjcs.Combat.Official.FightBattlePhase]::Play, [Zjcs.Combat.Official.FightBattlePhase]::Pause)) 'Battle phase machine rejected the official pause transition'
Assert-Phase ([Zjcs.Combat.Official.FightBattlePhaseMachine]::IsLegal(
    [Zjcs.Combat.Official.FightBattlePhase]::BattleEndTransitionFinished,
    [Zjcs.Combat.Official.FightBattlePhase]::End)) 'Battle phase machine rejected the final release transition'

Write-Output 'Battle phase machine checks passed: prepare countdown, ready, auto start, play/pause, entry and end transitions, release, and legal-order guards.'

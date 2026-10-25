$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
$resolver = Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRoundDisplay.cs'
Add-Type -Path (@($sources) + $resolver)

function Assert-RoundDisplay($display, $round, $maxRound, $isPersonal, $label) {
    if ($display.Round -ne $round -or $display.MaxRound -ne $maxRound -or $display.IsPersonal -ne $isPersonal) {
        throw "Battle round display mismatch: $label"
    }
}

$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.MaxRound = 25
$checkpoint.Outcome.Round = 8
$player = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$player.Id = 1
$player.SkillState.Round = 3
$checkpoint.Units.Add($player)

$checkpoint.IsPlayerRoundLimit = $true
$checkpoint.PlayerRoundLimit = 10
Assert-RoundDisplay ([Zjcs.Presentation.Combat.OfficialBattleRoundDisplayResolver]::Resolve($checkpoint)) 3 10 $true 'personal round'
$player.SkillState.Round = 12
Assert-RoundDisplay ([Zjcs.Presentation.Combat.OfficialBattleRoundDisplayResolver]::Resolve($checkpoint)) 10 10 $true 'personal round cap'

$checkpoint.PlayerRoundLimit = 0
$player.SkillState.Round = 7
Assert-RoundDisplay ([Zjcs.Presentation.Combat.OfficialBattleRoundDisplayResolver]::Resolve($checkpoint)) 7 25 $true 'personal round with global cap fallback'
$checkpoint.IsPlayerRoundLimit = $false
Assert-RoundDisplay ([Zjcs.Presentation.Combat.OfficialBattleRoundDisplayResolver]::Resolve($checkpoint)) 8 25 $false 'global fallback'
$checkpoint.Outcome = $null
$checkpoint.Timeline.Round = 6
Assert-RoundDisplay ([Zjcs.Presentation.Combat.OfficialBattleRoundDisplayResolver]::Resolve($checkpoint)) 6 25 $false 'timeline fallback'

$format = 'Round {0}/{1}'
$display = [Zjcs.Presentation.Combat.OfficialBattleRoundDisplay]::new()
$display.Round = 20
$display.MaxRound = 25
$text = [Zjcs.Presentation.Combat.OfficialBattleRoundTextFormatter]::Format($format, $display, 5)
if ($text -ne 'Round <color=#F75237FF>20</color>/25') { throw "Countdown round color mismatch: $text" }
$display.Round = 19
$text = [Zjcs.Presentation.Combat.OfficialBattleRoundTextFormatter]::Format($format, $display, 5)
if ($text -ne 'Round 19/25') { throw "Non-countdown round color mismatch: $text" }

Write-Output 'Battle round display checks passed: personal and global rounds, cap clamp, timeline fallback, and countdown color.'

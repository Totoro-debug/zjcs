$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_jump_position.ps1')

$native = $catalog.CreateSkill(10610, 0, 10009, 1, 1, 1, $null)
$actor = Unit 100 1 5 5
$small = Unit 101 2 6 5
$big = Unit 102 2 4 5
$big.BodyOffsets.Add((Point 0 1))
$roster = [Zjcs.Combat.Official.FightBattleUnitState[]]@($actor, $small, $big)
$selection = [Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange($actor, $roster, $native)
Require ($selection.Count -eq 1 -and $selection[0].Id -eq 102) 'Original 10009 did not prioritize the adjacent large-body target'
Write-Output 'Original 10009 prioritized the large-body target over the adjacent small target.'

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_players.ps1')

# Children precede their owners in the roster; dead/ended summons must still be included.
$state=[Zjcs.Combat.Official.FightBattleCheckpoint]@{ StageId=1;RandomSeed=17 }
foreach ($entry in @(@(30,3,31,1),@(2,4,1,1),@(31,3,1,1),@(1,1,0,1),@(32,3,2,1),@(42,3,41,1),@(41,3,40,1),@(40,5,1,1),@(10,1,0,1),@(20,1,0,2),@(21,3,20,2),@(22,2,0,1))) {
    $unit=[Zjcs.Combat.Official.FightBattleUnitState]@{ Id=$entry[0];CharacterType=$entry[1];MasterId=$entry[2];Side=$entry[3];Hp=0;MaxHp=100;Ended=$true }
    $unit.Turn.Id=$unit.Id; $unit.Turn.MasterId=$unit.MasterId
    $state.Units.Add($unit)
    $null=$state.Statistics.Unit($unit.Id)
    $state.Statistics.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=$unit.Id;TargetId=20;SkillEntityId=900;DamageType=8;Damage=(-$unit.Id);HasAbsoluteDamage=$true;AbsoluteDamage=(-$unit.Id) })
}
$report=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($state)
$groups=[Zjcs.Combat.Official.FightBattleReportGrouping]::Build($report,1)
Require ($groups.Unassigned.Count -eq 0 -and ($groups.Players.TargetId -join ',') -eq '2,1,10') 'NPCs/players did not retain independent roster-order rows'
$npc=$groups.Players[0]; $player=$groups.Players[1]
Require (($npc.Normal.UnitId -join ',') -eq '2' -and ($npc.Summon.UnitId -join ',') -eq '32') 'NPC was merged into the main player, or its own summon was lost'
Require (($player.Summon.UnitId -join ',') -eq '30,31' -and ($player.Pet.UnitId -join ',') -eq '42,41,40') 'Nested, dead or ended summons lost the final owner or source category, including pet descendants'
Require ($report.Unit(30).MasterId -eq 31 -and $report.Unit(30).CharacterType -eq 3 -and -not $report.Unit(30).Alive) 'Report snapshot did not retain ownership/type/liveness independently'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state)
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$again=[Zjcs.Combat.Official.FightBattleReportGrouping]::Build([Zjcs.Combat.Official.FightBattleReportBuilder]::Build($restored),1)
Require (($again.Players[1].Summon.UnitId -join ',') -eq '30,31' -and ($again.Players[1].Pet.UnitId -join ',') -eq '42,41,40' -and $again.Players[0].Normal[0].DamageDealt -eq 2) 'Saved checkpoint changed summon/pet lineage or attributed NPC damage'
$other=[Zjcs.Combat.Official.FightBattleReportGrouping]::Build($report,2)
Require ($other.Players.Count -eq 1 -and $other.Players[0].TargetId -eq 20 -and $other.Players[0].Summon.Count -eq 1) 'Grouping mixed sides or hard-coded the main player ID'

foreach ($edge in @(
    @{Id=50;CharacterType=3;MasterId=999;Reason='MissingMaster'},
    @{Id=51;CharacterType=3;MasterId=51;Reason='OwnershipCycle'},
    @{Id=52;CharacterType=3;MasterId=20;Reason='CrossSideMaster'},
    @{Id=53;CharacterType=7;MasterId=1;Reason='UnsupportedCharacterType'},
    @{Id=54;CharacterType=5;MasterId=30;Reason='UnconfirmedMasterCategory'},
    @{Id=55;CharacterType=0;MasterId=1;Reason='UnsupportedCharacterType'})) {
    $extra=[Zjcs.Combat.Official.FightBattleReportUnit]@{ UnitId=$edge.Id;CharacterType=$edge.CharacterType;MasterId=$edge.MasterId;Side=1 }
    $report.Units.Add($extra)
    $bad=[Zjcs.Combat.Official.FightBattleReportGrouping]::Build($report,1)
    Require ($bad.Unassigned.Count -eq 1 -and $bad.Unassigned[0].Unit.UnitId -eq $edge.Id -and $bad.Unassigned[0].Reason -eq $edge.Reason) 'Incomplete ownership was silently discarded, reassigned or lost its reason'
    $null=$report.Units.Remove($extra)
}
Write-Output 'Player grouping passed: independent NPC/player rows, recorded nested ownership, dead/ended summons, separate pets, roster order, sides, checkpoint restore and explicit unresolved/cyclic/proxy records.'

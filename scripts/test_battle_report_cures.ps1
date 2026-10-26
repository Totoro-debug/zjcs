$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_shields.ps1')

# Resolve real cure hits through the executor. Both selected targets begin at full HP.
$f=New-Fixture 1
$f.Skill.Hits[0].ActionType=5
$f.Skill.HasCustomCure=$true; $f.Skill.CustomCure=10
$f.Formula=$officialFormula
foreach ($unit in $f.State.Units) {
    foreach ($name in @('BaseCureAdd','BaseBeCureAdd')) {
        $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$officialFormula.PropertyId($name);Value=10000})
    }
}
$f.Runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($f.State,$f.Formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
$step=$f.Runner.Step(0,1)
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State).Skills[0]
Require ($step.Event.DamageEvents.Count -eq 2 -and $step.Event.DamageEvents[0].IsCure -and $step.Event.DamageEvents[0].Damage -eq 0 -and $step.Event.DamageEvents[0].DamageValue -eq 10) 'The fixture did not resolve positive cures clamped by full HP'
Require ($row.HasCureHitCounts -and $row.CureHits -eq 2 -and $row.Heal -eq 0 -and $row.DamageHits -eq 0) 'Full-health cures must count hits without adding effective healing or damage hits'
$f.Runner.Step(0,1) | Out-Null
Require ($f.State.Statistics.Skills[0].CureHits -eq 2) 'Replayed receipt counted cure hits twice'
foreach ($target in @($f.State.Units[1],$f.State.Units[2])) { $target.Hp-=100; $target.Turn.Hp=$target.Hp }
for ($n=0;$n -lt 100 -and $f.State.Statistics.Skills[0].Casts -lt 2;$n++) { $null=$f.Runner.Step($f.State.Sequence,$f.State.Counter+1) }
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State).Skills[0]
Require ($row.CureHits -eq 4 -and $row.Heal -eq 20) 'Effective healing must remain distinct from cure-hit count'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State)
$loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($loaded).Skills[0]
Require ($row.HasCureHitCounts -and $row.CureHits -eq 4 -and $row.Heal -eq 20) 'Checkpoint lost cure-hit availability/count'
$restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($loaded,$f.Formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
for ($n=0;$n -lt 100 -and $loaded.Statistics.Skills[0].Casts -lt 3;$n++) { $null=$restored.Step($loaded.Sequence,$loaded.Counter+1) }
Require ($loaded.Statistics.Skills[0].CureHits -eq 6) 'Restored battle did not continue cure-hit counting'

# Missing old history must stay unknown even after the restored skill lands another cure.
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(($saved -replace '"HasCureHitCounts":true,','' -replace '"CureHits":4,',''))
$old=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($legacy).Skills[0]
Require (-not $old.HasCureHitCounts) 'Legacy checkpoint fabricated cure-hit history'
$legacy.Statistics.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{SourceId=1;TargetId=2;SkillEntityId=900;SkillItemId=901;IsCure=$true;Damage=0})
Require (-not $legacy.Statistics.Skills[0].HasCureHitCounts) 'One new cure made incomplete historical counts appear complete'
Write-Output 'Battle report cure checks passed: real full-health/positive cures, distinct effective healing, replay, persistence, resumed counting and incomplete legacy history.'

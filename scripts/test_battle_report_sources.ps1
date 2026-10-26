$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_status_counts.ps1')

# Original HP-threshold status 60680 -> 60681, followed by a controlled second status hop.
$f=New-Fixture 1
$owner=$f.State.Units[1]; $owner.Hp=10000
$parent=$catalog.CreateStatus(60680,2,1,1,902,903)
$parent.HpTriggerMaxCount=1
$owner.Statuses.Add($parent)
$random=[Zjcs.Combat.Official.FightRandomStream]::new(17)
$null=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner,$catalog,$false,$null,$random)
$child=$owner.Statuses | Where-Object ClassId -eq 60681
Require ($child.CreatorId -eq 1 -and $child.RootSkillId -eq 0 -and $child.ReportSource.UnitId -eq 1 -and $child.ReportSource.SkillEntityId -eq 902) 'HP-threshold child lost its root report source or changed gameplay root'
$child.HpTriggerPer=0.5; $child.HpTriggerMaxCount=1
$child.HpTriggerStatuses.Add(16553)
$null=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($owner,$catalog,$false,$null,$random)
$grandchild=$owner.Statuses | Where-Object ClassId -eq 16553
Require ($grandchild.ReportSource.SkillEntityId -eq 902 -and -not [Object]::ReferenceEquals($child.ReportSource,$grandchild.ReportSource)) 'A second status hop lost or shared mutable root metadata'
$f.State.Statistics.RecordPendingStatusApplications($f.State.Units)
Require ($f.State.Statistics.Skill(1,902,903).BuffApplications -eq 2) 'Multi-hop applications did not reach the established root skill'

# HpAcc explicitly makes the applicator the gameplay creator. Statistical ancestry still belongs
# to the original caster/skill; it must not change creator-dependent properties or falloff.
$g=New-Fixture 1; $owner=$g.State.Units[1]
$parent=$catalog.CreateStatus(70144,2,1,1,902,903)
$parent.HpAccUnitHp=10; $parent.HpAccMaxCount=1; $parent.HpAccTriggerSkills.Clear()
foreach ($id in @(16553,24174)) {
    $parent.HpAccTriggerStatuses.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{ EntityId=$id; Target=2; BasePercent=1 })
}
$owner.Statuses.Add($parent)
$null=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyAccumulatedDamage($owner,10,$catalog,$random,$null)
$shield=$owner.Statuses | Where-Object ClassId -eq 24174
$shield.ShieldValue=150; $shield.ShieldRemainValue=150; $shield.ShieldInitialized=$true
Require ($shield.CreatorId -eq 2 -and $shield.RootSkillId -eq 0 -and $shield.ApplyingSkillItemId -eq 0 -and $shield.SourceSkillRank -eq 1 -and $shield.SourceProperties.Count -eq 0 -and $shield.ReportSource.UnitId -eq 1) 'Inherited report source changed configured applicator ownership or gameplay snapshots'
$serialized=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($g.State)
$loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($serialized)
$loaded.Statistics.RecordPendingStatusApplications($loaded.Units)
$loaded.Statistics.RecordPendingShieldGrants($loaded.Units)
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($loaded).Skills | Where-Object SkillEntityId -eq 902
Require ($row.UnitId -eq 1 -and $row.BuffApplications -eq 1 -and $row.ShieldGranted -eq 150 -and $loaded.Statistics.Unit(1).ShieldGranted -eq 150 -and $loaded.Statistics.Unit(2).ShieldReceived -eq 150 -and $loaded.Statistics.Unit(2).ShieldGranted -eq 0) 'Saved pending child application/shield was credited to its gameplay applicator'
$loaded.Statistics.RecordPendingStatusApplications($loaded.Units); $loaded.Statistics.RecordPendingShieldGrants($loaded.Units)
Require ($row.BuffApplications -eq 1 -and $loaded.Statistics.Skill(1,902,903).ShieldGranted -eq 150) 'Restored ancestry counted pending effects twice'

# A status reacting to the end of a skill inherits the status root. Effects produced by the
# independent skill itself start at that skill instead, even within the very same executor step.
$f=New-Fixture 1
$reactor=$catalog.CreateStatus(60840,1,3,1,902,903)
$f.State.Units[0].Statuses.Add($reactor)
$f.Skill.ActionType=2
$f.Skill.Hits[0].AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{ StatusId=16553; TargetType=1; Chance=1; AffectedByProp=$false })
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($f.State,$officialFormula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$null=$runner.Step(0,1)
$reaction=$f.State.Units[2].Statuses | Where-Object ClassId -eq 60841
Require ($reaction.CreatorId -eq 1 -and $reaction.RootSkillId -eq 900 -and $reaction.ReportSource.UnitId -eq 3 -and $reaction.ReportSource.SkillEntityId -eq 902) 'Skill-end reaction used the observed skill as its statistical parent'
Require ($f.State.Statistics.Skill(3,902,903).BuffApplications -eq 1 -and $f.State.Statistics.Skill(1,900,901).BuffApplications -eq 2 -and $f.State.Statistics.Skill(3,902,903).Casts -eq 0) 'Status reaction and independent cast merged report roots'
$null=$runner.Step(0,1)
Require ($f.State.Statistics.Skill(3,902,903).BuffApplications -eq 1) 'Replayed receipt executed status ancestry twice'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State))
Require ($saved.Units[2].Statuses[0].ReportSource.SkillItemId -eq 903 -and $saved.Statistics.Skill(3,902,903).BuffApplications -eq 1) 'Checkpoint lost established ancestry'

# Unknown roots remain unknown across changes of gameplay creator and across old saves.
$h=New-Fixture 1; $owner=$h.State.Units[1]
$parent=$catalog.CreateStatus(70144,2,1,1)
$parent.HpAccUnitHp=10; $parent.HpAccMaxCount=1; $parent.HpAccTriggerSkills.Clear()
$parent.HpAccTriggerStatuses.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{ EntityId=16553; Target=2; BasePercent=1 })
$owner.Statuses.Add($parent)
$null=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyAccumulatedDamage($owner,10,$catalog,$random,$null)
$h.State.Statistics.RecordPendingStatusApplications($h.State.Units)
Require ($h.State.Statistics.Unit(1).UnattributedBuffApplications -eq 1 -and $h.State.Statistics.Unit(2).BuffApplications -eq 0 -and $h.State.Statistics.Skills.Count -eq 0) 'Unknown root was guessed from the current actor or gameplay creator'
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(($serialized -replace ',"ReportSource":\{[^}]*\}',''))
$legacyShield=$legacy.Units[1].Statuses | Where-Object ClassId -eq 24174
Require ([Zjcs.Combat.Official.FightBattleStatusReportSource]::Resolve($legacyShield).SkillEntityId -eq 0) 'Legacy child provenance was fabricated from a nearby parent'
Write-Output 'Battle report source checks passed: HP threshold, multi-hop statuses, accumulated damage, skill-end reaction, independent cast, separate gameplay creator, shield credit, pending save/restore, replay and unknown legacy roots.'

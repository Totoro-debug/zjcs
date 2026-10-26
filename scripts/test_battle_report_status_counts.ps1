$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_cures.ps1')

# Real executor, two hit targets: self refreshes count once for each accepted application.
$f=New-Fixture 1
foreach ($spec in @(@(16553,1,1),@(24012,2,1),@(24010,2,1),@(24037,2,0),@(12062,1,1))) {
    $f.Skill.Hits[0].AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{
        StatusId=$spec[0];TargetType=$spec[1];Chance=$spec[2];AffectedByProp=$false })
}
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($f.State,$officialFormula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$step=$runner.Step(0,1)
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State).Skills[0]
Require ($row.HasStatusApplicationCounts -and $row.BuffApplications -eq 2 -and $row.DebuffApplications -eq 4) 'Accepted per-target refreshes, debuffs/abnormal debuffs, probability failure or None-status exclusion differ'
$runner.Step(0,1) | Out-Null
Require ($f.State.Statistics.Skills[0].BuffApplications -eq 2) 'Receipt replay counted status applications twice'

# Accept multiple stacks once, exclude Discard, and attribute a refresh to the incoming source.
$target=$f.State.Units[1]
$old=$target.Statuses | Where-Object ClassId -eq 24012 | Select-Object -First 1
$incoming=$catalog.CreateStatus(24012,$target.Id,3,100,902,903,1,1,1,$null,0)
$old.ActionBehOfSameStatus=[Zjcs.Combat.Official.FightStatusLifecycle]::BehaviourDiscard
$null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target,$incoming,0,100)
Require ($target.PendingStatusApplications.Count -eq 0) 'Discard emitted an accepted status application'
$old.IsOpenStack=$true;$old.MaxStackedCount=20;$old.IsResetDurationWhenStack=$true
$incoming.StackCount=4
$null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target,$incoming,3,101)
Require ($old.StackCount -eq 5 -and $old.StartRound -eq 3 -and $target.PendingStatusApplications.Count -eq 1) 'Adding multiple stacks/refreshing duration must count one application'
$old.IsOpenStack=$false;$old.ActionBehOfSameStatus=[Zjcs.Combat.Official.FightStatusLifecycle]::BehaviourImmediate
$null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target,$incoming,4,102)
$incoming.ActionBehOfSameStatus=[Zjcs.Combat.Official.FightStatusLifecycle]::BehaviourParallel
foreach ($status in $target.Statuses) { if ($status.ClassId -eq 24012) { $status.ActionBehOfSameStatus=0 } }
$null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($target,$incoming,4,103)
# Even removal before aggregation cannot erase an accepted application.
$target.Statuses.Clear()
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State)
$loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
Require ($loaded.Units[1].PendingStatusApplications.Count -eq 3) 'Checkpoint lost pending status applications'
$loaded.Statistics.RecordPendingStatusApplications($loaded.Units)
$incomingRow=$loaded.Statistics.Skills | Where-Object { $_.UnitId -eq 3 -and $_.SkillEntityId -eq 902 }
Require ($incomingRow.DebuffApplications -eq 3 -and $loaded.Statistics.Skills[0].DebuffApplications -eq 4) 'Stack/replace/parallel application was lost or attributed to the existing creator'
$loaded.Statistics.RecordPendingStatusApplications($loaded.Units)
Require ($incomingRow.DebuffApplications -eq 3) 'Draining the pending application journal counted twice'
Require ([Zjcs.Combat.Official.FightBattleReportBuilder]::Build($loaded).Skills[0].HasStatusApplicationCounts) 'Complete attributed history became unavailable'

# Preserve explicit unit-level attribution when a root skill is unavailable; do not invent skill 0.
$unknown=$catalog.CreateStatus(16553,2,1,200)
$null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($loaded.Units[1],$unknown,5,200)
$loaded.Statistics.RecordPendingStatusApplications($loaded.Units)
Require ($loaded.Statistics.Unit(1).UnattributedBuffApplications -eq 1 -and @($loaded.Statistics.Skills | Where-Object SkillEntityId -eq 0).Count -eq 0) 'Missing root skill was silently guessed or discarded'
$stable=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($loaded)
$again=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($stable)
$report=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($again)
Require (-not $report.Skills[0].HasStatusApplicationCounts -and $report.Skills[0].BuffApplications -eq 2 -and $report.Skills[0].DebuffApplications -eq 4 -and $report.Unit(1).UnattributedBuffApplications -eq 1) 'Saved report lost counters or exposed incomplete attribution as a complete skill total'
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(($stable -replace '"HasStatusApplicationCounts":true,',''))
Require (-not [Zjcs.Combat.Official.FightBattleReportBuilder]::Build($legacy).Skills[0].HasStatusApplicationCounts) 'Legacy report fabricated status application history'
$template=$catalog.CreateStatus(16553,2,1,201,900,901,1,1,1,$null,0)
$null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($legacy.Units[1],$template,5,201)
$legacy.Statistics.RecordPendingStatusApplications($legacy.Units)
Require (-not $legacy.Statistics.Skills[0].HasStatusApplicationCounts) 'A new application made missing historical counts appear complete'
Write-Output 'Battle report status counts passed: real multi-target applications, Buff/Debuff/AbnormalDebuff, failure/None/discard exclusion, stack/refresh/replace/parallel, incoming attribution, removal, pending/save/restore, replay and legacy history.'

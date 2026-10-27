$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_sources.ps1')
$scoreJson=Read-Source 'report-score-settings'
$settings=[Zjcs.Combat.Official.FightBattleReportScoreSettings]::Load($scoreJson,(Hash-Source 'report-score-settings'),'854_156383')
Require ($settings.BreakResilienceScoreFactor -eq 1 -and $settings.ApplyStatusScore.Count -eq 4 -and ($settings.ReportAllPlayerStages -join ',') -eq '4,500,28,44,45,46,47,34,600') 'Original report configuration was changed or truncated'

# Build type-specific counts from real accepted applications, rather than estimating the abnormal
# subset from the combined debuff counter. The raw BreakResilience argument is deliberately supplied.
$unit=[Zjcs.Combat.Official.FightBattleUnitState]@{ Id=2 }
$counts=[Collections.Generic.Dictionary[int,int]]::new()
foreach ($id in @(16553,24012,24010,12062)) {
    $template=$catalog.CreateStatus($id,2,1,$id,900,901)
    $null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($unit,$template,0,$id)
}
foreach ($application in $unit.PendingStatusApplications) {
    if (-not $counts.ContainsKey($application.StatusType)) { $counts[$application.StatusType]=0 }
    $counts[$application.StatusType]++
}
Require ($unit.PendingStatusApplications.Count -eq 3 -and $settings.GetApplyStatusScore(7,$counts) -eq 27) 'Original score for Buff/Debuff/AbnormalDebuff applications differs from 5+5+10 plus the supplied value 7'
Require ($settings.GetApplyStatusScore(7,$null) -eq 0 -and $settings.GetApplyStatusScore(7,[Collections.Generic.Dictionary[int,int]]::new()) -eq 7 -and $settings.GetReportApplyStatusScore(999) -eq 0) 'Original null/empty/unknown-type scoring branches changed'
$bad=$false
try { $null=[Zjcs.Combat.Official.FightBattleReportScoreSettings]::Load($scoreJson,('0'*64),'854_156383') } catch { $bad=$true }
Require $bad 'Report settings accepted mismatched source bytes'
Write-Output 'Original report score settings passed: APK factors/stage list, accepted status applications, null/empty/unknown branches and source digest rejection; no server counter semantics inferred.'

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_averages.ps1')
function Read-Source($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Source($name) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes((Join-Path $root "unity/Assets/Official/Combat/$name.json")))).ToLowerInvariant() }
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Source 'skill-rules'), (Hash-Source 'skill-rules'), '854_156383',
    (Read-Source 'battle-rules'), (Hash-Source 'battle-rules'), '854_156383')
$officialFormula = $catalog.CreateDamageFormula((Read-Source 'rules'), (Hash-Source 'rules'), '854_156383')

# Execute a controlled single-target cast through the real status application and original
# custom-shield component 24174. The configured grant is 250; no absorption has occurred.
$f=New-Fixture 1
$f.Skill.Range.RemoveAt(1); $f.Skill.Hits[0].Scope.RemoveAt(1)
$f.Skill.Hits[0].AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{
    StatusId=24174; TargetType=1; Chance=1; AffectedByProp=$false; CustomShieldValue=250 })
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($f.State,$officialFormula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$step=$runner.Step(0,1)
$report=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State)
$row=$report.Skills[0]
Require ($row.HasShieldGrantCounts -and $row.ShieldGranted -eq 250 -and $row.ShieldPer -eq 1) 'A successful original custom-shield grant was not attributed to its caster/skill'
Require ($f.State.Units[0].Statuses[0].ShieldRemainValue -eq 250 -and $report.Unit(1).ShieldGranted -eq 250 -and $report.Unit(1).ShieldReceived -eq 250) 'Report shield value depended on absorption instead of addition'
Require (-not $f.State.Units[0].Statuses[0].ShieldGrantPending) 'A resolved grant remained pending'
$runner.Step(0,1) | Out-Null
Require ($f.State.Statistics.Skills[0].ShieldGranted -eq 250) 'A replayed receipt counted its shield grant twice'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State)
$loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$loaded.Statistics.RecordPendingShieldGrants($loaded.Units)
Require ($loaded.Statistics.Skills[0].ShieldGranted -eq 250) 'A restored initialized shield was counted as a new grant'

# Same-status discard/stack returns the existing object; it does not add another numeric shield.
$template=$catalog.CreateStatus(24174,1,1,99,900,901,1,1,1,$null,250)
$existing=$loaded.Units[0].Statuses[0]
$existing.ActionBehOfSameStatus=[Zjcs.Combat.Official.FightStatusLifecycle]::BehaviourDiscard
$applied=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($loaded.Units[0],$template,0,99)
$loaded.Statistics.RecordPendingShieldGrants($loaded.Units)
Require ([object]::ReferenceEquals($existing,$applied) -and $loaded.Statistics.Skills[0].ShieldGranted -eq 250) 'Discard fabricated a shield grant'
$existing.IsOpenStack=$true; $existing.MaxStackedCount=5
$applied=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($loaded.Units[0],$template,0,100)
$loaded.Statistics.RecordPendingShieldGrants($loaded.Units)
Require ($existing.StackCount -eq 2 -and $loaded.Statistics.Skills[0].ShieldGranted -eq 250) 'A stack update counted the unchanged shield again'

# Ratios are per caster, across that caster's skills; HP ratios use that caster, not the target.
$cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$cp.Units.Add([Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;MaxHp=1000;Hp=1000})
$cp.Units.Add([Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;MaxHp=100000;Hp=100000})
$a=$cp.Statistics.Skill(1,1,101); $a.Damage=300; $a.Heal=100; $a.ShieldGranted=250
$b=$cp.Statistics.Skill(1,2,102); $b.Damage=100; $b.Heal=300; $b.ShieldGranted=750
$other=$cp.Statistics.Skill(2,3,103); $other.Damage=10000; $other.Heal=10000; $other.ShieldGranted=10000
$zero=$cp.Statistics.Skill(3,4,104)
$r=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($cp)
Require ($r.Skills[0].DamagePer -eq 0.75 -and $r.Skills[0].HealPer -eq 0.25 -and $r.Skills[0].ShieldPer -eq 0.25) 'Another caster polluted skill distribution denominators'
Require ($r.Skills[0].HasMaxHpPercentages -and [Math]::Abs($r.Skills[0].HealMaxHpPer-0.1) -lt 0.000001 -and $r.Skills[0].ShieldMaxHpPer -eq 0.25) 'HP ratios used the target or another denominator'
Require ($r.Skills[3].DamagePer -eq 0 -and $r.Skills[3].HealPer -eq 0 -and $r.Skills[3].ShieldPer -eq 0 -and -not $r.Skills[3].HasMaxHpPercentages) 'Empty totals or missing caster HP fabricated ratios'
$a.HasShieldGrantCounts=$false
$r=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($cp)
Require (-not $r.Skills[0].HasShieldGrantCounts -and -not $r.Skills[1].HasShieldGrantCounts -and $r.Skills[1].ShieldPer -eq 0) 'A partial historical denominator appeared complete'
$a.HasShieldGrantCounts=$true
$cp.Units[1].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{
 CreatorId=1; RootSkillId=0; ShieldValue=200; ShieldInitialized=$true; ShieldGrantPending=$true })
$cp.Statistics.RecordPendingShieldGrants($cp.Units)
$r=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($cp)
Require ($r.Unit(1).UnattributedShieldGranted -eq 200 -and $r.Unit(2).ShieldReceived -eq 200 -and -not $r.Skills[0].HasShieldGrantCounts) 'A grant with an unavailable root skill silently disappeared or fabricated a complete distribution'
$old=$saved -replace '"HasShieldGrantCounts":true,','' -replace '"ShieldGranted":250,',''
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($old)
Require (-not [Zjcs.Combat.Official.FightBattleReportBuilder]::Build($legacy).Skills[0].HasShieldGrantCounts) 'Legacy checkpoint invented shield grant history'
Write-Output 'Battle report shields passed: actual original-component grants, attribution before absorption, replay/restore, discard/stack exclusion, per-caster distributions, caster HP ratios and unavailable legacy data.'

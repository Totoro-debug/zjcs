$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test_battle_report_scores.ps1')

# Resolve a real formula hit: 1000 basic damage, 50% defence and 50% reduction => 250.
# A 100 shield leaves 150 before the HP clamp; 50 HP is lost and 100 overflows.
$schema=[Zjcs.Combat.Official.FightDamagePropertySchema]::new()
foreach ($entry in @(@('Attack',1),@('Defence',2),@('TestCoefficient',999),@('DmgReducePercent',998))) {
    $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name=$entry[0];Id=$entry[1] })
}
$schema.FloatPropertyIds.Add(999); $schema.FloatPropertyIds.Add(998)
$formula=[Zjcs.Combat.Official.FightDamageFormula]::new($schema,[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
foreach ($shield in @(100,1000)) {
    $f=New-Fixture 1
    $f.Skill.Range.RemoveAt(1); $f.Skill.Hits[0].Scope.RemoveAt(1)
    $hit=$f.Skill.Hits[0]; $hit.DamageType=3; $hit.BasicProperty=1; $hit.DamageProperty=999; $hit.CustomDamage=1000
    $f.State.Units[0].Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=1;Value=1000 })
    $f.State.Units[0].Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=999;Value=10000 })
    $target=$f.State.Units[1]; $target.Hp=50; $target.Turn.Hp=50
    $target.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=2;Value=1000 })
    $target.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=998;Value=10000 })
    $target.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{
        ClassId=99998;InstanceId=1;DurationRound=-1;ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
        ShieldValue=$shield;ShieldRemainValue=$shield;ShieldInitialized=$true })
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($f.State,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
    $step=$runner.Step(0,1)
    $event=$step.Event.DamageEvents[0]
    $row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State).Unit(2)
    $hpLoss=50; $shieldLoss=100; $beforeClamp=-150
    if ($shield -eq 1000) { $hpLoss=0; $shieldLoss=250; $beforeClamp=0 }
    Require ($event.HasAbsoluteDamage -and $event.AbsoluteDamage -eq -1000 -and $event.ShieldDamage -eq $shieldLoss -and $event.DamageValue -eq $beforeClamp -and $event.Damage -eq -$hpLoss) 'Real formula, shield or overflow fixture did not resolve as expected'
    Require ($row.HasAbsoluteDamageTaken -and $row.AbsoluteDamageTaken -eq 1000 -and $row.DamageTaken -eq $hpLoss) 'BeAbsoluteDamage excluded defence/reduction, shielding or overflow, or changed actual HP loss'
    $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State)
    $loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
    $again=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($loaded,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
    $null=$again.Step(0,1)
    Require ($loaded.Statistics.Unit(2).AbsoluteDamageTaken -eq 1000) 'Saved/replayed receipt counted received absolute damage twice'
    $loaded.Statistics.RecordAdditionalDamage(1,900,901,10)
    Require ($loaded.Statistics.Unit(2).AbsoluteDamageTaken -eq 1000) 'Additional mark credit created another received-damage count'
}

# Successful enemy break transitions, not the number of points removed. Two landed hits empty
# a 15-point bar; a later hit on the empty bar cannot break it again. A restored bar can.
$f=New-Fixture 1; $f.Skill.Range.RemoveAt(1); $f.Skill.Hits[0].Scope.RemoveAt(1)
$f.Skill.Hits[0].BreakResilience=$true; $f.Skill.Hits[0].DamageParam=10000
$breakSchema=[Zjcs.Combat.Official.FightDamagePropertySchema]::new()
$breakSchema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name='BreakResilience';Id=113 })
$f.State.Units[0].Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=113;Value=10 })
$f.State.Units[1].MaxResilience=15; $f.State.Units[1].Resilience=15
$breakFormula=[Zjcs.Combat.Official.FightDamageFormula]::new($breakSchema,[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($f.State,$breakFormula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
$null=$runner.Step(0,1)
Require ($f.State.Units[1].Resilience -eq 5 -and $f.State.Statistics.Unit(1).BreakResilienceCount -eq 0) 'Partial resilience loss was counted as a successful break'
for ($n=0;$n -lt 100 -and $f.State.Statistics.Skill(1,900,901).Casts -lt 3;$n++) { $null=$runner.Step(0,$f.State.Counter+1) }
Require ($f.State.Units[1].Resilience -eq 0 -and $f.State.Statistics.Unit(1).BreakResilienceCount -eq 1) 'Empty-bar hits or points removed were counted as extra breaks'
$null=$runner.Step(0,$f.State.Counter)
Require ($f.State.Statistics.Unit(1).BreakResilienceCount -eq 1) 'Receipt replay counted another break'
$f.State.Units[1].Resilience=1
for ($n=0;$n -lt 100 -and $f.State.Statistics.Skill(1,900,901).Casts -lt 4;$n++) { $null=$runner.Step(0,$f.State.Counter+1) }
Require ($f.State.Statistics.Unit(1).BreakResilienceCount -eq 2) 'A restored bar could not count a subsequent break'

# Rooted and unrooted applications still belong to the known caster's player score.
foreach ($id in @(16553,24012,24010)) {
    $template=$catalog.CreateStatus($id,3,1,$id)
    $null=[Zjcs.Combat.Official.FightStatusLifecycle]::Apply($f.State.Units[2],$template,0,$id)
}
$f.State.Statistics.RecordPendingStatusApplications($f.State.Units)
$report=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State); $row=$report.Unit(1)
[long]$score=0
Require ($row.HasStatusTypeCounts -and $row.OrdinaryDebuffApplications -eq 1 -and $row.AbnormalDebuffApplications -eq 1 -and $row.DebuffApplications -eq 2 -and $settings.TryGetApplyStatusScore($row,[ref]$score) -and $score -eq 22) 'Player score did not use accepted per-type applications and two successful enemy breaks'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State)
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
Require ($settings.TryGetApplyStatusScore([Zjcs.Combat.Official.FightBattleReportBuilder]::Build($restored).Unit(1),[ref]$score) -and $score -eq 22) 'Saved player report lost successful breaks or the abnormal status subtype'

# Legacy absence must remain unknown after new events rather than estimating subtypes from totals.
$old=$saved -replace '"HasAbsoluteDamageTaken":true,','' -replace '"HasBreakResilienceCount":true,','' -replace '"HasStatusTypeCounts":true,',''
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($old)
$legacy.Statistics.RecordResilienceBreak(1)
$legacy.Statistics.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=2;TargetId=1;SkillEntityId=999;DamageType=8;HasAbsoluteDamage=$true;AbsoluteDamage=-10;Damage=-10 })
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($legacy).Unit(1)
Require (-not $row.HasAbsoluteDamageTaken -and -not $row.HasBreakResilienceCount -and -not $settings.TryGetApplyStatusScore($row,[ref]$score)) 'New events fabricated complete legacy player-report history'
$stats=[Zjcs.Combat.Official.FightBattleStatistics]::new()
foreach ($flags in @(@{ Dodge=$true },@{ Blinding=$true },@{ IsCure=$true },@{ IsSkillCost=$true },@{ Immunity=$true })) {
    $edge=[Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=2;SkillEntityId=900;DamageType=1 }
    foreach ($name in $flags.Keys) { $edge.$name=$true }
    $stats.RecordHit($edge)
}
Require ($stats.Unit(2).HasAbsoluteDamageTaken -and $stats.Unit(2).AbsoluteDamageTaken -eq 0) 'Miss, immunity, cure or skill cost manufactured received absolute damage or missing history'
Write-Output 'Player report counters passed: original absolute formula, defence/reduction, shield/overflow, actual HP separation, successful break transitions, type-specific scoring, replay, extra-credit exclusion and legacy persistence.'

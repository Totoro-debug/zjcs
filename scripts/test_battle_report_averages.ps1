$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
Add-Type -Path (Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
function Require($condition, $message) { if (-not $condition) { throw $message } }

function New-Fixture([int]$segments = 3) {
    $state = [Zjcs.Combat.Official.FightBattleCheckpoint]@{ StageId=1; MaxRound=1000; RandomSeed=17 }
    $state.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width=8; Height=8; DefaultGroundType=2 }
    foreach ($id in 1..3) {
        $unit = [Zjcs.Combat.Official.FightBattleUnitState]@{ Id=$id; Side=2; X=1; Y=($id-2); Hp=100000; MaxHp=100000; CharacterType=2 }
        $unit.Turn.Id=$id; $unit.Turn.Hp=$unit.Hp; $unit.Turn.Speed=1
        if ($id -eq 1) { $unit.Side=1; $unit.CharacterType=1; $unit.X=0; $unit.Y=0; $unit.Turn.Speed=100 }
        $state.Units.Add($unit)
    }
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]@{ Slot=0; EntityId=900; ItemId=901; TargetType=1 }
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,1))
    foreach ($n in 1..$segments) {
        $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]@{ EntityId=(910+$n); DamageId=(920+$n); DamageType=8; HasCustomDamage=$true; CustomDamage=10; TargetMode=1; Anchor=1 }
        $hit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
        $hit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,1))
        $skill.Hits.Add($hit)
    }
    $state.Units[0].Skills.Add($skill)
    $state.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot=0; LimitedTimes=-1 })
    $formula=[Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(),[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
    return @{ State=$state; Runner=$runner; Formula=$formula; Skill=$skill }
}

# The same target must count again on the next cast, but only once within a multi-segment cast.
$f=New-Fixture
for ($n=0; $n -lt 100 -and $f.State.Statistics.Skill(1,900,901).Casts -lt 10; $n++) {
    $step=$f.Runner.Step($f.State.Sequence,$f.State.Counter+1)
    if ($step.Event.ActorId -eq 1) {
        Require ($step.Event.DamageEvents.Count -eq 6) 'Fixture did not execute one three-segment/two-target cast'
    }
}
$report=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($f.State)
$row=$report.Skills[0]
Require ($row.HasDamageHitCounts -and $row.Casts -eq 10 -and $row.DamageHits -eq 60 -and $row.DamageTargets -eq 20) 'Per-cast damage hit/target counts differ from the confirmed 60/20 example'
Require ($row.AvgDamageCount -eq 6 -and $row.AvgDamageCountPerTarget -eq 3 -and $row.Damage -eq 600) 'Report averages or actual HP damage changed'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($f.State)
$loaded=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($loaded,$f.Formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
$replay=$restored.Step(0,$loaded.Counter)
Require ($replay.Replayed -and $loaded.Statistics.Skills[0].DamageHits -eq 60) 'Replayed checkpoint counted damage hits twice'
for ($n=0; $n -lt 100 -and $loaded.Statistics.Skill(1,900,901).Casts -lt 11; $n++) {
    $null=$restored.Step($loaded.Sequence,$loaded.Counter+1)
}
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($loaded).Skills[0]
Require ($row.Casts -eq 11 -and $row.DamageHits -eq 66 -and $row.DamageTargets -eq 22 -and $row.AvgDamageCountPerTarget -eq 3) 'Restored battle lost per-cast target counting'

# A replay recursively invokes the same skill and must own another target set.
$nested=New-Fixture
$nested.Skill.HasReplay=$true; $nested.Skill.ReplayStatusId=99997
$nested.Skill.ReplayStackCount=1; $nested.Skill.ReplayRelation=1
$nested.State.Units[0].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ ClassId=99997; InstanceId=1; DurationRound=-1; StackCount=1 })
$step=$nested.Runner.Step(0,1)
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($nested.State).Skills[0]
Require ($step.Event.SkillCasts.Count -eq 2 -and $row.Casts -eq 2 -and $row.DamageHits -eq 12 -and $row.DamageTargets -eq 4 -and $row.AvgDamageCount -eq 6 -and $row.AvgDamageCountPerTarget -eq 3) 'Recursive same-skill replay merged target denominators with the parent cast'

# A full shield hit has positive shield loss and zero HP loss, and still counts all segments.
$shielded=New-Fixture 1
foreach ($target in @($shielded.State.Units[1],$shielded.State.Units[2])) {
    $target.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ ClassId=99998; InstanceId=$target.Id; DurationRound=-1; ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent; ShieldValue=100; ShieldRemainValue=100; ShieldInitialized=$true })
}
$step=$shielded.Runner.Step(0,1)
Require ($step.Event.DamageEvents[0].Damage -eq 0 -and $step.Event.DamageEvents[0].ShieldDamage -eq 10) 'Fixture did not absorb all damage'
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($shielded.State).Skills[0]
Require ($row.DamageHits -eq 2 -and $row.DamageTargets -eq 2 -and $row.Damage -eq 0 -and $row.AvgDamageCount -eq 2) 'Shield absorption was excluded from hit counts'

# Feed resolved protocol edges to the statistics accumulator; immunity generation is a separate gap.
$stats=[Zjcs.Combat.Official.FightBattleStatistics]::new()
$stats.RecordCast(1,900,901)
$targets=[System.Collections.Generic.HashSet[int]]::new()
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=2;SkillEntityId=900;SkillItemId=901;DamageType=1;Dodge=$true },$targets)
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=3;SkillEntityId=900;SkillItemId=901;DamageType=1;Immunity=$true },$targets)
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=3;SkillEntityId=900;SkillItemId=901;DamageType=1;Immunity=$true },$targets)
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=4;SkillEntityId=900;SkillItemId=901;DamageType=1;Blinding=$true },$targets)
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=1;SkillEntityId=900;SkillItemId=901;DamageType=1;IsCure=$true;Damage=15 },$targets)
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=1;SkillEntityId=900;SkillItemId=901;DamageType=1;IsSkillCost=$true;Damage=-20 },$targets)
$stats.RecordHit([Zjcs.Combat.Official.FightBattleDamageEvent]@{ SourceId=1;TargetId=5;SkillEntityId=900;SkillItemId=901;DamageType=0 },$targets)
$stats.RecordAdditionalDamage(1,900,901,30)
$row=$stats.Skills[0]
Require ($row.HasDamageHitCounts -and $row.DamageHits -eq 2 -and $row.DamageTargets -eq 1) 'Dodge, immunity, blinding, cure, skill cost or additional attribution has incorrect damage-hit semantics'
Require ($row.Damage -eq 30 -and $row.Heal -eq 15) 'Damage counting changed damage/healing attribution'
$stats.RecordCast(1,900,901) # A cast with no damage hits contributes to H/C, but not T.
$cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$cp.Statistics=$stats
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($cp).Skills[0]
Require ($row.AvgDamageCount -eq 1 -and $row.AvgDamageCountPerTarget -eq 2) 'A zero-hit cast changed the per-target denominator'

# Missing old fields must not turn incomplete historical counts into plausible but false averages.
$old=$saved -replace '"HasDamageHitCounts":true,','' -replace '"DamageHits":60,','' -replace '"DamageTargets":20,',''
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($old)
$row=[Zjcs.Combat.Official.FightBattleReportBuilder]::Build($legacy).Skills[0]
Require (-not $row.HasDamageHitCounts -and $row.AvgDamageCount -eq 0 -and $row.AvgDamageCountPerTarget -eq 0) 'Legacy checkpoint fabricated unavailable averages'
Write-Output 'Battle report average checks passed: repeated multi-target casts, shielding, immunity/dodge, zero-hit casts, healing/cost exclusion, attribution and checkpoint continuation.'

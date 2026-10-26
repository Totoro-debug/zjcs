$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
Add-Type -Path (Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
function Require($condition, $message) { if (-not $condition) { throw $message } }
function Read-Source($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Source($name) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes((Join-Path $root "unity/Assets/Official/Combat/$name.json")))).ToLowerInvariant() }
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Source 'skill-rules'), (Hash-Source 'skill-rules'), '854_156383',
    (Read-Source 'battle-rules'), (Hash-Source 'battle-rules'), '854_156383')
$formula = $catalog.CreateDamageFormula((Read-Source 'rules'), (Hash-Source 'rules'), '854_156383')
function New-Unit([int]$id, [int]$side) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id=$id; $u.Side=$side; $u.X=$id; $u.Y=1; $u.Hp=100000; $u.MaxHp=100000
    $u.Level=1; $u.SubRank=1; $u.CharacterType=1
    $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=100
    return $u
}
function New-Fixture([int]$sourceId=1, [bool]$controlled=$false, [long]$shieldValue=0, [int]$level=1) {
    $source=New-Unit $sourceId 1; $target=New-Unit 2 2
    $mark=$catalog.CreateStatus(24062, 2, 1, 1, 0, 0, 1, $level, 1, $null)
    $mark.StartRound=0; $target.Statuses.Add($mark)
    if($controlled){
        $mark.ResolvedProperties=[System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
        $mark.ResolvedProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=231;Value=2500})
        $mark.ResolvedProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=235;Value=10})
    }
    if($shieldValue -gt 0){
        $shield=[Zjcs.Combat.Official.FightBattleStatusState]::new()
        $shield.ClassId=99998; $shield.InstanceId=2; $shield.DurationRound=-1
        $shield.ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
        $shield.ShieldValue=$shieldValue; $shield.ShieldRemainValue=$shieldValue; $shield.ShieldInitialized=$true
        $target.Statuses.Add($shield)
    }
    $units=[Zjcs.Combat.Official.FightBattleUnitState[]]@($source,$target)
    [Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($units,$catalog,$formula)
    $skill=[Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.EntityId=90001; $skill.ItemId=90101; $skill.Slot=0; $skill.TargetType=1
    $skill.HasCustomDamage=$true; $skill.CustomDamage=100; $skill.RangeVerified=$true
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
    $hit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId=90002; $hit.DamageId=90003; $hit.DamageType=8; $hit.ActionType=4
    $skill.Hits.Add($hit)
    return @{Source=$source;Target=$target;Mark=$mark;Skill=$skill;Hit=$hit;Units=$units}
}
function Hit($f) {
    $executor=[Zjcs.Combat.Official.FightBattleHitExecutor]::new($formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
    return $executor.Apply([Zjcs.Combat.Official.FightRandomStream]::new(19),1,$f.Source,$f.Target,$f.Skill,$f.Hit,0,$false)
}
$f=New-Fixture
Require ($f.Mark.HasCreatorDamageBoost) 'Official 24062 BeforeShield component was not assembled'
$props=@{};foreach($p in $f.Mark.ResolvedProperties){$props[$p.Id]=$p.Value}
Require ($props[231] -eq 2000 -and $props[235] -eq 4789) 'Official rank/level/default/factor property calculation changed'
Require (-not ($f.Target.Properties | Where-Object { $_.Id -eq 231 -or $_.Id -eq 235 })) 'Conditional mark damage properties leaked into global target properties'
$result=Hit $f
Require ($f.Target.Hp -eq 95091 -and $result.Formula.AbsoluteDamage -eq -4909) 'Original status values did not produce 100 * 1.2 + 4789 before shielding'
$f=New-Fixture 1 $true 100
$result=Hit $f
Require ($result.ShieldDamage -eq 100 -and $f.Target.Hp -eq 99965) 'Fixed addition ran before percentage, or damage boost ran after shields'
$f=New-Fixture 3 $true
$f.Source.MasterId=1
$result=Hit $f
Require ($f.Target.Hp -eq 99900) 'Another actor or the creator summon received the creator-only damage bonus'
$f=New-Fixture 1 $true
$f.Target.Statuses.Clear()
$result=Hit $f
Require ($f.Target.Hp -eq 99900) 'Removed mark still increased damage'
$f=New-Fixture 1 $true
$f.Hit.ActionType=0
$result=Hit $f
Require ($f.Target.Hp -eq 100000) 'An action with no damage generated fixed bonus damage'
$f=New-Fixture 1 $true
$f.Skill.SkillType=3; $f.Skill.HasCustomCure=$true; $f.Skill.CustomCure=100
$f.Hit.ActionType=5; $f.Target.Hp=99900
$result=Hit $f
$baseline=New-Fixture 1 $true
$baseline.Target.Statuses.Clear(); $baseline.Skill.SkillType=3; $baseline.Skill.HasCustomCure=$true; $baseline.Skill.CustomCure=100
$baseline.Hit.ActionType=5; $baseline.Target.Hp=99900
$baselineResult=Hit $baseline
Require ($result.IsCure -and $result.Cure.Cure -eq $baselineResult.Cure.Cure -and
    $f.Target.Hp -eq $baseline.Target.Hp) 'Damage-only mark modifier affected healing'
# Use the complete executor to verify accumulation, expiry, source credit and checkpoint replay together.
$f=New-Fixture 1 $true 100
$f.Hit.DamageType=2
$f.Source.Skills.Add($f.Skill)
$f.Source.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$f.Source.Turn.Speed=200
$checkpoint=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId=1; $checkpoint.MaxRound=15; $checkpoint.RandomSeed=19
$checkpoint.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new()
$checkpoint.Grid.Width=8; $checkpoint.Grid.Height=8; $checkpoint.Grid.DefaultGroundType=2
foreach($unit in $f.Units){$checkpoint.Units.Add($unit)}
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$first=$runner.Step(0,1)
Require ($first.Event.DamageEvents[0].Damage -eq -35 -and $first.Event.DamageEvents[0].ShieldDamage -eq 100 -and
    $f.Mark.AccumulatedDamage.HpDamage -eq 35) 'Complete executor did not accumulate actual boosted HP damage after shield absorption'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($runner.State))
$saved.Units[1].Statuses[0].HasCreatorDamageBoost=$false
$restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($saved,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
Require ($saved.Units[1].Statuses[0].HasCreatorDamageBoost) 'Old checkpoint did not restore the damage-process component from official config'
$end=$runner.Step(0,2);$replay=$restored.Step(0,2)
Require ($end.Event.DamageEvents[0].SkillEntityId -eq 13148 -and $end.Event.DamageEvents[0].Damage -eq -10 -and
    $replay.Event.DamageEvents[0].Damage -eq -10 -and $f.Target.Hp -eq 99955 -and
    $end.Checkpoint.Statistics.Skill(1,90001,90101).AdditionalAttributedDamage -eq 10) 'Status expiration, source credit or restoration reapplied the removed mark bonus'
Write-Output 'Battle damage-process checks passed: official 20% + 4789, percent-before-fixed, BeforeShield, creator filter, no global property leakage, healing/no-action, accumulated expiry and checkpoint replay.'

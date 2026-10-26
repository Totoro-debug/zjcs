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
function New-Fixture([int]$statusId, [int]$element, [long]$shield=0, [bool]$cure=$false) {
    $state=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $state.StageId=1; $state.MaxRound=15; $state.RandomSeed=19
    $state.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=8;Height=8;DefaultGroundType=2}
    foreach($id in 1..2){
        $unit=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=$id;Side=$id;X=$id;Y=1;Hp=10000;MaxHp=10000;Level=1;SubRank=1;CharacterType=1}
        $unit.Turn.Id=$id; $unit.Turn.Hp=$unit.Hp; $unit.Turn.Speed=300-$id*100
        $state.Units.Add($unit)
    }
    $source=$state.Units[0]; $target=$state.Units[1]
    if($statusId -gt 0){$target.Statuses.Add($catalog.CreateStatus($statusId,2,2,1))}
    if($shield -gt 0){
        $target.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{
            ClassId=99998;InstanceId=2;DurationRound=-1;ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
            ShieldValue=$shield;ShieldRemainValue=$shield;ShieldInitialized=$true})
    }
    $skill=[Zjcs.Combat.Official.FightBattleSkillDefinition]@{Slot=0;ItemId=10012;EntityId=90001;ElementType=$element;HasCustomDamage=$true;CustomDamage=100}
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
    $hit=[Zjcs.Combat.Official.FightBattleHitDefinition]@{EntityId=90002;ActionType=4;DamageType=2}
    if($cure){
        $skill.SkillType=3; $skill.HasCustomCure=$true; $skill.CustomCure=100
        $skill.TargetType=2; $hit.ActionType=5; $target.Side=1; $target.Hp=9900
        $enemy=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=3;Side=2;X=7;Y=7;Hp=10000;MaxHp=10000;Level=1;SubRank=1;CharacterType=1}
        $enemy.Turn.Id=3; $enemy.Turn.Hp=10000; $enemy.Turn.Speed=1; $state.Units.Add($enemy)
    }
    $skill.Hits.Add($hit); $source.Skills.Add($skill)
    $source.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
    return @{State=$state;Source=$source;Target=$target;Skill=$skill;Hit=$hit;Runner=$runner}
}
$config=(Read-Source 'battle-rules' | ConvertFrom-Json -AsHashtable).entities
foreach($id in 25051..25060){
    foreach($entry in $config["$id"].FightStatusDamageElementComponent.ElementInfos){
        $f=New-Fixture $id $entry.ElementType
        $step=$f.Runner.Step(0,1)
        $hit=$step.Event.DamageEvents[0]
        $value=[float]$entry.Value
        $expected=[long][Math]::Truncate(100*[float](1.0-$value))
        Require ($hit -and $hit.Damage -eq -$expected -and $hit.BeforeHp-$hit.AfterHp -eq $expected) "Official status $id / element $($entry.ElementType) did not settle $expected HP"
        Require ($hit.IsElementAffect -and $hit.ElementConfrontation -eq $value -and -not $hit.ElementConfrontationUnresolved) 'The original confrontation result fields were lost'
        $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($step.Checkpoint))
        $record=$saved.Receipts[0].Event.DamageEvents[0]
        Require ($record.ElementConfrontation -eq $value -and $record.IsElementAffect -and $record.Damage -eq -$expected) 'Saved hit lost the element result'
        $restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($saved,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
        $replay=$restored.Step(0,1)
        Require ($replay.Replayed -and $saved.Units[1].Hp -eq $f.Target.Hp) 'An element hit was applied again after restoration'
    }
}
$f=New-Fixture 25051 101 50
$step=$f.Runner.Step(0,1);$hit=$step.Event.DamageEvents[0]
Require ($hit.ShieldDamage -eq 50 -and $hit.Damage -eq -30 -and $f.Target.Hp -eq 9970) 'Element reduction must run before shield absorption and actual HP settlement'
$f=New-Fixture 25056 101 50
$step=$f.Runner.Step(0,1);$hit=$step.Event.DamageEvents[0]
Require ($hit.Damage -eq 0 -and $hit.ShieldDamage -eq 0 -and $f.Target.Statuses[1].ShieldRemainValue -eq 50 -and $hit.IsElementAffect -and -not $hit.Immunity) 'Element immunity consumed shield or was confused with damage-type immunity'
$f=New-Fixture 25051 104
$step=$f.Runner.Step(0,1)
Require ($step.Event.DamageEvents[0].Damage -eq -100 -and -not $step.Event.DamageEvents[0].IsElementAffect) 'A nonmatching element received the modifier'
$f=New-Fixture 25051 101
$f.Target.Statuses.Clear()
$step=$f.Runner.Step(0,1)
Require ($step.Event.DamageEvents[0].Damage -eq -100) 'A removed element status still affected damage'
$f=New-Fixture 25051 101 0 $true
$step=$f.Runner.Step(0,1)
Require ($step.Event.DamageEvents.Count -gt 0 -and $step.Event.DamageEvents[0].IsCure -and -not $step.Event.DamageEvents[0].IsElementAffect -and $step.Event.DamageEvents[0].ElementConfrontation -eq 0) 'Incoming element resistance affected healing'
$f=New-Fixture 25051 101
$f.Target.Statuses.Add($catalog.CreateStatus(25052,2,2,2))
$value=[float]99
Require (-not $catalog.TryResolveElementConfrontation($f.Target,101,[ref]$value) -and $value -eq 0) 'The missing multi-status server rule was guessed'
$step=$f.Runner.Step(0,1)
Require ($step.Event.DamageEvents[0].ElementConfrontationUnresolved -and -not $step.Event.DamageEvents[0].IsElementAffect) 'The unresolved overlap was reported as restored'
Write-Output 'Battle element-confrontation checks passed: all 20 official entries, pre-shield reduction, elemental immunity, nonmatching/removal/healing, explicit unresolved overlap, saved result and replay idempotency.'

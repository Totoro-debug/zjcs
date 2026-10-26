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
        $source.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$formula.PropertyId('BaseCureAdd');Value=10000})
        $target.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$formula.PropertyId('BaseBeCureAdd');Value=10000})
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
foreach($shield in @(0,70,1000)){
    $f=New-Fixture 0 0 $shield
    $step=$f.Runner.Step(0,1);$hit=$step.Event.DamageEvents[0]
    $absorbed=[Math]::Min(100,$shield)
    Require ($hit.HasDamageValue -and $hit.DamageValue -eq -(100-$absorbed) -and $hit.ShieldAddFinalDamageValue -eq -100) 'Original damage plus shield display value was lost'
    Require ($hit.Damage -eq -(100-$absorbed) -and $f.Target.Hp -eq 10000-(100-$absorbed)) 'Display changes altered actual HP settlement'
}
$f=New-Fixture 0 0
$f.Target.Hp=30
$step=$f.Runner.Step(0,1);$hit=$step.Event.DamageEvents[0]
Require ($hit.Damage -eq -30 -and $hit.DamageValue -eq -100 -and $hit.ShieldAddFinalDamageValue -eq -100 -and $step.Checkpoint.Statistics.Unit(1).DamageDealt -eq 30) 'Overkill display was clamped, or actual HP statistics were inflated'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($step.Checkpoint)
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
$record=$restored.Receipts[0].Event.DamageEvents[0]
Require ($record.HasDamageValue -and $record.DamageValue -eq -100 -and $record.Damage -eq -30 -and $record.ShieldAddFinalDamageValue -eq -100) 'Checkpoint lost original and actual damage separation'
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved.Replace('"HasDamageValue":true,','').Replace('"DamageValue":-100,',''))
Require (-not $legacy.Receipts[0].Event.DamageEvents[0].HasDamageValue -and $legacy.Receipts[0].Event.DamageEvents[0].ShieldAddFinalDamageValue -eq -30) 'Legacy checkpoint fabricated an unavailable overkill value'
foreach($hp in @(9990)){
    $f=New-Fixture 0 0 0 $true
    $f.Target.Hp=$hp
    $step=$f.Runner.Step(0,1);$hit=$step.Event.DamageEvents[0]
    Require ($hit.IsCure -and $hit.Damage -eq 10000-$hp -and $hit.DamageValue -eq 100 -and $hit.ShieldAddFinalDamageValue -eq 100 -and $step.Checkpoint.Statistics.Unit(1).HealDealt -eq 10000-$hp) "Overheal display failed: start=$hp raw=$($hit.DamageValue) real=$($hit.Damage) display=$($hit.ShieldAddFinalDamageValue) stat=$($step.Checkpoint.Statistics.Unit(1).HealDealt)"
}
$legacyHit=[Zjcs.Combat.Official.FightBattleDamageEvent]@{Damage=-30;ShieldDamage=70}
Require ($legacyHit.ShieldAddFinalDamageValue -eq -100) 'Old hit records lost available shield display information'
Write-Output 'Battle damage display passed: shield-only/mixed hits, unclamped overkill/overheal, actual HP statistics, saved raw/real values and legacy fallback.'

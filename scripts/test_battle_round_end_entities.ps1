$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-Rule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-Rule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-EntityRound($condition,$message){if(-not $condition){throw $message}}
$skills=Read-Rule 'skill-rules'
$original=Read-Rule 'battle-rules'
$fixture=$original | ConvertFrom-Json -AsHashtable
# Reuse the exact original component under a different identity. This must go
# through the real catalog parser, rather than altering a pre-parsed status ID.
$fixture.entities['9905098']=$fixture.entities['25098']
$battle=$fixture | ConvertTo-Json -Depth 100 -Compress
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skills,(Hash-Rule $skills),'component-fixture',$battle,(Hash-Rule $battle),'component-fixture')
$rules=Read-Rule 'rules'
$formula=$catalog.CreateDamageFormula($rules,(Hash-Rule $rules),'component-fixture')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-Rule $rules),'component-fixture')
Assert-EntityRound ($catalog.HasRoundEndEntityCountDefinition(9905098) -and
    $catalog.CreateStatus(9905098,1,1).RoundEndRulesSupported) 'Round-end entity counting is gated by a particular skill identity'
function Invoke-EntityRound([bool]$restore){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]@{
        StageId=1;MaxRound=8;RandomSeed=73
        Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=24;Height=24;DefaultGroundType=2}
    }
    foreach($id in 1,2){
        $unit=[Zjcs.Combat.Official.FightBattleUnitState]@{
            Id=$id;Side=$id;CharacterType=1;Profession=13;Level=70;SubRank=8;X=3*$id;Y=3
            Hp=100000;MaxHp=1000000;LastMaxHp=1000000
        }
        $unit.Turn.Id=$id;$unit.Turn.Hp=$unit.Hp;$unit.Turn.Speed=$(if($id -eq 1){1000}else{100})
        foreach($pair in @(@('Attack',1000),@('MaxHp',1000000),@('Speed',$unit.Turn.Speed),
            @('BaseCureAdd',10000),@('BaseBeCureAdd',10000))){
            $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$formula.PropertyId($pair[0]);Value=$pair[1]})
        }
        $cp.Units.Add($unit)
    }
    $status=$catalog.CreateStatus(9905098,1,1,1,9905098,10597,4,2,8,$cp.Units[0].Properties)
    $status.RoundEndMaxCount=3;$status.RoundEndTriggeredCount=2
    $cp.Units[0].Statuses.Add($status)
    $cp.GridItems.Add([Zjcs.Combat.Official.FightBattleGridItemState]@{Id=100;EntityClassId=3051;OwnerId=2;X=3;Y=3})
    $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$cp.Waves.Add($wave)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    if($restore){
        # A legacy checkpoint lacks the newly decoded condition. Restore the
        # definition by component data without replacing its accumulated calls.
        $status.RoundEndEntityCount=$null;$status.RoundEndRulesSupported=$false
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    $step=$runner.Step($cp.Sequence,$cp.Counter+1).Event
    $heals=@($step.DamageEvents|Where-Object SkillEntityId -eq 12040)
    Assert-EntityRound ($heals.Count -eq 1 -and $heals[0].IsCure -and $heals[0].SourceId -eq 1 -and
        $heals[0].TargetId -eq 1 -and $heals[0].SkillItemId -eq 10597 -and
        $heals[0].AfterHp -gt $heals[0].BeforeHp) 'The aliased original component lost its actual round-end cure or attribution'
    Assert-EntityRound ($cp.Units[0].Statuses[0].RoundEndTriggeredCount -eq 3) 'Restoring the condition reset the existing trigger count'
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
$direct=Invoke-EntityRound $false
$restored=Invoke-EntityRound $true
Assert-EntityRound ($direct -eq $restored) 'Generic entity-count restoration changed the real battle continuation'
Write-Output 'Round-end entity component checks passed: identity-independent parsing, original self cure, foreign-owner burning tile, finite count and legacy cold restoration.'

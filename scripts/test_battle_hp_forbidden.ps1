. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Assert-Start($condition, $message) { if (-not $condition) { throw $message } }
function Text($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skillJson = Text 'skill-rules'; $battleJson = Text 'battle-rules'; $rules = Text 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash $skillJson), '854_156383', $battleJson, (Hash $battleJson), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash $rules), '854_156383')
$skillItems = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, (Hash $rules), '854_156383')

function Fixture($restore = $false, $healthy = $false, $second = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width=12; Height=8; DefaultGroundType=2 }
    foreach($id in 1,2) {
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$u.Side=$id; $u.X=$id+2; $u.Y=2; $u.CharacterType=1
        $u.MaxHp=100000; $u.Hp=$(if($id -eq 1 -and -not $healthy){30000}else{100000})
        $u.Level=$u.SubRank=1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=1;Value=1000})
        $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    $listener=$catalog.CreateStatus(24202,1,1,9914202)
    $forbidden=$catalog.CreateStatus(24456,1,1,9914456)
    $cp.Units[0].Statuses.Add($listener); $cp.Units[0].Statuses.Add($forbidden)
    if($second){$cp.Units[0].Statuses.Add($catalog.CreateStatus(24003,1,1,9924003))}
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$skillItems)
    if($restore){
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$skillItems)
    }
    for($counter=1;$counter -le 5;$counter++){
        $result=$runner.Step(0,$counter)
        if($cp.Units[0].Statuses.ClassId -contains 24456){
            Assert-Start (@($result.Event.SkillCasts | Where-Object SkillEntityId -eq 10170).Count -eq 0) 'HP listener triggered while forbidden'
        }
    }
    Assert-Start ($cp.Units[0].Statuses.ClassId -notcontains 24456) 'Original forbidden status did not expire'
    $casts=@($cp.Receipts.Event.SkillCasts | Where-Object SkillEntityId -eq 10170)
    $expected=$(if($healthy -or $second){0}else{1})
    Assert-Start ($casts.Count -eq $expected) "Forbidden exit lost or duplicated HP recheck: $($casts.Count), expected=$expected"
    if($expected -eq 1){Assert-Start ($cp.Units[0].Statuses.ClassId -notcontains 24202) 'HP recheck lost automatic removal'}
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
$direct=Fixture
Assert-Start ($direct -eq (Fixture $true)) 'Forbidden exit changed after preparation restore'
[void](Fixture $false $true)
[void](Fixture $false $false $true)
Write-Output 'HP forbidden exit passed: original 24456 expiry -> 24202 -> 10170, one-shot, healthy/remaining-forbidden guards and deterministic restore.'

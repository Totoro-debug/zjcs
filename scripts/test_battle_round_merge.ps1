$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-MergeRule($name){[IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))}
function Hash-MergeRule($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
function Assert-RoundMerge($condition,$message){if(-not $condition){throw $message}}
$skills=Read-MergeRule 'skill-rules';$battle=Read-MergeRule 'battle-rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-MergeRule $skills),'854_156383',$battle,(Hash-MergeRule $battle),'854_156383')
foreach($id in 24037,24038,24045,24222,24472,24473,24474,24486,24640){
    $source=$catalog.CreateStatus($id,1,2,990000+$id,12950,1295,5,20,1,$null)
    Assert-RoundMerge ($source.RoundStartMergeMaxCountOnStackRound -and $source.RoundStartStackRoundCount -eq 1 -and [Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($source) -eq 3) "Original $id lost its initial duration-merge quota"
    Assert-RoundMerge ([Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartRemainingCount($source) -eq 3) 'Original custom icon count did not start with three remaining invocations'
    $source.StartRound=0;$source.RoundStartTriggeredCount=1
    [Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($source,1)
    Assert-RoundMerge ($source.CurDurationRound -eq 5 -and $source.StartRound -eq 1 -and $source.RoundStartTriggeredCount -eq 1 -and [Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($source) -eq 6) 'Duration renewal reset actual calls or failed to add the original three-call quota'
    Assert-RoundMerge ([Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartRemainingCount($source) -eq 5) 'Custom count used status layers or elapsed rounds instead of retained invocation quota'
}
$source=$catalog.CreateStatus(24640,1,2)
$source.StartRound=0
foreach($application in 1..12){[Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($source,0)}
Assert-RoundMerge ($source.CurDurationRound -eq 30 -and $source.RoundStartStackRoundCount -eq 13 -and [Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($source) -eq 39) 'Duration cap suppressed the original stack-round notification or counted status layers instead'
$source.RoundStartMaxCount=-1
Assert-RoundMerge ([Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($source) -lt 0) 'Unlimited quota became finite after duration stacking'
Assert-RoundMerge ([Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartRemainingCount($source) -eq -1) 'Original unlimited remaining count sentinel was lost'
$source=$catalog.CreateStatus(25077,1,1)
$source.IsAddDurationRound=$true;$source.DurationRound=$source.CurDurationRound=3;$source.StartRound=0
[Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($source,0)
Assert-RoundMerge ($source.RoundStartStackRoundCount -eq 1 -and [Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartMaximumCount($source) -eq 4) 'A component without MergeMaxCountOnStackRound gained more calls'
foreach($cap in 0,-1){
    $source=$catalog.CreateStatus(24640,1,2);$source.StartRound=0;$source.MaxAddDurationRound=$cap
    [Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($source,0)
    Assert-RoundMerge ($source.CurDurationRound -eq $cap) 'Official Math.Min duration cap was replaced by an unlimited fallback'
}
$rules=Read-MergeRule 'rules'
$formula=$catalog.CreateDamageFormula($rules,(Hash-MergeRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-MergeRule $rules),'854_156383')
function Invoke-MergeFormulaBattle($restore){
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1;$cp.MaxRound=12;$cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
    foreach($id in 1..2){
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id;$u.Side=$id;$u.CharacterType=1;$u.X=$id+1;$u.Y=2
        $u.MaxHp=$u.LastMaxHp=$u.Hp=10000000;$u.Level=$u.SubRank=1
        foreach($pair in @(@(1,9000),@(2,100),@(3,$u.MaxHp),@(4,$(if($id -eq 1){100}else{1})))){
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        $u.Turn.Id=$id;$u.Turn.Hp=$u.Hp;$u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    $source=$catalog.CreateStatus(24640,1,2,9924640,0,0,5,20,1,$null)
    # Formula-only fixture; source-aware instance selection is tested separately.
    $source.RoundStartRulesSupported=$true
    [Zjcs.Combat.Official.FightStatusLifecycle]::Initialize($source,0)
    $cp.Units[0].Statuses.Add($source)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    foreach($round in 1..7){
        if($restore){
            $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
            $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        }
        $runner.Step(0,$cp.Counter+1)|Out-Null
        $event=$cp.Receipts[$cp.Receipts.Count-1].Event
        $casts=@($event.SkillCasts|Where-Object SkillEntityId -eq 63280)
        Assert-RoundMerge ($casts.Count -eq [int]($round -le 6)) 'Extended formula fixture stopped at the old three-call maximum or exceeded six calls'
        if($round -le 6){
            $damage=@($event.DamageEvents|Where-Object SkillEntityId -eq 63280)
            Assert-RoundMerge ($casts[0].ActorId -eq 2 -and $damage.Count -eq 1 -and $damage[0].SourceId -eq 2 -and $damage[0].TargetId -eq 1 -and $damage[0].Damage -lt 0 -and $damage[0].AfterHp -lt $damage[0].BeforeHp) 'Extended formula fixture lost actual creator/victim damage'
        }
        if($round -eq 1){
            $active=$cp.Units[0].Statuses[0]
            [Zjcs.Combat.Official.FightStatusLifecycle]::ExtendDurationOnStack($active,$cp.Units[0].SkillState.Round)
            Assert-RoundMerge ($active.RoundStartTriggeredCount -eq 1 -and $active.CurDurationRound -eq 5 -and $active.StartRound -eq 1) 'Live extension reset the first actual invocation or used the wrong remaining duration'
        }
        if($round -lt 6){
            $remaining=[Zjcs.Combat.Official.FightStatusLifecycle]::RoundStartRemainingCount($cp.Units[0].Statuses[0])
            Assert-RoundMerge ($remaining -eq (6-$round)) 'Real creator damage or checkpoint reload lost the display invocation count'
        }
        Assert-RoundMerge (($cp.Units[0].Statuses.Count -eq 1) -eq ($round -lt 6)) 'Extended state was not removed immediately after its sixth actual invocation'
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
Assert-RoundMerge ((Invoke-MergeFormulaBattle $false) -eq (Invoke-MergeFormulaBattle $true)) 'Per-round save/restore changed duration quota or damage continuation'
Write-Output 'Round merge formula checks passed: nine original definitions, independent quota, retained calls, duration cap notifications, unlimited quota, literal caps; isolated real damage fixture executes six calls, removes its exhausted state and resumes exactly. Source-aware instance selection is covered by test_battle_status_sources.ps1.'

param([string]$ReceiptDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (Get-FightCombatSources)
$root = Split-Path -Parent $PSScriptRoot
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rule($name) { (Get-FileHash (Join-Path $root "unity/Assets/Official/Combat/$name.json")).Hash.ToLowerInvariant() }
function Check($ok, $message) { if (-not $ok) { throw $message } }
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Read-Rule 'skill-rules'),(Hash-Rule 'skill-rules'),'854_156383',(Read-Rule 'battle-rules'),(Hash-Rule 'battle-rules'),'854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Read-Rule 'rules'),(Hash-Rule 'rules'),'854_156383')
$formula = $catalog.CreateDamageFormula((Read-Rule 'rules'),(Hash-Rule 'rules'),'854_156383')
foreach ($direction in @(@(0,1),@(0,-1),@(1,0),@(-1,0))) {
    foreach ($mode in @('clear','large','landing','wall')) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
        $cp.StageId=1; $cp.MaxRound=5; $cp.RandomSeed=91
        $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=17;Height=17;DefaultGroundType=2}
        foreach ($id in @(1,2,3)) {
            $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
            $u.Id=$id; $u.Side=if($id -eq 1){1}else{2}; $u.CharacterType=$u.Side
            $u.Hp=$u.MaxHp=$u.LastMaxHp=1000000
            $u.X=8; $u.Y=8; $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=if($id -eq 1){100}else{1}
            $cp.Units.Add($u)
        }
        $actor=$cp.Units[0]
        $cp.Units[1].X+=2*$direction[0]; $cp.Units[1].Y+=2*$direction[1]
        $cp.Units[2].X+=4*$direction[0]; $cp.Units[2].Y+=4*$direction[1]
        if($mode -eq 'large') {
            foreach($i in 0..4) { $cp.Units[1].BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($i*$direction[1],$i*$direction[0])) }
        }
        if($mode -eq 'landing') { $cp.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(8+5*$direction[0],8+5*$direction[1])) }
        if($mode -eq 'wall') { $cp.Grid.Cells.Add([Zjcs.Combat.Official.FightBattleGridCell]@{X=8+$direction[0];Y=8+$direction[1];GroundType=2;Height=4}) }
        $skill=$catalog.CreateSkill(10680,0,10016,2,20,1,$null)
        $actor.Skills.Add($skill); $actor.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,3,-10))
        $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(2); $wave.UnitIds.Add(3); $cp.Waves.Add($wave)
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $before=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        $result=$runner.Step(0,1)
        $moves=@($result.Event.Movements | Where-Object MoveBehaviourId -eq 10682)
        $hits=@($result.Event.DamageEvents | Where-Object SkillEntityId -eq 10680)
        if($mode -eq 'clear') {
            Check ($moves.Count -eq 1 -and $moves[0].Path.Count -eq 6) 'Dash did not follow its five-cell straight path'
            Check ($actor.X -eq 8+5*$direction[0] -and $actor.Y -eq 8+5*$direction[1]) 'Dash landing differs'
            Check ($hits.Count -eq 2 -and @($hits.TargetId | Sort-Object -Unique).Count -eq 2) "Dash did not hit both intermediate enemies exactly once: direction=$direction hits=$($hits.Count) targets=$($hits.TargetId -join ',')"
            Check ($moves[0].MoveDuration -gt 0.099 -and $moves[0].MoveDuration -lt 0.101) 'Original movement timing changed'
            Check ($actor.SkillState.Slots[0].ReleasedTimes -eq 1) 'Dash release count differs'
        } else {
            Check ($moves.Count -eq 0 -and $hits.Count -eq 0 -and $actor.SkillState.Slots[0].ReleasedTimes -eq 0) 'Illegal dash consumed or released its skill'
        }
        $after=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        if ($mode -eq 'clear' -and $ReceiptDirectory) {
            $receiptPath = Join-Path $ReceiptDirectory ("dash-$($direction[0])-$($direction[1]).json")
            [IO.File]::WriteAllText($receiptPath, $after)
        }
        $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($before)
        $cold=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        [void]$cold.Step(0,1)
        Check ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored) -eq $after) 'Dash cold restore diverged'
        Check ($runner.Step(0,1).Replayed) 'Dash receipt replay failed'
    }
}
Write-Output 'Lightning dash passed: four directions, intermediate hits, large-body/landing/height rejection, original timing, cold restore and receipt replay.'

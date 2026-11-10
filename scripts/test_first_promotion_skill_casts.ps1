. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
Add-Type -Path (Get-FightCombatSources)
$root=Split-Path -Parent $PSScriptRoot
function Read-Input($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Input($name) { (Get-FileHash (Join-Path $root "unity/Assets/Official/Combat/$name.json")).Hash.ToLowerInvariant() }
function Check($ok,$message) { if(-not $ok) { throw $message } }
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Read-Input 'skill-rules'),(Hash-Input 'skill-rules'),'854_156383',(Read-Input 'battle-rules'),(Hash-Input 'battle-rules'),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Read-Input 'rules'),(Hash-Input 'rules'),'854_156383')
$formula=$catalog.CreateDamageFormula((Read-Input 'rules'),(Hash-Input 'rules'),'854_156383')
$rules=Read-Input 'rules' | ConvertFrom-Json
$ids=@($rules.profession_base | Where-Object Profession -in @(1,10,2,6,11,15) | ForEach-Object { $_.Skills; $_.FightUsedSkills }) | Sort-Object -Unique
$active=@($rules.skill | Where-Object { $_.ClassId -in $ids -and $items.ItemTypeFor($_.ClassId) -eq 100 })
Check ($ids.Count -eq 104 -and $active.Count -eq 57) 'Review the six-profession direct and internal skill scope'
function Unit($id,$side,$speed) {
    $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id=$id; $u.Side=$side; $u.CharacterType=1; $u.Level=20; $u.SubRank=1; $u.X=5; $u.Y=5
    $u.Hp=500000; $u.MaxHp=$u.LastMaxHp=1000000; $u.MoveDist=5
    $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=$speed
    foreach($prop in @(@(1,1000),@(2,10),@(3,1000000),@(32,100),@(33,100))) {
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$prop[0];Value=$prop[1]})
    }
    return $u
}
$cases=0
foreach($row in $active) {
    foreach($direction in @(@(0,1,0),@(0,-1,1),@(-1,0,2),@(1,0,3))) {
        $label="$($row.ClassId)/$($direction[2])"
        $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
        $cp.StageId=1; $cp.MaxRound=10; $cp.RandomSeed=73
        $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=12;DefaultGroundType=2}
        $actor=Unit 1 1 100; $enemy=Unit 2 2 1; $ally=Unit 3 1 1
        $actor.Direction=@(2,0,1,3)[$direction[2]]
        $skillProps=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
        foreach($prop in $catalog.CalculateSkillProps($row.EcEntityId,2,20,1).GetEnumerator()) {
            $skillProps.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$prop.Key;Value=$prop.Value})
        }
        $definition=$catalog.CreateSkill($row.EcEntityId,0,$row.ClassId,2,20,1,$skillProps)
        # Keep original damage, healing and child graphs in the persistence comparison.
        # Exact formula and trigger expectations have their own original-data suites.
        $distance=@($definition.Range | Where-Object { $_.X*$direction[1] -eq 0 -and $_.Y*$direction[0] -eq 0 -and $_.X*$direction[0]+$_.Y*$direction[1] -gt 0 } |
            ForEach-Object { [Math]::Abs($_.X)+[Math]::Abs($_.Y) } | Sort-Object | Select-Object -First 1)
        $distance=if($distance.Count){[int]$distance[0]}else{1}
        $enemy.X+=$direction[0]*$distance; $enemy.Y+=$direction[1]*$distance
        $ally.X-=$direction[0]; $ally.Y-=$direction[1]
        foreach($u in @($actor,$enemy,$ally)) { $cp.Units.Add($u) }
        $actor.Skills.Add($definition); $actor.SkillState.Slots.Add($catalog.CreateSkillSlot($definition,3,-10))
        $wave=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $before=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        $result=$runner.Step(0,1)
        $casts=@($result.Event.SkillCasts | Where-Object { $_.ActorId -eq 1 -and $_.Slot -eq 0 })
        Check ($casts.Count -eq 1 -and $actor.SkillState.Slots[0].ReleasedTimes -eq 1) "Missing or duplicate equipped release: $label"
        $cast=$casts[0]
        Check ($cast.SkillItemId -eq $row.ClassId -and $actor.Skills[0].EntityId -eq $row.EcEntityId) "Equipped source was mutated: $label"
        $binding=$items.Required($row.ClassId)
        $expected=if($binding.DirectionEntityIds.ContainsKey($direction[2])){$binding.DirectionEntityIds[$direction[2]]}else{$row.EcEntityId}
        Check ($cast.SkillEntityId -eq $expected -and $cast.Presentation -ne $null) "Original entity or presentation snapshot differs: $label"
        Check ($runner.Step(0,1).Replayed -and $actor.SkillState.Slots[0].ReleasedTimes -eq 1) "Receipt consumed the release twice: $label"
        $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($before)
        $cold=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
        $again=$cold.Step(0,1)
        Check ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored) -eq [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)) "Cold restore changed the complete settled checkpoint: $label"
        $cases++
    }
}
Write-Output "First-promotion cast routing passed: 50 active and 7 internal skills, $cases cardinal casts with original values, one equipped release, receipt replay and identical cold-restored checkpoints. Exact numeric expectations and native visual coverage are checked separately."

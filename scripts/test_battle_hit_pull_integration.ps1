. (Join-Path $PSScriptRoot 'test_battle_hit_move.ps1')

function Invoke-ConfirmedPull([bool]$restore, [bool]$statusOnly) {
    $skill = New-MoveSkill 4 -1 $true $false 0
    $hit = $skill.Hits[0]
    $hit.DamageId = 12877
    $hit.HitMove.MoveBehaviourId = 12878
    if ($statusOnly) { $hit.ActionType = 0; $hit.HasCustomDamage = $false }
    $actor = New-Unit 1 1 0 0 1000
    $target = New-Unit 2 2 4 0 5000
    $result = Invoke-MoveBattle $skill $actor $target $restore
    $settled = $result.Checkpoint.Units | Where-Object Id -eq 2 | Select-Object -First 1
    Assert-Move ($settled.X -eq 1 -and $result.Event.Movements.Count -eq 1) "Confirmed pull was not settled by the executor: x=$($settled.X), movements=$($result.Event.Movements.Count), restore=$restore, statusOnly=$statusOnly"
    $movement = $result.Event.Movements[0]
    Assert-Move ($movement.UnitId -eq 2 -and $movement.MoveBehaviourId -eq 12878 -and
        $movement.Path.Count -eq 4 -and $movement.Path[3].X -eq 1) 'Pull movement receipt lost its path or action'
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($result.Checkpoint)
}

foreach ($statusOnly in @($false, $true)) {
    $normal = Invoke-ConfirmedPull $false $statusOnly
    $restored = Invoke-ConfirmedPull $true $statusOnly
    Assert-Move ($normal -eq $restored) 'Restoring the checkpoint changed the confirmed pull'
}
$original = Invoke-GraphMove 12875 12878 4
$originalRestored = Invoke-GraphMove 12875 12878 4 -1 $true
Assert-Move ($original.Event.Movements.Count -eq 1 -and
    $original.Event.Movements[0].Path[-1].X -eq 1 -and
    $original.Event.Movements[0].MoveBehaviourId -eq 12878) 'Official 12875 -> 12876 -> 12877 did not execute its pull'
Assert-Move ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($original.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($originalRestored.Checkpoint)) 'Original pull changed after checkpoint restore'
$selectedTarget = Invoke-GraphMove 59970 59973 4
Assert-Move ($selectedTarget.Event.Movements.Count -eq 0) 'A target-relative pull moved the selected target toward itself'
$method = [Zjcs.Combat.Official.FightBattleExecutor].GetMethod('ApplyHitMove',
    [Reflection.BindingFlags]'Instance,NonPublic')
$directions = @(@(0,-1),@(-1,0),@(0,1),@(1,0))
for ($facing = 0; $facing -lt 4; $facing++) {
    $actor = New-Unit 1 1 1 1 1000
    $target = New-Unit 2 2 5 6 5000
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 21
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 12; DefaultGroundType = 2 }
    $checkpoint.Units.Add($actor); $checkpoint.Units.Add($target)
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $moveFormula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new())
    $skill = New-MoveSkill 1 1 $false $false 0
    $hit = $skill.Hits[0]; $hit.DamageId = 13214
    $hit.HitMove.MoveBehaviourId = 13215; $hit.HitMove.CancelDiagonally = $true
    if ($facing -eq 3) {
        $hit.HitMove.Distance = 2
        $checkpoint.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(7,6))
    }
    $cast = [Zjcs.Combat.Official.FightBattleSkillCastPresentation]@{ FacingIndex = $facing }
    $events = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleMovementEvent]]::new()
    $method.Invoke($executor, @($actor, $target, $target, $skill, $hit, $cast, $events)) | Out-Null
    Assert-Move ($target.X -eq (5 + $directions[$facing][0]) -and
        $target.Y -eq (6 + $directions[$facing][1]) -and $events.Count -eq 1) "Cast-facing push failed: $facing"
}
$actor = New-Unit 1 1 1 1 1000
$target = New-Unit 2 2 5 6 5000
$anchor = New-Unit 3 2 4 6 5000
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 21
$checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 12; DefaultGroundType = 2 }
$checkpoint.Units.Add($actor); $checkpoint.Units.Add($target); $checkpoint.Units.Add($anchor)
$executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $moveFormula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new())
$skill = New-MoveSkill 1 2 $false $false 1
$hit = $skill.Hits[0]; $hit.DamageId = 15984; $hit.HitMove.MoveBehaviourId = 15983
$hit.HitMove.MoveDuration = 0.32
$cast = [Zjcs.Combat.Official.FightBattleSkillCastPresentation]@{ FacingIndex = 3 }
$events = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleMovementEvent]]::new()
$method.Invoke($executor, @($actor, $target, $anchor, $skill, $hit, $cast, $events)) | Out-Null
Assert-Move ($target.X -eq 7 -and $target.Y -eq 6 -and $events.Count -eq 1 -and
    $events[0].Path.Count -eq 3 -and [Math]::Abs($events[0].MoveDuration - 0.32) -lt 0.000001) `
    'Positive-distance move ignored the selected skill target or lost its configured segment duration'

function Invoke-ConfirmedSide($sourceX, $sourceY, $anchorX, $anchorY, $targetX, $targetY,
    [bool]$away, [bool]$blockPositive = $false, [bool]$blockBody = $false) {
    $actor = New-Unit 1 1 $sourceX $sourceY 1000
    $target = New-Unit 2 2 $targetX $targetY 5000
    $anchor = New-Unit 3 2 $anchorX $anchorY 5000
    $checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $checkpoint.StageId = 1; $checkpoint.MaxRound = 10; $checkpoint.RandomSeed = 21
    $checkpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 12; DefaultGroundType = 2 }
    $checkpoint.Units.Add($actor); $checkpoint.Units.Add($target); $checkpoint.Units.Add($anchor)
    if ($blockPositive) {
        $checkpoint.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new($targetX, ($targetY + 1)))
    }
    if ($blockBody) {
        $target.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
        $checkpoint.Grid.BlockedPositions.Add([Zjcs.Combat.Official.FightGridPoint]::new(($targetX + 1), ($targetY + 1)))
    }
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint, $moveFormula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new())
    $skill = New-MoveSkill 1 1 $away $false 2
    $hit = $skill.Hits[0]; $hit.DamageId = if ($away) { 56233 } else { 56524 }
    $hit.HitMove.MoveBehaviourId = if ($away) { 56234 } else { 56523 }
    $cast = [Zjcs.Combat.Official.FightBattleSkillCastPresentation]@{ FacingIndex = 3 }
    $events = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleMovementEvent]]::new()
    $method.Invoke($executor, @($actor, $target, $anchor, $skill, $hit, $cast, $events)) | Out-Null
    return @($target.X, $target.Y, $events.Count)
}
Assert-Move ((Invoke-ConfirmedSide 2 4 8 4 5 6 $true) -join ',' -eq '5,7,1') 'Side move did not move away from the selected-target line'
Assert-Move ((Invoke-ConfirmedSide 2 4 8 4 5 6 $false) -join ',' -eq '5,5,1') 'Side move did not approach the selected-target line'
Assert-Move ((Invoke-ConfirmedSide 2 4 8 4 5 4 $true) -join ',' -eq '5,5,1') 'On-line side move did not prefer the positive axis'
Assert-Move ((Invoke-ConfirmedSide 2 4 8 4 5 4 $true $true) -join ',' -eq '5,3,1') 'On-line side move did not fall back to the opposite axis'
Assert-Move ((Invoke-ConfirmedSide 2 4 8 4 5 6 $true $true) -join ',' -eq '5,6,0') 'Off-line blocked side move incorrectly reversed direction'
Assert-Move ((Invoke-ConfirmedSide 2 4 8 4 5 6 $true $false $true) -join ',' -eq '5,6,0') 'Side move ignored a blocked body cell'
Assert-Move ((Invoke-ConfirmedSide 2 2 8 8 5 5 $true) -join ',' -eq '5,6,1') 'Diagonal line did not use the confirmed horizontal-major-axis tie'
Assert-Move ((Invoke-ConfirmedSide 4 2 4 8 6 5 $true) -join ',' -eq '7,5,1') 'Vertical side move did not move away from the line'
Assert-Move ((Invoke-ConfirmedSide 4 2 4 8 6 5 $false) -join ',' -eq '5,5,1') 'Vertical side move did not approach the line'
Assert-Move ((Invoke-ConfirmedSide 8 8 2 2 5 6 $true) -join ',' -eq '5,7,1') 'Reverse diagonal side move selected the wrong side'
Write-Output 'Confirmed pull executor, status-only hit, movement receipt and checkpoint restore passed.'

. (Join-Path $PSScriptRoot 'test_battle_hit_links.ps1')
$cast=$step.Event.SkillCasts[0]
Assert-Link ($cast.HitSelections.Count -eq 7) 'Production cast did not record seven real selections'
for($i=0;$i -lt 7;$i++) {
 $record=$cast.HitSelections[$i]
 Assert-Link ($record.Sequence -eq ($i+1) -and $record.HitEntityId -eq 11 -and $record.RandomTargetGroupId -gt 0 -and [Math]::Abs($record.Delay-(0.37+$i/10.0)) -lt 0.00001) 'Repeated hit identity/timing not scoped to its actual cast'
 Assert-Link ($record.Targets.Count -eq 1 -and $record.Targets[0].TargetId -eq 2 -and $record.SelectedGridPositions.Count -eq 1 -and $record.SelectedGridPositions[0].X -eq 5 -and $record.EffectCenters[0].X -eq 5) 'Real selection points or target missing'
}
$enemy.X=9;$enemy.Y=8;$source.X=7;$source.Direction=1
Assert-Link ($cast.HitSelections[0].Targets[0].TargetX -eq 5 -and $cast.HitSelections[0].Targets[0].TargetY -eq 4 -and $cast.HitSelections[0].SourceX -eq 4) 'Later roster movement changed a captured selection'
$restoredSelections=$saved.Receipts[0].Event.SkillCasts[0].HitSelections
Assert-Link ($restoredSelections.Count -eq 7 -and $restoredSelections[6].Sequence -eq 7 -and [Math]::Abs($restoredSelections[6].Delay-0.97) -lt 0.00001 -and $restoredSelections[0].Targets[0].TargetX -eq 5) 'Checkpoint lost selection identity/positions/timing'
# Area selection must preserve each body cell and its delay, not merge by target ID.
$enemy.X=5;$enemy.Y=4;$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,1))
$area=[Zjcs.Combat.Official.FightBattleHitDefinition]@{EntityId=12;Anchor=0;TargetMode=1}
foreach($y in 0..1) {
 $position=[Zjcs.Combat.Official.FightGridPoint]::new(0,$y);$area.Scope.Add($position)
 $area.ScopeEntries.Add([Zjcs.Combat.Official.FightBattleHitScopeEntry]@{Position=$position;Delay=($y/10.0)})
}
$context=[Zjcs.Combat.Official.FightHitTargetSelectionContext]@{Source=$source;Units=$state.Units;Skill=$skill;Hit=$area;Primary=$enemy;Targets=([Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new());Cast=$cast.Presentation;Random=([Zjcs.Combat.Official.FightRandomStream]::new(7))}
$context.Targets.Add($enemy)
$selector=[Zjcs.Combat.Official.FightHitTargetSelectors]::new()
$selection=$selector.For($area).Select($context)
$captured=[Zjcs.Combat.Official.FightBattleHitSelectionSnapshot]::Capture(1,$area,$selection,$cast.Presentation)
Assert-Link ($captured.Targets.Count -eq 2 -and $captured.Targets[0].HasGridPosition -and $captured.Targets[1].HasGridPosition -and $captured.Targets[0].GridY -eq 4 -and $captured.Targets[1].GridY -eq 5 -and [Math]::Abs($captured.Targets[1].ScopeDelay-0.1) -lt 0.00001) 'Area receipt merged cells or lost their order/delay'
$selection.Targets.Clear()
Assert-Link ($captured.Targets.Count -eq 2 -and $captured.EffectCenters.Count -eq 1) 'Selection scratch mutation affected saved receipt'
# Legacy payloads omit the new field; their existing damage must remain recoverable.
$json=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($saved)
$object=$json|ConvertFrom-Json -Depth 100
foreach($receipt in $object.checkpoint.Receipts){foreach($c in $receipt.Event.SkillCasts){$c.PSObject.Properties.Remove('HitSelections')}}
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(($object|ConvertTo-Json -Depth 100 -Compress))
Assert-Link ($legacy.Receipts[0].Event.SkillCasts[0].HitSelections.Count -eq 0 -and $legacy.Receipts[0].Event.DamageEvents.Count -eq 7 -and $legacy.Units[1].Hp -eq $saved.Units[1].Hp) 'Legacy payload lost settled damage or invented selection receipts'
Write-Output 'Hit selection snapshots passed: production repeated occurrences, actual random points, frozen source/targets, independent body cells and delays, checkpoint and legacy compatibility.'
# Original client scopes are transformed around the hit grid, including a selected body edge.
$source.X=4;$source.Y=4;$enemy.X=5;$enemy.Y=4;$enemy.BodyOffsets.Clear()
$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0));$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$neighbor=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=3;Side=2;CharacterType=2;X=7;Y=4;Hp=100;MaxHp=100}
$state.Units.Add($neighbor)
$edge=[Zjcs.Combat.Official.FightBattleHitDefinition]@{EntityId=13;RandomTargetGroupId=7;RandomCount=1;RandomTargetAnchor=1;TargetMode=1}
$edge.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,0))
foreach($x in 0..1){$edge.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,0))}
$context.Source=$source;$context.Hit=$edge;$context.Primary=$enemy;$context.Cast=$cast.Presentation;$context.Grid=$state.Grid
$edgeSelection=([Zjcs.Combat.Official.FightHitTargetSelectors]::new()).For($edge).Select($context)
Assert-Link ($edgeSelection.SelectedGridPositions[0].X -eq 6 -and $edgeSelection.EffectCenters[0].X -eq 6) 'Random body edge snapped back to entity centre'
Assert-Link ($edgeSelection.Targets.Count -eq 2 -and $edgeSelection.Targets[0].Id -eq 2 -and $edgeSelection.Targets[1].Id -eq 3 -and $edgeSelection.Occurrences[0].GridPosition.X -eq 6 -and $edgeSelection.Occurrences[1].GridPosition.X -eq 7) 'Damage area diverged from original client hit-grid projection'
# An allowed empty anchor can still cover a neighbor in its child Scope.
$edge.RandomTargetGroupId=8;$edge.RandomTargetScope.Clear();$edge.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,1))
$edge.RandomTargetAllowEmptyScope=$true;$edge.Scope.Clear();$edge.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0));$edge.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,-1))
$emptySelection=([Zjcs.Combat.Official.FightHitTargetSelectors]::new()).For($edge).Select($context)
Assert-Link ($emptySelection.SelectedGridPositions[0].X -eq 6 -and $emptySelection.SelectedGridPositions[0].Y -eq 5 -and $emptySelection.Targets.Count -eq 1 -and $emptySelection.Targets[0].Id -eq 2 -and $emptySelection.Occurrences[0].GridPosition.Y -eq 4) 'Allowed empty hit grid discarded its real neighboring damage'
Write-Output 'Client hit-grid projection passed: large-body edge, adjacent target, exact damage grids and empty-anchor neighboring scope.'

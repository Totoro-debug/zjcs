param([string]$InputPath,[string]$NativeSource,[string]$OriginalFilesJson,[string]$FixturePath='scripts/fixtures/bbq-reference.json')
$ErrorActionPreference='Stop'
$files=@($OriginalFilesJson | ConvertFrom-Json)
Add-Type -Path ($files+@($NativeSource))
$inputData=Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json
function Ints($values){$list=[System.Collections.Generic.List[int]]::new();foreach($value in $values){$list.Add([int]$value)};return ,$list}
[Common.Parsers]::GameSettings.BBQSetting.BurntScoreRate=$inputData.tables.settings.BurntScoreRate
foreach($row in $inputData.tables.formulas){
 $value=[Common.ActivityMiniGameBBQFormulaInfo]::new()
 foreach($key in 'Id','Score','WaitTime'){$value.$key=$row.$key}
 $value.Foods=Ints $row.Foods;[Common.Parsers]::Instance.ActivityMiniGameBBQFormulaInfoParser.Items.Add($value.Id,$value)
}
foreach($row in $inputData.tables.foods){
 $value=[Common.ActivityMiniGameBBQFoodInfo]::new();$value.Id=$row.Id;$value.Time=$row.Time
 [Common.Parsers]::Instance.ActivityMiniGameBBQFoodInfoParser.Items.Add($value.Id,$value)
}
$stages=[System.Collections.Generic.List[object]]::new()
foreach($row in $inputData.tables.stages){
 $value=[Common.ActivityMiniGameBBQLevelInfo]::new()
 foreach($key in 'ActivityId','Order','AllVisitorCount','PassScore'){$value.$key=$row.$key}
 foreach($key in 'Npcs','BBQFormulas','Weights'){$value.$key=Ints $row.$key}
 $value.BBQFormulasWeightRandomSelector=[Common.WeightRandomSelector[int]]::new($value.BBQFormulas,$value.Weights);$stages.Add($value)
}
$sequences=[System.Collections.Generic.List[object]]::new()
foreach($stage in $stages){foreach($seed in 1,12345,-2147483648){
 $random=[Framework.SystemRandom]::new();$random.SetSeed($seed);[Common.RandomUtils]::Default=$random
 $session=[BBQGameSession]::new();$session.Begin($stage,(Ints @($stage.Npcs | Select-Object -Unique)))
 $steps=[System.Collections.Generic.List[object]]::new()
 for($i=0;$i -lt 45;$i++){
  # Occupy every candidate once, exercise exhaustion, rollback and release.
  if($i -eq 5){foreach($npc in $session.DistinctNpcIds){$session.OccupyNpc($npc)}}
  if($i -ge 6 -and $i%3 -eq 0){foreach($npc in @($session.OccupiedNpcIds)){$session.ReleaseNpc($npc);break}}
  $before=@($session.OccupiedNpcIds);$npc=0;$formula=0;$ok=$session.TryBeginVisitorAssignment([ref]$npc,[ref]$formula)
  $rollback=$ok -and $i%4 -eq 0
  if($ok){if($rollback){$session.RollbackVisitorAssignment($npc)}else{$session.CommitVisitorSpawned()}}
  $steps.Add(@{occupied=$before;ok=$ok;npc=$npc;formula=$formula;rollback=$rollback;spawned=$session.VisitorsSpawned})
 }
 $sequences.Add(@{activity=$stage.ActivityId;order=$stage.Order;seed=$seed;steps=@($steps)})
}}
$scores=[System.Collections.Generic.List[object]]::new()
foreach($row in $inputData.tables.formulas){
 $variants=[System.Collections.Generic.List[object]]::new();$variants.Add(@($row.Foods))
 $reverse=@($row.Foods);[Array]::Reverse($reverse);$variants.Add($reverse)
 $wrong=@($row.Foods);$wrong[$wrong.Count-1]=99999;$variants.Add($wrong)
 $variants.Add(@());$variants.Add($null)
 foreach($values in $variants){foreach($cook in 0,1,2,3){
  $foods=$null;if($null -ne $values){$foods=Ints $values}
  $session=[BBQGameSession]::new();$session.ApplySuccessfulOrder($row.Id,([Enum]::ToObject([BBQCookState],$cook)),$foods)
  $scores.Add(@{formula=$row.Id;foods=$values;cook=$cook;matches=[BBQGameSession]::OrderMatchesFormula($row.Id,$foods);score=$session.CurrentScore;success=$session.SuccessOrderCount})
 }}
}
$timers=[System.Collections.Generic.List[object]]::new()
foreach($row in $inputData.tables.formulas){foreach($pattern in 0,1){
 $skewer=[NativeSkewer]::new();$skewer.mFoodIds=Ints $row.Foods;$skewer.mBurntTimeRate=$inputData.tables.settings.BurntTimeRate
 $skewer.BeginGrillingOnGrill();$frames=[System.Collections.Generic.List[object]]::new()
 $total=$skewer.mTotalCookSeconds
 $deltas=if($pattern -eq 0){@(0.016,($total-0.016),0,($total*1.25-0.001),0.001,1)}else{@(($total*10),0,($total*10),1)}
 $frame=0
 foreach($delta in $deltas){foreach($mode in 0,1,2){
  $paused=$mode -eq 1;$drag=$mode -eq 2
  $skewer.Frame($delta,$paused,$drag)
  $frames.Add(@{delta=$delta;paused=$paused;drag=$drag;cook=[int]$skewer.mCookState;grill=$skewer.mGrillElapsed;post=$skewer.mPostCookedElapsed})
 };$frame++}
 $timers.Add(@{foods=@($row.Foods);total=$total;frames=@($frames)})
}}
$ingredients=[System.Collections.Generic.List[object]]::new()
foreach($max in 0,1,4){foreach($mounts in 0,2,4){foreach($flow in 0,1,2){foreach($cook in 0,1,2){foreach($count in 0,1,2,4){
 $skewer=[NativeSkewer]::new();$skewer.mMaxFoodCount=$max;$skewer.mFlowState=$flow;$skewer.mCookState=$cook
 for($i=0;$i -lt $mounts;$i++){$skewer.mFoodMountPoints.Add([UnityEngine.Transform]::new())}
 for($i=0;$i -lt $count;$i++){$skewer.mFoodIds.Add(21)}
 $reason='';$ok=$skewer.TryAddFood(22,$null,[ref]$reason)
 $ingredients.Add(@{max=$max;mounts=$mounts;flow=$flow;cook=$cook;initial=$count;ok=$ok;result=@($skewer.mFoodIds)})
}}}}}
$patience=[System.Collections.Generic.List[object]]::new()
foreach($wait in -1,0,1,35){
 $visitor=[BBQNpcSlot]::new();$visitor.Start(8406,$wait);$frames=[System.Collections.Generic.List[object]]::new()
 foreach($row in @(@{delta=100;ready=$false;paused=$false},@{delta=100;ready=$true;paused=$true},@{delta=0.5;ready=$true;paused=$false},@{delta=0.5;ready=$true;paused=$false},@{delta=100;ready=$true;paused=$false},@{delta=100;ready=$true;paused=$false})){
  $visitor.mVisitorReady=$row.ready;$visitor.Frame($row.delta,$row.paused)
  $frames.Add(@{delta=$row.delta;ready=$row.ready;paused=$row.paused;remaining=$visitor.mPatienceRemaining;total=$visitor.mPatienceTotal;expired=$visitor.Expired})
 }
 $patience.Add(@{wait=$wait;frames=@($frames)})
}
$limits=[System.Collections.Generic.List[object]]::new();$normal=[NativeNormal]::new();$endless=[NativeEndless]::new()
foreach($all in -1,0,1,9){foreach($spawned in 0,1,9){foreach($max in 0,1,3){
 $level=[Common.ActivityMiniGameBBQLevelInfo]::new();$level.AllVisitorCount=$all
 $session=[BBQGameSession]::new();$session.Begin($level,(Ints @(1,2)));$session.VisitorsSpawned=$spawned
 $bbqHost=[NativeHost]::new();$bbqHost.Session=$session;$bbqHost.NpcSlotsView=[BBQNpcSlotsView]::new()
 $limits.Add(@{all=$all;spawned=$spawned;max=$max;normal=$normal.GetInitialVisitorSlotCount($max,3,2,$level);endless=$endless.GetInitialVisitorSlotCount($max,3,2,$level);can=$normal.CanSpawnMoreVisitors($level,$spawned);exhausted=[NativeNormal]::Exhausted($bbqHost)})
}}}
$inputData | Add-Member -NotePropertyName visitor_cases -NotePropertyValue @($sequences)
$inputData | Add-Member -NotePropertyName score_cases -NotePropertyValue @($scores)
$inputData | Add-Member -NotePropertyName timer_cases -NotePropertyValue @($timers)
$inputData | Add-Member -NotePropertyName ingredient_cases -NotePropertyValue @($ingredients)
$inputData | Add-Member -NotePropertyName patience_cases -NotePropertyValue @($patience)
$inputData | Add-Member -NotePropertyName limit_cases -NotePropertyValue @($limits)
$inputData | ConvertTo-Json -Depth 35 -Compress | Set-Content -LiteralPath $FixturePath -Encoding utf8NoBOM
Write-Output "Pinned BBQ C# reference: $($stages.Count) stages, $($scores.Count) score cases, $($timers.Count) cooking sequences."

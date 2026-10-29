param([string]$FixturePath='scripts/fixtures/jigsaw-reference.json',[string]$PythonExecutable='python')
$ErrorActionPreference='Stop'
$sourceRoot=Join-Path $PSScriptRoot '../.tools/source-packages/official-code'
$files=@('ActivityMinigameJigsawInfoParser','JigsawUtils') | ForEach-Object {Join-Path $sourceRoot "common-source/Common/$_.cs"}
$files+=@('JigsawLogicGrids','JigsawUIUtils') | ForEach-Object {Join-Path $sourceRoot "client-source/$_.cs"}
Add-Type -Path ($files+(Join-Path $PSScriptRoot 'fixtures/jigsaw_reference_harness.cs'))
$inputCode=@'
import json,hashlib
from official_tables import Constructors
from prepare_map_regions import Regions
e=Regions();c=Constructors()
_,obj=e.locate('Assets/Config/Luban/activity_festival_minigame_jigsaw_level.g.bytes')
raw=obj.read().m_Script.encode('utf8','surrogateescape')
print(json.dumps(dict(rows=c.table(raw,'TbActivityMinigameJigsawInfo'),source=dict(e.apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest()))))
'@
$inputData=(& $PythonExecutable -c $inputCode) | ConvertFrom-Json
if($LASTEXITCODE -ne 0){throw 'Pinned jigsaw table decoding failed'}
$parser=[Common.Parsers]::Instance.ActivityMinigameJigsawInfoParser
$items=[System.Collections.Generic.List[Common.ActivityMinigameJigsawInfo]]::new()
foreach($row in $inputData.rows){
 $item=[Common.ActivityMinigameJigsawInfo]::new()
 foreach($name in 'Id','Level','Order','RowNum','ColumnNum','UnlockHour'){$item.$name=$row.$name}
 $item.InitialLayout=[System.Collections.Generic.List[int]]::new();foreach($n in $row.InitialLayout){$item.InitialLayout.Add($n)}
 $items.Add($item)
}
# Reversed input checks that the original parser supplies sorted predecessor links.
$items.Reverse();$parser.Items=$items.ToArray();$parser.PostParse()
$allowed=[System.Collections.Generic.List[int]]::new();foreach($n in 0,1,2){$allowed.Add($n)}
[Common.Parsers]::GameSettings.JigsawFastCompleteSetting.AllowedDifficulties=$allowed
$epoch=[DateTime]::SpecifyKind([DateTime]'2020-01-01',[DateTimeKind]::Utc)
[ActivityDataManager]::Instance.Data.StartDate=$epoch
$swaps=[System.Collections.Generic.List[object]]::new();$conditions=[System.Collections.Generic.List[object]]::new()
foreach($item in $items){
 $grid=[JigsawLogicGrids]::new();$grid.Init($item);$steps=[System.Collections.Generic.List[object]]::new()
 $random=[Random]::new($item.Id*10000+$item.Level*100+$item.Order)
 $pairs=@(@(0,0),@(-1,0),@(0,$item.InitialLayout.Count),@(0,($item.InitialLayout.Count-1)))
 foreach($pair in $pairs){$ok=$grid.TrySwap($pair[0],$pair[1]);$steps.Add(@{x=$pair[0];y=$pair[1];ok=$ok;values=@($grid.Grids)})}
 for($i=0;$i -lt 12;$i++){
  $x=$random.Next($item.InitialLayout.Count);$y=$random.Next($item.InitialLayout.Count)
  $ok=$grid.TrySwap($x,$y);$steps.Add(@{x=$x;y=$y;ok=$ok;values=@($grid.Grids)})
 }
 # Solve by real swaps, recording every intermediate grid from unchanged C#.
 for($i=0;$i -lt $grid.Grids.Count;$i++){
  if($grid.Grids[$i] -ne $i){$j=0;while($grid.Grids[$j] -ne $i){$j++};$ok=$grid.TrySwap($i,$j);$steps.Add(@{x=$i;y=$j;ok=$ok;values=@($grid.Grids)})}
 }
 $swaps.Add(@{activity=$item.Id;level=$item.Level;order=$item.Order;steps=@($steps)})
 foreach($mode in 'none','previous','this','all'){
  $player=[Common.Player]::new();[MeManager]::MainPlayer=$player
  foreach($candidate in $items){
   if(($mode -eq 'all') -or ($mode -eq 'previous' -and $candidate -eq $item.LastInfo) -or ($mode -eq 'this' -and $candidate -eq $item)){
    $orders=[Common.PlayerAwardedRecordsSubValueSubValueWrap]$null
    [Common.JigsawUtils]::GetAndCreateCompletedOrder($player,$candidate.Id,$candidate.Level,[ref]$orders)
    [void]$orders.Add($candidate.Order)
   }
  }
  foreach($offset in -1,0,1){
   $seconds=[double]$item.UnlockHour*3600+$offset;[TimeManager]::Instance.Now=$epoch.AddSeconds($seconds)
   $records=@{};foreach($activity in $player.JigsawtData.AwardedRecords.Keys){
    $levels=@{};foreach($level in $player.JigsawtData.AwardedRecords[$activity].Keys){$levels["$level"]=@($player.JigsawtData.AwardedRecords[$activity][$level])};$records["$activity"]=$levels
   }
   $conditions.Add(@{activity=$item.Id;level=$item.Level;order=$item.Order;records=$records;now=$seconds;
    level_unlocked=[Common.JigsawUtils]::IsUnlock($player,$item.Id,$item.Level);
    unlocked=[Common.JigsawUtils]::IsUnlock($player,$item.Id,$item.Level,$item.Order);
    complete=[Common.JigsawUtils]::IsJigsawCompleted($player,$item.Id,$item.Level,$item.Order);
    level_complete=[Common.JigsawUtils]::IsJigsawCompleted($player,$item.Id,$item.Level);
    challenge=[JigsawUIUtils]::IsNeedChallenge($item.Id,$item.Level,$item.Order);
    fast=@([JigsawUIUtils]::GetFastCompletableOrders($item.Id,$item.Level))})
  }
 }
}
$hashes=@{};foreach($path in $files){$hashes[[IO.Path]::GetRelativePath($sourceRoot,$path).Replace('\','/')]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
@{manifest='854_156383';sources=$hashes;table_source=$inputData.source;rows=$inputData.rows;swaps=@($swaps);conditions=@($conditions)} | ConvertTo-Json -Depth 35 -Compress | Set-Content -LiteralPath $FixturePath -Encoding utf8NoBOM
Write-Output "Pinned jigsaw C# reference: $($items.Count) stages, $($conditions.Count) condition cases."

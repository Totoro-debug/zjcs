param([string]$FixturePath='scripts/fixtures/ice-cream-reference.json',[string]$PythonExecutable='python')
$ErrorActionPreference='Stop'
$sourceRoot=Join-Path $PSScriptRoot '../.tools/source-packages/official-code'
$files=@('ActivityMiniGameIceCreamDataExt','WeightRandomSelector') | ForEach-Object {Join-Path $sourceRoot "common-source/Common/$_.cs"}
$files+=@('SystemRandom','IRandom') | ForEach-Object {Join-Path $sourceRoot "framework-firstpass-source/Framework/$_.cs"}
$files+=Join-Path $sourceRoot 'client-source/IceCreamUIUtils.cs'
Add-Type -Path ($files+(Join-Path $PSScriptRoot 'fixtures/ice_cream_reference_harness.cs'))
$inputCode=@'
import json,re,hashlib
from official_tables import Constructors
from prepare_map_regions import Regions
e=Regions();c=Constructors();tables={};sources=[]
mapping={m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',c.source('Tables'))}
for key,name in [('stages','ActivityMiniGame/activity_minigame_ice_cream'),('formulas','ActivityMiniGame/ice_cream_formula')]:
 _,obj=e.locate('Assets/Config/Luban/'+name+'.g.bytes')
 raw=obj.read().m_Script.encode('utf8','surrogateescape')
 tables[key]=c.table(raw,mapping[name]);sources.append(dict(e.apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest()))
print(json.dumps(dict(tables=tables,sources=sources)))
'@
$inputData=(& $PythonExecutable -c $inputCode) | ConvertFrom-Json
if($LASTEXITCODE -ne 0){throw 'Pinned ice cream table decoding failed'}
foreach($row in $inputData.tables.formulas){
 $item=[Common.IceCreamFormulaInfo]::new()
 foreach($name in 'Id','IceCanister','OrnamentType','Score'){$item.$name=$row.$name}
 $item.IceCreams=[System.Collections.Generic.List[int]]::new();foreach($id in $row.IceCreams){$item.IceCreams.Add($id)}
 [Common.Parsers]::Instance.IceCreamFormulaInfoParser.Items.Add($item.Id,$item)
}
$stages=[System.Collections.Generic.List[Common.ActivityMiniGameIceCreamInfo]]::new()
foreach($row in $inputData.tables.stages){
 $item=[Common.ActivityMiniGameIceCreamInfo]::new()
 foreach($name in 'ActivityId','Order','AllVisitorCount','QueueVisitorCount','UnlockHour'){$item.$name=$row.$name}
 foreach($name in 'Npcs','IceCreamFormulas','Weights'){
  $item.$name=[System.Collections.Generic.List[int]]::new();foreach($id in $row.$name){$item.$name.Add($id)}
 }
 $item.IceCreamWeightRandomSelector=[Common.WeightRandomSelector[int]]::new($item.IceCreamFormulas,$item.Weights)
 $stages.Add($item);[Common.Parsers]::Instance.ActivityMiniGameIceCreamInfoParser.Items.Add($item)
}
$visitors=[System.Collections.Generic.List[object]]::new()
foreach($stage in $stages){foreach($seed in 1,12345,-2147483648){
 $random=[Framework.SystemRandom]::new();$random.SetSeed($seed);[Common.RandomUtils]::Default=$random
 $queue=[System.Collections.Generic.List[Common.PlayerActivityMiniGameIceCreamVisitorDataWrap]]::new()
 $result=[System.Collections.Generic.List[object]]::new();$buffer=[System.Collections.Generic.List[int]]::new()
 # Include a full NPC queue once, exercising the original fallback to all NPCs.
 for($i=0;$i -lt 30;$i++){
  if($i -eq 10){$queue.Clear();foreach($npc in $stage.Npcs){$v=[Common.PlayerActivityMiniGameIceCreamVisitorDataWrap]::new();$v.NPCId=$npc;$queue.Add($v)}}
  $before=@($queue | ForEach-Object {$_.NPCId})
  $v=[Common.ActivityMiniGameIceCreamDataExt]::CreateIceCreamVisitor($stage,$queue,[ref]$buffer)
  $result.Add(@{queue=$before;NPCId=$v.NPCId;IceCreams=@($v.IceCreams);Score=$v.Score})
  $queue.Add($v);while($queue.Count -gt $stage.QueueVisitorCount){$queue.RemoveAt(0)}
 }
 $visitors.Add(@{activity=$stage.ActivityId;order=$stage.Order;seed=$seed;visitors=@($result)})
}}
$ingredients=[System.Collections.Generic.List[object]]::new()
foreach($maximum in 1,2,4){foreach($initial in @(@(),@(11),@(11,21),@(11,21,22),@(11,21,33))){foreach($id in 0,11,20,21,30,31,40,41){
 $values=[System.Collections.Generic.List[int]]::new();foreach($value in $initial){$values.Add($value)}
 $errorTip='';$ok=[IceCreamUIUtils]::TryAddIngredientToIceCreams($id,$maximum,[ref]$values,[ref]$errorTip)
 $ingredients.Add(@{maximum=$maximum;initial=@($initial);ingredient=$id;ok=$ok;error=$errorTip;result=@($values)})
}}}
$hashes=@{};foreach($path in $files){$hashes[[IO.Path]::GetRelativePath($sourceRoot,$path).Replace('\','/')]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
@{manifest='854_156383';sources=$hashes;table_sources=$inputData.sources;tables=$inputData.tables;visitor_cases=@($visitors);ingredient_cases=@($ingredients)} | ConvertTo-Json -Depth 35 -Compress | Set-Content -LiteralPath $FixturePath -Encoding utf8NoBOM
Write-Output "Pinned ice cream C# reference: $($stages.Count) stages, $($visitors.Count) visitor sequences, $($ingredients.Count) ingredient boundaries."

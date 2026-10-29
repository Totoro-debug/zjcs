param([string]$FixturePath='scripts/fixtures/jump-score-reference.json',[string]$PythonExecutable='python')
$ErrorActionPreference='Stop'
$sourceRoot=Join-Path $PSScriptRoot '../.tools/source-packages/official-code/common-source/Common'
$files=@('FixInt','ServerDifficultyConfig','ServerStageScoreConfig','ActivityMinigameJumpScoreRuleInfoParser') | ForEach-Object {Join-Path $sourceRoot "$_.cs"}
# The decompiler spells the IL base call as a virtual cast, which recurses in C#.
# Restore that dispatch only; the parser's configuration construction is unchanged.
$parserPath=Join-Path ([IO.Path]::GetTempPath()) ('zjcs-jump-parser-'+[guid]::NewGuid().ToString('N')+'.cs')
try {
 $parserSource=[IO.File]::ReadAllText($files[-1]).Replace('((ParserBase)this).PostParse();','base.PostParse();')
 [IO.File]::WriteAllText($parserPath,$parserSource)
 Add-Type -Path (@($files | Select-Object -SkipLast 1)+$parserPath+(Join-Path $PSScriptRoot 'fixtures/jump_score_reference_harness.cs'))
} finally {Remove-Item -LiteralPath $parserPath -ErrorAction SilentlyContinue}
$inputCode=@'
import json,re,hashlib
from official_tables import Constructors
from prepare_map_regions import Regions
e=Regions();c=Constructors()
name='ActivityMiniGame/activity_minigame_jump_score_rule'
mapping={m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',c.source('Tables'))}
_,obj=e.locate('Assets/Config/Luban/'+name+'.g.bytes')
raw=obj.read().m_Script.encode('utf8','surrogateescape')
print(json.dumps(dict(rows=c.table(raw,mapping[name]),source=dict(e.apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest()))))
'@
$inputData=(& $PythonExecutable -c $inputCode) | ConvertFrom-Json
if($LASTEXITCODE -ne 0){throw 'Pinned jump score table decoding failed'}
$rows=[System.Collections.Generic.List[Common.ActivityMinigameJumpScoreRuleInfo]]::new()
foreach($row in $inputData.rows){
 $item=[Common.ActivityMinigameJumpScoreRuleInfo]::new()
 foreach($key in 'ActivityId','StageID','LevelID','HeightRange','ScorePerHeightUnit'){$item.$key=$row.$key}
 $rows.Add($item)
}
$parser=[Common.ActivityMinigameJumpScoreRuleInfoParser]::new();$parser.configs=$rows.ToArray();$parser.PostParse()
$cases=[System.Collections.Generic.List[object]]::new()
foreach($group in ($rows | Group-Object ActivityId,StageID)){
 $first=$group.Group[0];$config=$parser.GetScoreConfig($first.ActivityId,$first.StageID)
 $heights=[System.Collections.Generic.HashSet[single]]::new()
 foreach($row in $group.Group){foreach($boundary in $row.HeightRange){
  foreach($offset in -1.0,-0.00146484375,-0.00048828125,0,0.00048828125,0.00146484375,0.5,1.0){[void]$heights.Add([single]($boundary+$offset))}
 }}
 foreach($height in ($heights | Sort-Object)){
  $fixed=[Common.FixInt]::new([single]$height)
  $cases.Add(@{activity=$first.ActivityId;stage=$first.StageID;height=$height;height_raw=$fixed.Value;score_raw=$config.CalculateCurHeightScore($fixed).Value})
 }
}
$hashes=@{};foreach($path in $files){$hashes[[IO.Path]::GetFileName($path)]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
@{manifest='854_156383';sources=$hashes;table_source=$inputData.source;rows=$inputData.rows;cases=@($cases)} | ConvertTo-Json -Depth 20 -Compress | Set-Content -LiteralPath $FixturePath -Encoding utf8NoBOM
Write-Output "Pinned jump C# score reference: $($rows.Count) rules, $($cases.Count) fixed point boundaries."

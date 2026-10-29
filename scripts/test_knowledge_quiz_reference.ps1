param([string]$FixturePath='scripts/fixtures/knowledge-quiz-reference.json',[string]$PythonExecutable='python')
$ErrorActionPreference='Stop'
$sourceRoot=Join-Path $PSScriptRoot '../.tools/source-packages/official-code'
$files=@('ActivityKnowledgeQuizQuestionInfoParser','KnowledgeQuizDifficultyType','KnowledgeQuizOptionType','WeightRandomSelector') | ForEach-Object {Join-Path $sourceRoot "common-source/Common/$_.cs"}
$files+=@('SystemRandom','IRandom') | ForEach-Object {Join-Path $sourceRoot "framework-firstpass-source/Framework/$_.cs"}
$files+=Join-Path $sourceRoot 'launch-source/Common/RegionCodeEnum.cs'
Add-Type -Path ($files+(Join-Path $PSScriptRoot 'fixtures/knowledge_quiz_reference_harness.cs'))
# These constructor decoders read the pinned Luban bytes without executing DLLs.
$inputCode=@'
import sys,json,hashlib
from pathlib import Path
sys.path.insert(0,'scripts')
from official_tables import Constructors
c=Constructors();tables={};sources={}
for name,kind in [('question','TbActivityKnowledgeQuizQuestionInfo'),('stage','TbActivityKnowledgeQuizStageInfo')]:
 p=Path('.tools/source-packages/official-data/activity_knowledge_quiz_'+name+'.g.bytes')
 tables[name]=c.table(p.read_bytes(),kind);sources[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
print(json.dumps(dict(tables=tables,sources=sources)))
'@
$inputData=(& $PythonExecutable -c $inputCode) | ConvertFrom-Json
if($LASTEXITCODE -ne 0){throw 'Pinned quiz table decoding failed'}
$parser=[Common.ActivityKnowledgeQuizQuestionInfoParser]::new()
$items=[System.Collections.Generic.List[Common.ActivityKnowledgeQuizQuestionInfo]]::new()
foreach($row in $inputData.tables.question){
 $item=[Common.ActivityKnowledgeQuizQuestionInfo]::new()
 foreach($name in 'ActivityId','Id','Weight','Enabled','Difficulty','Region','CorrectOption'){$item.$name=$row.$name}
 $items.Add($item)
}
$parser.Items=$items.ToArray();$parser.PostParse()
$cases=[System.Collections.Generic.List[object]]::new()
foreach($activity in 2181,2182){foreach($seed in 1,-2147483648,12345){foreach($difficulty in 1,2){foreach($region in 0,1,9){foreach($count in 0,3,300){
 $random=[Framework.SystemRandom]::new();$random.SetSeed($seed)
 $exclude=[System.Collections.Generic.List[int]]::new();foreach($id in 1,4,17,111){$exclude.Add($id)}
 $result=$parser.DrawQuestions($activity,[Common.KnowledgeQuizDifficultyType]$difficulty,[Common.RegionCodeEnum]$region,$count,$exclude,$random,$true)
 [Common.RandomUtils]::Default=$random
 $options=$parser.BuildShuffledOptionMap();$optionMap=@{};foreach($pair in $options.GetEnumerator()){$optionMap[[string][int]$pair.Key]=[int]$pair.Value}
 $cases.Add(@{activity=$activity;seed=$seed;difficulty=$difficulty;region=$region;count=$count;exclude=@($exclude);questions=@($result);options=$optionMap})
}}}}}
$probeRows=@(
 @{ActivityId=900001;Id=1;Weight=1;Enabled=$true;Difficulty=1;Region=0;CorrectOption=1},
 @{ActivityId=900001;Id=2;Weight=10;Enabled=$true;Difficulty=1;Region=9;CorrectOption=2},
 @{ActivityId=900001;Id=3;Weight=30;Enabled=$true;Difficulty=1;Region=1;CorrectOption=3},
 @{ActivityId=900001;Id=4;Weight=0;Enabled=$true;Difficulty=1;Region=9;CorrectOption=4},
 @{ActivityId=900001;Id=5;Weight=99;Enabled=$false;Difficulty=1;Region=0;CorrectOption=1},
 @{ActivityId=900001;Id=6;Weight=71;Enabled=$true;Difficulty=1;Region=4;CorrectOption=2},
 @{ActivityId=900001;Id=7;Weight=80;Enabled=$true;Difficulty=2;Region=9;CorrectOption=3}
)
# Deliberate test inputs cover regional order, disabled rows and unequal/zero
# weights. They are separate from the unmodified original runtime table.
$probeItems=[System.Collections.Generic.List[Common.ActivityKnowledgeQuizQuestionInfo]]::new()
foreach($row in $probeRows){
 $item=[Common.ActivityKnowledgeQuizQuestionInfo]::new()
 foreach($name in 'ActivityId','Id','Weight','Enabled','Difficulty','Region','CorrectOption'){$item.$name=$row.$name}
 $probeItems.Add($item)
}
$parser.Items=$probeItems.ToArray();$parser.PostParse()
$probes=[System.Collections.Generic.List[object]]::new()
foreach($seed in 1,12345){foreach($region in 0,1,4,9){foreach($count in 1,50){
 $random=[Framework.SystemRandom]::new();$random.SetSeed($seed)
 $exclude=[System.Collections.Generic.List[int]]::new();$exclude.Add(3)
 $result=$parser.DrawQuestions(900001,[Common.KnowledgeQuizDifficultyType]1,[Common.RegionCodeEnum]$region,$count,$exclude,$random,$true)
 [Common.RandomUtils]::Default=$random
 $options=$parser.BuildShuffledOptionMap();$optionMap=@{};foreach($pair in $options.GetEnumerator()){$optionMap[[string][int]$pair.Key]=[int]$pair.Value}
 $probes.Add(@{seed=$seed;difficulty=1;region=$region;count=$count;exclude=@($exclude);questions=@($result);options=$optionMap})
}}}
$hashes=@{}
$hashFiles=$files+(Join-Path $sourceRoot 'client-source/KnowledgeQuizStagePanel.cs')+(Join-Path $sourceRoot 'client-source/KnowledgeQuizStagePanelItem.cs')
foreach($path in $hashFiles){$hashes[[IO.Path]::GetRelativePath($sourceRoot,$path).Replace('\','/')]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
@{manifest='854_156383';sources=$hashes;table_sources=$inputData.sources;tables=$inputData.tables;cases=$cases;probe_questions=$probeRows;probe_cases=$probes} | ConvertTo-Json -Depth 35 -Compress | Set-Content -LiteralPath $FixturePath -Encoding utf8NoBOM
Write-Output "Pinned quiz C# reference: $($items.Count) original questions, $($cases.Count) original and $($probes.Count) boundary pool/shuffle cases."

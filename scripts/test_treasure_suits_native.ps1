param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor='D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python='D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=[IO.Path]::GetFullPath($ProjectPath)
. (Join-Path $PSScriptRoot 'assert_home_validation_project.ps1')
Assert-HomeValidationProject -Repository $repository -Project $project
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($project.Replace('\','/'))
}).Count){throw 'Do not stage into a running source project.'}
$work=Join-Path $repository ('output/validation/treasure-suits-runtime-'+[Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{format=1;repository=$repository;ownerPid=$PID}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_suit_ui.py') --check
if($LASTEXITCODE -ne 0){throw 'Original suit control sources differ.'}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_suit_validation.py') --work $work
if($LASTEXITCODE -ne 0){throw 'Original suit closure or parent layout differs.'}
$wanted=@{}
foreach($name in @('treasure-suit-ui','treasure-suit-native','treasure-properties')){
    $receipt=Get-Content -LiteralPath (Join-Path $repository ('assets/reference/'+$name+'-source-manifest.json')) -Raw|ConvertFrom-Json
    foreach($entry in $receipt.outputs.PSObject.Properties){if($entry.Name.StartsWith('unity/')){$wanted[$entry.Name]=$entry.Value}}
}
foreach($folder in @('Offline/TreasureUI','Official/TreasureDetailUI')){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $repository ('unity/Assets/Game/Runtime/'+$folder)) -File -Recurse){
        $wanted[$file.FullName.Substring($repository.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$native=Get-Content -LiteralPath (Join-Path $work 'native.json') -Raw|ConvertFrom-Json
$used=@{}
foreach($selection in @($native.selection,$native.member_template)){
    foreach($script in $selection.scripts){
        $name=if($script.namespace){$script.namespace+'.'+$script.type}else{$script.type}
        $used[$name]=$true
    }
}
foreach($entry in $native.native_scripts.PSObject.Properties){
    if(-not $used.ContainsKey($entry.Name)){continue}
    $row=$entry.Value;$wanted[$row.path]=$row.sha256;$wanted[$row.path+'.meta']=$row.meta_sha256
}
# The consumed custom layout inherits the existing source implementation.
$layout='unity/Assets/Game/Runtime/Official/WorldUI/HorizontalOrVerticalLayoutGroupJSZX.cs'
foreach($key in @($layout,($layout+'.meta'))){$wanted[$key]=(Get-FileHash -LiteralPath (Join-Path $repository $key) -Algorithm SHA256).Hash.ToLowerInvariant()}
foreach($entry in $wanted.GetEnumerator()){
    $source=Join-Path $repository $entry.Key
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw ('Bound source differs: '+$entry.Key)}
    $target=Join-Path $project $entry.Key.Substring(6)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw 'Source changed during staging.'}
}
$fixture=Join-Path $project 'Assets/Editor/TreasureSuitsNativeValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/treasure_suits_native_validation.cs') -Destination (Join-Path $fixture 'TreasureSuitsNativeValidation.cs') -Force
$wanted['scripts/fixtures/treasure_suits_native_validation.cs']=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'fixtures/treasure_suits_native_validation.cs') -Algorithm SHA256).Hash.ToLowerInvariant()
$wanted['scripts/test_treasure_suits_native.ps1']=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
@{name='TreasureSuitsNativeValidation';includePlatforms=@('Editor');references=@(
    'Zjcs.Editor','Unity.ugui','Unity.TextMeshPro','Zjcs.Official.TreasureDetailUI','Zjcs.Official.ProfessionUI',
    'Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI','Zjcs.Official.TreasureCultivation',
    'Zjcs.Offline.TreasureUI','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation',
    'Zjcs.Offline.ProfessionUI','Zjcs.Offline.HomeHudUI'
)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $fixture 'TreasureSuitsNativeValidation.asmdef') -Encoding utf8
$wanted|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'staged-source.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$oldProject=$env:ZJCS_TREASURE_SUITS_PROJECT;$oldOutput=$env:ZJCS_TREASURE_SUITS_OUTPUT;$oldAutomation=$env:ZJCS_BATTLE_INPUT_AUTOMATION
try{
    $env:ZJCS_TREASURE_SUITS_PROJECT=$project;$env:ZJCS_TREASURE_SUITS_OUTPUT=$work;$env:ZJCS_BATTLE_INPUT_AUTOMATION='1'
    $log=Join-Path $work 'unity.log'
    $process=Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-noaudio','-job-worker-count','2','-gc-helper-count','2','-projectPath',('"'+$project+'"'),
        '-executeMethod','TreasureSuitsNativeValidation.Run','-logFile',('"'+$log+'"'))
}finally{$env:ZJCS_TREASURE_SUITS_PROJECT=$oldProject;$env:ZJCS_TREASURE_SUITS_OUTPUT=$oldOutput;$env:ZJCS_BATTLE_INPUT_AUTOMATION=$oldAutomation}
Write-Output ('Treasure suit PID '+$process.Id+', output: '+$work)
$timer=[Diagnostics.Stopwatch]::StartNew()
while(-not $process.WaitForExit(10000)){
    (Get-Item -LiteralPath $work).LastWriteTime=Get-Date
    (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
    if($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))){
        Stop-Process -Id $process.Id;throw ('Treasure suit checks stopped: '+$log)
    }
}
$process.Refresh()
if($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $work 'result.json')) -or
    -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_SUITS_NATIVE_PASSED:' -Quiet)){
    throw ('Treasure suit checks failed: '+$log)
}
Write-Output ('TREASURE_SUITS_NATIVE_PASSED: '+$work)

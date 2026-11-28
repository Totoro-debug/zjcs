param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$ReferencePath,
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
$work=Join-Path $repository ('output/validation/treasure-descriptions-'+[Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{format=1;repository=$repository;ownerPid=$PID}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_descriptions.py') --check
if($LASTEXITCODE -ne 0){throw 'Original description sources differ.'}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_description_validation.py') --work $work --reference $ReferencePath
if($LASTEXITCODE -ne 0){throw 'Original description oracle preparation failed.'}
foreach($name in @('treasure-properties','treasure-descriptions')){
    $receipt=Get-Content -LiteralPath (Join-Path $repository ('assets/reference/'+$name+'-source-manifest.json')) -Raw|ConvertFrom-Json
    foreach($entry in $receipt.outputs.PSObject.Properties){
        $source=Join-Path $repository $entry.Name
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw ('Bound source differs: '+$entry.Name)}
        $target=Join-Path $project $entry.Name.Substring(6)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw 'Source changed during staging.'}
    }
}
$fixture=Join-Path $project 'Assets/Editor/TreasureDescriptionsValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
foreach($name in @('OriginalTreasureProperties.cs','OriginalTreasureDescriptions.cs','TreasureDescriptionsValidation.cs')){
    Copy-Item -LiteralPath (Join-Path $work $name) -Destination (Join-Path $fixture $name) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/treasure_descriptions_validation.cs') -Destination (Join-Path $fixture 'TreasureDescriptionChecks.cs') -Force
@{name='TreasureDescriptionsValidation';includePlatforms=@('Editor');references=@(
    'Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Official.TreasureCultivation',
    'Zjcs.Official.HomeLevelUpRules','Zjcs.Official.HomeExperienceRules','Zjcs.Official.HomeIncomeRules'
)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $fixture 'TreasureDescriptionsValidation.asmdef') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$env:ZJCS_TREASURE_DESCRIPTIONS_OUTPUT=$work
$env:ZJCS_TREASURE_DESCRIPTIONS_PROJECT=$project
$log=Join-Path $work 'unity.log'
$process=Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode','-nographics','-noaudio','-job-worker-count','2','-gc-helper-count','2',
    '-projectPath',('"'+$project+'"'),'-executeMethod','TreasureDescriptionsValidation.Run','-logFile',('"'+$log+'"'))
Write-Output ('Treasure descriptions PID '+$process.Id+', output: '+$work)
$timer=[Diagnostics.Stopwatch]::StartNew()
while(-not $process.WaitForExit(10000)){
    (Get-Item -LiteralPath $work).LastWriteTime=Get-Date
    (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
    if($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))){
        Stop-Process -Id $process.Id
        throw ('Treasure description checks stopped: '+$log)
    }
}
$process.Refresh()
if($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_DESCRIPTIONS_PASSED:' -Quiet)){
    throw ('Treasure description checks failed: '+$log)
}
Write-Output ('TREASURE_DESCRIPTIONS_PASSED: '+$work)

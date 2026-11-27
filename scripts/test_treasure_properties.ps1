param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$WorkPath,
    [string]$Editor='D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python='D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation=Join-Path $repository 'output/validation'
$project=(Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\','/')
$work=(Get-Item -LiteralPath $WorkPath).FullName
if(-not $project.StartsWith($validation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt')) -or
    -not $work.StartsWith($validation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $work '.zjcs-validation.json'))) { throw 'Use owned validation outputs and an independent source Unity project.' }
$ancestor=[IO.DirectoryInfo]::new($project)
while($ancestor.FullName -ne $validation) {
    if((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'A worktree or linked project is forbidden.' }
    $ancestor=$ancestor.Parent
}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($project.Replace('\','/'))
}).Count) { throw 'Do not stage into a running source project.' }
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_properties.py') --check
if($LASTEXITCODE -ne 0){throw 'Original treasure property sources differ.'}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_property_reference.py') --work $work
if($LASTEXITCODE -ne 0){throw 'Original property reference preparation failed.'}
$manifest=Get-Content -LiteralPath (Join-Path $repository 'assets/reference/treasure-properties-source-manifest.json') -Raw | ConvertFrom-Json
$probe=Get-Content -LiteralPath (Join-Path $work 'reference-inputs.json') -Raw | ConvertFrom-Json
if($probe.manifest -ne $manifest.manifest -or $probe.binary_sha256 -ne $manifest.binary.sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $work 'reference-level-prop.g.bytes') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $probe.binary_sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $work 'raw-tables.json') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $probe.raw_tables_sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $repository 'unity/Assets/Game/Resources/Treasure/treasure-properties.json') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $probe.runtime_payload_sha256){throw 'Original property reference payload changed.'}
$sources=@{}
foreach($entry in $manifest.outputs.PSObject.Properties) {
    $source=Join-Path $repository $entry.Name
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw ('Original output changed: '+$entry.Name)}
    $target=Join-Path $project $entry.Name.Substring(6)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw 'Source changed during staging.'}
    $sources[$entry.Name]=$entry.Value
}
$fixture=Join-Path $project 'Assets/Editor/TreasurePropertiesValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
foreach($pair in @(
    @((Join-Path $work 'OriginalTreasureProperties.cs'),'OriginalTreasureProperties.cs'),
    @((Join-Path $PSScriptRoot 'fixtures/treasure_properties_validation.cs'),'TreasurePropertiesValidation.cs')
)) {
    Copy-Item -LiteralPath $pair[0] -Destination (Join-Path $fixture $pair[1]) -Force
    $sources[$pair[0]]=(Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash.ToLowerInvariant()
}
@{name='TreasurePropertiesValidation';includePlatforms=@('Editor');references=@(
    'Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Official.TreasureCultivation',
    'Zjcs.Official.HomeLevelUpRules','Zjcs.Official.HomeExperienceRules','Zjcs.Official.HomeIncomeRules'
)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'TreasurePropertiesValidation.asmdef') -Encoding utf8
$sources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'source-fixture.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$previousOutput=$env:ZJCS_TREASURE_PROPERTIES_OUTPUT
$previousProject=$env:ZJCS_TREASURE_PROPERTIES_PROJECT
try {
    $env:ZJCS_TREASURE_PROPERTIES_OUTPUT=$work
    $env:ZJCS_TREASURE_PROPERTIES_PROJECT=$project
    $log=Join-Path $work 'unity.log'
    $process=Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode','-nographics','-muteAudio','-job-worker-count','2','-gc-helper-count','2',
        '-projectPath',('"'+$project+'"'),'-executeMethod','TreasurePropertiesValidation.Run','-logFile',('"'+$log+'"'))
}finally{$env:ZJCS_TREASURE_PROPERTIES_OUTPUT=$previousOutput;$env:ZJCS_TREASURE_PROPERTIES_PROJECT=$previousProject}
Write-Output ('Treasure properties PID '+$process.Id+', output: '+$work)
$timer=[Diagnostics.Stopwatch]::StartNew()
while(-not $process.WaitForExit(10000)) {
    (Get-Item -LiteralPath $work).LastWriteTime=Get-Date
    (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
    if($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Treasure property checks stopped: '+$log)
    }
}
$process.Refresh()
if($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_PROPERTIES_PASSED:' -Quiet)){throw ('Treasure property checks failed: '+$log)}
Write-Output ('TREASURE_PROPERTIES_PASSED: '+$work)

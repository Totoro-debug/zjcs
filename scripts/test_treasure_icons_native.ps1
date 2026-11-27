param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$PreparedWork='',
    [string]$Editor='D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python='D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation=Join-Path $repository 'output/validation'
$project=(Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\','/')
if(-not $project.StartsWith($validation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Use an independent source Unity project.' }
$ancestor=[IO.DirectoryInfo]::new($project)
while($ancestor.FullName -ne $validation) {
    if((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'A worktree or linked project is forbidden.' }
    $ancestor=$ancestor.Parent
}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($project.Replace('\','/'))
}).Count) { throw 'Do not stage into a running source project.' }
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_icon_controls.py') --check
if($LASTEXITCODE -ne 0) { throw 'Original treasure icon control source differs.' }
if($PreparedWork) {
    $work=(Get-Item -LiteralPath $PreparedWork).FullName
    if(-not $work.StartsWith($validation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $work '.zjcs-validation.json'))) { throw 'Reuse only an owned validation workspace.' }
} else {
    $work=Join-Path $validation ('treasure-icons-runtime-'+[Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $work)
    @{format=1;repository=$repository;ownerPid=$PID} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
}
$sources=@{}
$controls=Get-Content -LiteralPath (Join-Path $repository 'assets/reference/treasure-icon-controls-source-manifest.json') -Raw | ConvertFrom-Json
$native=Get-Content -LiteralPath (Join-Path $repository 'assets/reference/treasure-icon-native-source-manifest.json') -Raw | ConvertFrom-Json
$wanted=@{}
foreach($table in @($controls.outputs,$controls.shared_components,$native.outputs)) {
    foreach($entry in $table.PSObject.Properties) {
        if($entry.Name.StartsWith('unity/')) { $wanted[$entry.Name]=$entry.Value }
    }
}
foreach($folder in @('Assets/Game/Runtime/Offline/TreasureUI')) {
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $repository ('unity/'+$folder)) -File -Recurse) {
        $key=$file.FullName.Substring($repository.Length+1).Replace('\','/')
        $wanted[$key]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach($entry in @($wanted.GetEnumerator())) {
    if($entry.Key.EndsWith('.cs')) {
        $meta=$entry.Key+'.meta'
        $wanted[$meta]=(Get-FileHash -LiteralPath (Join-Path $repository $meta) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach($entry in $wanted.GetEnumerator()) {
    $source=Join-Path $repository $entry.Key
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw ('Original source changed: '+$entry.Key) }
    $target=Join-Path $project $entry.Key.Substring(6)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Source changed during staging.' }
    $sources[$entry.Key]=$entry.Value
}
if(-not (Test-Path -LiteralPath (Join-Path $work 'probe.json'))) {
    & $Python (Join-Path $PSScriptRoot 'prepare_treasure_icon_validation.py') --project $project --work $work
    if($LASTEXITCODE -ne 0) { throw 'Original icon probe extraction failed.' }
} else {
    & $Python (Join-Path $PSScriptRoot 'prepare_treasure_icon_validation.py') --texts-only --work $work
    if($LASTEXITCODE -ne 0) { throw 'Original icon language extraction failed.' }
    $probe=Get-Content -LiteralPath (Join-Path $work 'probe.json') -Raw | ConvertFrom-Json
    foreach($entry in $probe.outputs.PSObject.Properties) {
        $source=Join-Path $work ('probe-assets/'+$entry.Name)
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Original icon probe changed.' }
        $target=Join-Path $project ('Assets/TreasureIconValidationProbe/'+$entry.Name)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture=Join-Path $project 'Assets/Editor/TreasureIconsNativeValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
$fixtureSource=Join-Path $PSScriptRoot 'fixtures/treasure_icons_native_validation.cs'
Copy-Item -LiteralPath $fixtureSource -Destination (Join-Path $fixture 'TreasureIconsNativeValidation.cs') -Force
$sources['scripts/fixtures/treasure_icons_native_validation.cs']=(Get-FileHash -LiteralPath $fixtureSource -Algorithm SHA256).Hash.ToLowerInvariant()
@{name='TreasureIconsNativeValidation';includePlatforms=@('Editor');references=@(
    'Zjcs.Editor','Unity.ugui','Unity.TextMeshPro','Zjcs.Official.TreasureUI','Zjcs.Official.TreasureIconUI','Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.TreasureCultivation','Zjcs.Offline.TreasureUI','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Offline.ProfessionUI','Zjcs.Offline.HomeExperienceUI','Zjcs.Offline.HomePlayerHealthUI','Zjcs.Official.HomeExperienceRules','Zjcs.Offline.HomeHudUI'
)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'TreasureIconsNativeValidation.asmdef') -Encoding utf8
$sources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'source-fixture.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$oldProject=$env:ZJCS_TREASURE_ICONS_PROJECT
$oldOutput=$env:ZJCS_TREASURE_ICONS_OUTPUT
$oldAutomation=$env:ZJCS_BATTLE_INPUT_AUTOMATION
try {
    $env:ZJCS_TREASURE_ICONS_PROJECT=$project
    $env:ZJCS_TREASURE_ICONS_OUTPUT=$work
    $env:ZJCS_BATTLE_INPUT_AUTOMATION='1'
    $log=Join-Path $work 'unity.log'
    $process=Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-muteAudio','-job-worker-count','2','-gc-helper-count','2','-projectPath',('"'+$project+'"'),
        '-executeMethod','TreasureIconsNativeValidation.Run','-logFile',('"'+$log+'"'))
} finally {
    $env:ZJCS_TREASURE_ICONS_PROJECT=$oldProject
    $env:ZJCS_TREASURE_ICONS_OUTPUT=$oldOutput
    $env:ZJCS_BATTLE_INPUT_AUTOMATION=$oldAutomation
}
Write-Output ('Treasure icons PID '+$process.Id+', output: '+$work)
$timer=[Diagnostics.Stopwatch]::StartNew()
while(-not $process.WaitForExit(10000)) {
    (Get-Item -LiteralPath $work).LastWriteTime=Get-Date
    (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
    if($timer.Elapsed.TotalMinutes -gt 15 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Native treasure icon verification stopped: '+$log)
    }
}
$process.Refresh()
if($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $work 'result.json')) -or
    -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_ICONS_NATIVE_PASSED:' -Quiet)) { throw ('Native treasure icon verification failed: '+$log) }
Write-Output ('TREASURE_ICONS_NATIVE_PASSED: '+$work)

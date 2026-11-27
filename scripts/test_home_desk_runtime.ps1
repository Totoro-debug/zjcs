param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor='D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python='D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation=Join-Path $repository 'output/validation'
$project=(Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\','/')
if (-not $project.StartsWith($validation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Use an independent source Unity project.' }
$ancestor=[IO.DirectoryInfo]::new($project)
while($ancestor.FullName -ne $validation) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'A worktree or linked project is forbidden.' }
    $ancestor=$ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($project.Replace('\','/'))
}).Count) { throw 'Do not stage into a running source project.' }
$work=Join-Path $validation ('desk-runtime-'+[Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{format=1;repository=$repository;ownerPid=$PID} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
(Get-Item -LiteralPath $project).LastWriteTime=Get-Date
(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
$staged=@{}
foreach($relative in @(
    'Assets/Game/Runtime/Official/HomeTipDots', 'Assets/Game/Runtime/Offline/HomeTipDots',
    'Assets/Game/Runtime/Official/TreasureCultivation',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureTipService.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureTipService.cs.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.HomeTipDots.cs',
    'Assets/Game/Runtime/Official/HomeFurniture',
    'Assets/Game/Runtime/Official/ProfessionUI/IDynamicTipDotNodeManager.cs',
    'Assets/Game/Runtime/Official/ProfessionUI/IDynamicTipDotNodeManager.cs.meta',
    'Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots/PartnerTipDotServices.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
    'Assets/Game/Runtime/Presentation/Home/OfflineHomeCartServices.cs'
)) {
    $source=Join-Path $repository ('unity/'+$relative)
    $files=if((Get-Item -LiteralPath $source).PSIsContainer) { @(Get-ChildItem -LiteralPath $source -File -Recurse) } else { @(Get-Item -LiteralPath $source) }
    foreach($file in $files) {
        $path=$file.FullName.Substring((Join-Path $repository 'unity').Length+1)
        $target=Join-Path $project $path
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Source changed during staging.' }
        $staged['unity/'+$path.Replace('\','/')]=$hash
    }
}
foreach($name in @('home-desk-native','home-native-prefab-references','home-tip-dot-catalog','treasure-tips')) {
    $receipt=Get-Content -LiteralPath (Join-Path $repository ('assets/reference/'+$name+'-source-manifest.json')) -Raw | ConvertFrom-Json
    foreach($entry in $receipt.outputs.PSObject.Properties) {
        if(-not $entry.Name.StartsWith('unity/')) { continue }
        $source=Join-Path $repository $entry.Name
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw ('Original bound asset changed: '+$entry.Name) }
        $target=Join-Path $project $entry.Name.Substring(6)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Bound resource changed during staging.' }
        $staged[$entry.Name]=$entry.Value
    }
}
$staged | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'source-runtime.json') -Encoding utf8
& $Python (Join-Path $PSScriptRoot 'prepare_home_desk_validation.py') --project $project --work $work
if($LASTEXITCODE -ne 0) { throw 'Native workbench fixture preparation failed.' }
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$env:ZJCS_DESK_PROJECT=$project
$env:ZJCS_VALIDATION_DIR=$work
$env:ZJCS_REPOSITORY_ROOT=$repository
$env:ZJCS_BATTLE_INPUT_AUTOMATION='1'
foreach($phase in @('Import','Run')) {
    $log=Join-Path $work ($phase.ToLowerInvariant()+'.log')
    $arguments=@('-muteAudio','-job-worker-count','2','-gc-helper-count','2','-projectPath',('"'+$project+'"'),'-executeMethod',('HomeDeskRuntimeValidation.'+$phase),'-logFile',('"'+$log+'"'))
    if($phase -eq 'Import') { $arguments=@('-batchmode','-nographics','-quit')+$arguments }
    $process=Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList $arguments
    Write-Output ('Desk '+$phase+' PID '+$process.Id+', output: '+$work)
    $timer=[Diagnostics.Stopwatch]::StartNew()
    while(-not $process.WaitForExit(10000)) {
        (Get-Item -LiteralPath $work).LastWriteTime=Get-Date
        (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
        if($timer.Elapsed.TotalMinutes -ge 20) {
            Stop-Process -Id $process.Id
            throw ('Workbench '+$phase+' timed out: '+$log)
        }
        if((Test-Path -LiteralPath $log) -and
            (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet)) {
            Stop-Process -Id $process.Id
            throw ('Workbench validation stopped: '+$log)
        }
    }
    $process.Refresh()
    $marker=if($phase -eq 'Import') { 'HOME_DESK_IMPORT_PASSED' } else { 'HOME_DESK_RUNTIME_PASSED:' }
    if($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch $marker -Quiet)) { throw ('Workbench '+$phase+' failed: '+$log) }
}
Write-Output ('HOME_DESK_RUNTIME_PASSED: '+$work)

param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python = 'D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation = Join-Path $repository 'output/validation'
$project = (Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\','/')
if (-not $project.StartsWith($validation + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Use an independent source Unity project.' }
$ancestor = [IO.DirectoryInfo]::new($project)
while ($ancestor.FullName -ne $validation) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'A worktree or linked project is forbidden.' }
    $ancestor = $ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($project.Replace('\','/'))
}).Count) { throw 'Do not stage into a running source project.' }
$work = Join-Path $validation ('treasure-slots-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{format=1;repository=$repository;ownerPid=$PID} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_slots.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original treasure slot source verification failed.' }
$sources = @{}
foreach ($relative in @(
    'Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureSlots.cs',
    'Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureSlots.cs.meta',
    'Assets/Game/Resources/Treasure/treasure-slots.json','Assets/Game/Resources/Treasure/treasure-slots.json.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotProgress.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotProgress.cs.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.cs.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs'
)) {
    $source = Join-Path $repository ('unity/' + $relative)
    $target = Join-Path $project $relative
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    $digest = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $digest -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $digest) { throw 'Source changed during staging.' }
    $sources['unity/' + $relative] = $digest.ToLowerInvariant()
}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_slots.py') --oracle (Join-Path $work 'OriginalTreasureSlots.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original treasure slot extraction failed.' }
$fixture = Join-Path $project 'Assets/Editor/TreasureSlotsValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
foreach ($pair in @(
    @((Join-Path $work 'OriginalTreasureSlots.cs'), 'OriginalTreasureSlots.cs'),
    @((Join-Path $PSScriptRoot 'fixtures/treasure_slots_validation.cs'), 'TreasureSlotsValidation.cs')
)) {
    Copy-Item -LiteralPath $pair[0] -Destination (Join-Path $fixture $pair[1]) -Force
    $sources[$pair[0]] = (Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash.ToLowerInvariant()
}
@{name='TreasureSlotsValidation';includePlatforms=@('Editor');references=@(
    'Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Official.TreasureCultivation','Zjcs.Offline.HomePlayerHealthUI'
)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'TreasureSlotsValidation.asmdef') -Encoding utf8
$sources['OriginalTreasureSlots.json'] = (Get-FileHash -LiteralPath (Join-Path $work 'OriginalTreasureSlots.json') -Algorithm SHA256).Hash.ToLowerInvariant()
$sources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'source-fixture.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$previous = $env:ZJCS_TREASURE_SLOTS_OUTPUT
try {
    $env:ZJCS_TREASURE_SLOTS_OUTPUT = $work
    $log = Join-Path $work 'unity.log'
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode','-nographics','-muteAudio','-job-worker-count','2','-gc-helper-count','2',
        '-projectPath',('"'+$project+'"'),'-executeMethod','TreasureSlotsValidation.Run','-logFile',('"'+$log+'"'))
} finally { $env:ZJCS_TREASURE_SLOTS_OUTPUT = $previous }
Write-Output ('Treasure slots PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    (Get-Item -LiteralPath $work).LastWriteTime = Get-Date
    (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Treasure slot checks stopped: ' + $log)
    }
}
$process.Refresh()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_SLOTS_PASSED:' -Quiet)) { throw ('Treasure slot checks failed: ' + $log) }
Write-Output ('TREASURE_SLOTS_PASSED: ' + $work)

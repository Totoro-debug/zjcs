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
$work = Join-Path $validation ('treasure-checks-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{format=1;repository=$repository;ownerPid=$PID} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_cultivation_rules.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original Treasure check source verification failed.' }
& $Python (Join-Path $PSScriptRoot 'prepare_item_merge_rules.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original item merge verification failed.' }
$sources = @{}
foreach ($relative in @(
    'Assets/Game/Runtime/Official/TreasureCultivation', 'Assets/Game/Runtime/Official/TreasureCultivation.meta',
    'Assets/Game/Runtime/Official/ItemMerge', 'Assets/Game/Runtime/Official/ItemMerge.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineItemMergeCatalog.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineItemMergeCatalog.cs.meta',
    'Assets/Game/Resources/ItemMerge', 'Assets/Game/Resources/ItemMerge.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureService.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureService.cs.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureCostRules.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureCostRules.cs.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs',
    'Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs.meta',
    'Assets/Game/Resources/Treasure/treasure-rules.json',
    'Assets/Game/Resources/Treasure/treasure-rules.json.meta',
    'Assets/Game/Runtime/Offline/Progression/Zjcs.Offline.Progression.asmdef'
)) {
    $source = Join-Path $repository ('unity/' + $relative)
    $files = if ((Get-Item -LiteralPath $source).PSIsContainer) { @(Get-ChildItem -LiteralPath $source -File -Recurse) } else { @(Get-Item -LiteralPath $source) }
    foreach ($file in $files) {
        $path = $file.FullName.Substring((Join-Path $repository 'unity').Length + 1)
        $target = Join-Path $project $path
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        $digest = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $digest -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $digest) { throw 'Source changed during staging.' }
        $sources['unity/' + $path.Replace('\','/')] = $digest.ToLowerInvariant()
    }
}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_cultivation_rules.py') --oracle (Join-Path $work 'OriginalTreasureChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original Treasure helper extraction failed.' }
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_rules.py') --oracle (Join-Path $work 'OriginalTreasureRules.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original Treasure table extraction failed.' }
& $Python (Join-Path $PSScriptRoot 'prepare_item_merge_rules.py') --oracle (Join-Path $work 'OriginalItemMergeRules.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original item merge extraction failed.' }
$fixture = Join-Path $project 'Assets/Editor/TreasureCultivationValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
foreach ($pair in @(
    @((Join-Path $work 'OriginalTreasureChecks.cs'), 'OriginalTreasureChecks.cs'),
    @((Join-Path $PSScriptRoot 'fixtures/treasure_cultivation_validation.cs'), 'TreasureCultivationValidation.cs'),
    @((Join-Path $PSScriptRoot 'fixtures/treasure_cultivation_oracle.cs'), 'TreasureCultivationOracle.cs'),
    @((Join-Path $work 'OriginalItemMergeRules.cs'), 'OriginalItemMergeRules.cs'),
    @((Join-Path $PSScriptRoot 'fixtures/item_merge_validation.cs'), 'ItemMergeValidation.cs'),
    @((Join-Path $PSScriptRoot 'fixtures/item_merge_oracle.cs'), 'ItemMergeOracle.cs')
)) {
    Copy-Item -LiteralPath $pair[0] -Destination (Join-Path $fixture $pair[1]) -Force
    $sources[$pair[0]] = (Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash.ToLowerInvariant()
}
@{name='TreasureCultivationValidation'; includePlatforms=@('Editor'); references=@(
    'Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Official.TreasureCultivation','Zjcs.Official.ItemMerge'
)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'TreasureCultivationValidation.asmdef') -Encoding utf8
foreach ($name in @('OriginalTreasureRules.json','TreasureCultivationItems.json','OriginalItemMergeRules.json')) {
    $sources[$name] = (Get-FileHash -LiteralPath (Join-Path $work $name) -Algorithm SHA256).Hash.ToLowerInvariant()
}
$sources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'source-fixture.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$previous = $env:ZJCS_TREASURE_CHECK_OUTPUT
try {
    $env:ZJCS_TREASURE_CHECK_OUTPUT = $work
    $log = Join-Path $work 'unity.log'
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode','-nographics','-muteAudio','-job-worker-count','2','-gc-helper-count','2',
        '-projectPath',('"'+$project+'"'),'-executeMethod','TreasureCultivationValidation.Run','-logFile',('"'+$log+'"'))
} finally { $env:ZJCS_TREASURE_CHECK_OUTPUT = $previous }
Write-Output ('Treasure checks PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    (Get-Item -LiteralPath $work).LastWriteTime = Get-Date
    (Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Treasure cultivation checks stopped: ' + $log)
    }
}
$process.Refresh()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_CULTIVATION_PASSED:' -Quiet)) { throw ('Treasure cultivation checks failed: ' + $log) }
Write-Output ('TREASURE_CULTIVATION_PASSED: ' + $work)

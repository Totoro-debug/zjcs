param(
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python = 'D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$work = Join-Path $repository ('output/validation/treasure-catalog-' + [Guid]::NewGuid().ToString('N'))
$project = Join-Path $work 'source-unity'
[void](New-Item -ItemType Directory -Path $project)
@{format=1;repository=$repository;ownerPid=$PID} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
foreach ($relative in @('Assets/Editor', 'Assets/Resources/Treasure', 'Packages', 'ProjectSettings')) {
    [void](New-Item -ItemType Directory -Path (Join-Path $project $relative) -Force)
}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_rules.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original Treasure source verification failed.' }
$sources = @{}
foreach ($pair in @(
    @('unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs', 'Assets/OfflineTreasureCatalog.cs'),
    @('unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs.meta', 'Assets/OfflineTreasureCatalog.cs.meta'),
    @('unity/Assets/Game/Resources/Treasure/treasure-rules.json', 'Assets/Resources/Treasure/treasure-rules.json'),
    @('unity/Assets/Game/Resources/Treasure/treasure-rules.json.meta', 'Assets/Resources/Treasure/treasure-rules.json.meta'),
    @('unity/ProjectSettings/ProjectVersion.txt', 'ProjectSettings/ProjectVersion.txt'),
    @('scripts/fixtures/treasure_catalog_validation.cs', 'Assets/Editor/TreasureCatalogValidation.cs')
)) {
    $source = Join-Path $repository $pair[0]
    $target = Join-Path $project $pair[1]
    $digest = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    Copy-Item -LiteralPath $source -Destination $target
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $digest -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $digest) { throw 'Source changed during staging.' }
    $sources[$pair[0]] = $digest
}
@{dependencies=@{'com.unity.modules.jsonserialize'='1.0.0';'com.unity.nuget.newtonsoft-json'='3.2.1'}} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Encoding utf8
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_rules.py') --oracle (Join-Path $work 'OriginalTreasureRules.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original Treasure oracle extraction failed.' }
Copy-Item -LiteralPath (Join-Path $work 'OriginalTreasureRules.cs') -Destination (Join-Path $project 'Assets/Editor/OriginalTreasureRules.cs')
foreach ($name in @('OriginalTreasureRules.cs', 'OriginalTreasureRules.json')) {
    $sources[$name] = (Get-FileHash -LiteralPath (Join-Path $work $name) -Algorithm SHA256).Hash.ToLowerInvariant()
}
$sources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'source-fixture.json') -Encoding utf8
$previous = $env:ZJCS_TREASURE_CATALOG_OUTPUT
$log = Join-Path $work 'unity.log'
try {
    $env:ZJCS_TREASURE_CATALOG_OUTPUT = $work
    $process = Start-Process -FilePath $Editor -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'TreasureCatalogValidation.Run',
        '-logFile', ('"' + $log + '"'))
} finally { $env:ZJCS_TREASURE_CATALOG_OUTPUT = $previous }
Write-Output ('Treasure catalog PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    (Get-Item -LiteralPath $work).LastWriteTime = Get-Date
    if ($timer.Elapsed.TotalMinutes -gt 10 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Treasure catalog validation stopped: ' + $log)
    }
}
$process.Refresh()
$path = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $path)) { throw ('Treasure catalog validation failed: ' + $log) }
$report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
if (-not $report.success) { throw ('Treasure catalog checks failed: ' + $path) }
Write-Output ('TREASURE_CATALOG_PASSED: ' + $report.checks.Count + ' checks, ' + $work)

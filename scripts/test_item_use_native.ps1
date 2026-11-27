param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python = 'D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation = Join-Path $repository 'output/validation'
$project = (Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\', '/')
if (-not $project.StartsWith($validation + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Use an independent Unity source project.' }
$ancestor = [IO.DirectoryInfo]::new($project)
while ($ancestor.FullName -ne $validation) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or
        ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'A worktree or linked project is forbidden.' }
    $ancestor = $ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count) { throw 'Do not stage sources in a running Unity project.' }
$work = Join-Path $validation ('item-use-native-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
(Get-Item -LiteralPath $project).LastWriteTime = Get-Date
(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
& $Python (Join-Path $PSScriptRoot 'prepare_pet_item_detail_runtime.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original native item source verification failed.' }
$proof = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/pet-item-detail-native-source-manifest.json') -Raw | ConvertFrom-Json
$sources = @{}
foreach ($map in @($proof.inputs, $proof.outputs, $proof.runtime_bindings)) {
    foreach ($entry in $map.PSObject.Properties) { if ($entry.Name.StartsWith('unity/')) { $sources[$entry.Name] = $entry.Value } }
}
foreach ($row in $proof.script_bindings.PSObject.Properties.Value) {
    $sources[$row.path] = $row.sha256
    $sources[$row.path + '.meta'] = $row.meta_sha256
}
foreach ($relative in @($sources.Keys)) {
    $source = Join-Path $repository $relative
    $target = Join-Path $project $relative.Substring(6)
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sources[$relative]) { throw ('Source changed before staging: ' + $relative) }
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sources[$relative] -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sources[$relative]) { throw 'Source changed during staging.' }
}
$fixture = Join-Path $project 'Assets/Editor/ItemUseNative/ItemUseNativeValidation.cs'
[void](New-Item -ItemType Directory -Path (Split-Path $fixture) -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/item_use_native_validation.cs') -Destination $fixture -Force
$sources['fixture'] = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $work 'source-fixture.json'), ($sources | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$previous = @{}
foreach ($name in @('ZJCS_ITEM_USE_PROJECT', 'ZJCS_ITEM_USE_OUTPUT')) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$log = Join-Path $work 'unity.log'
try {
    $env:ZJCS_ITEM_USE_PROJECT = $project
    $env:ZJCS_ITEM_USE_OUTPUT = $work
    . (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'ItemUseNativeValidation.Run',
        '-logFile', ('"' + $log + '"'))
} finally { foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) } }
Write-Output ('Original item-use PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Item-use validation stopped: ' + $log)
    }
}
$process.Refresh()
$reportPath = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw ('Native item-use validation failed: ' + $log) }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.success) { throw ('Native item-use checks failed: ' + $reportPath) }
foreach ($id in @(6950, 6951, 6952)) {
    foreach ($prefix in @('detail-', 'choice-')) {
        if (-not (Test-Path -LiteralPath (Join-Path $work ($prefix + $id + '.png')))) { throw 'Missing actual original rendering.' }
    }
}
Write-Output ('ITEM_USE_NATIVE_PASSED: ' + $work)

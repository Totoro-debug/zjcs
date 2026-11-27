param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python = 'D:/miniconda3/envs/comm/python.exe',
    [switch]$NativeAcceleration,
    [string]$NativeSourceDirectory
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validation = Join-Path $repository 'output/validation'
$project = (Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\', '/')
if (-not $project.StartsWith($validation + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Use an independent Unity source project under output/validation.'
}
$ancestor = [IO.DirectoryInfo]::new($project)
while ($ancestor.FullName -ne $validation) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or
        ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'A worktree or linked project is forbidden.' }
    $ancestor = $ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count) { throw 'Do not change sources in a running Unity project.' }
$work = Join-Path $validation ('item-use-count-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
(Get-Item -LiteralPath $project).LastWriteTime = Get-Date
(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
& $Python (Join-Path $PSScriptRoot 'prepare_shared_use_count.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Shared use-count source check failed.' }
$proof = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/shared-use-count-source-manifest.json') -Raw | ConvertFrom-Json
$staged = @{}
foreach ($entry in $proof.inputs.PSObject.Properties) {
    if (-not $entry.Name.StartsWith('unity/')) { continue }
    $source = Join-Path $repository $entry.Name
    $target = Join-Path $project $entry.Name.Substring(6)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Source changed before staging.' }
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Source changed during staging.' }
    $staged[$entry.Name] = $entry.Value
}
if ($NativeAcceleration) {
    $native = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-fast-hangup-native-source-manifest.json') -Raw | ConvertFrom-Json
    $nativeSources = @{}
    foreach ($entry in $native.installed_output_sha256.PSObject.Properties) { $nativeSources[$entry.Name] = $entry.Value }
    foreach ($entry in $native.script_bindings.PSObject.Properties) {
        $nativeSources[$entry.Value.path] = $entry.Value.sha256
        $nativeSources[$entry.Value.path + '.meta'] = $entry.Value.meta_sha256
    }
    foreach ($relative in $nativeSources.Keys) {
        $source = Join-Path $repository $relative
        $target = Join-Path $project $relative.Substring(6)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $nativeSources[$relative]) { throw ('Native source differs: ' + $relative) }
        Copy-Item -LiteralPath $source -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $nativeSources[$relative] -or
            (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $nativeSources[$relative]) { throw 'Native source changed during staging.' }
        $staged[$relative] = $nativeSources[$relative]
    }
    $originalByGuid = @{}
    if ($NativeSourceDirectory) {
        $nativeRoot = (Get-Item -LiteralPath $NativeSourceDirectory).FullName
        if (-not $nativeRoot.StartsWith($validation + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use owned native source outputs under output/validation.' }
        $nativeAncestor = [IO.DirectoryInfo]::new($nativeRoot)
        while ($nativeAncestor.FullName -ne $validation) {
            if ($nativeAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Native validation sources must not be linked.' }
            $nativeAncestor = $nativeAncestor.Parent
        }
        foreach ($entry in $native.outputs.PSObject.Properties) {
            $source = Join-Path $nativeRoot $entry.Name
            if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Rebuilt original resource differs.' }
            if ($entry.Name.EndsWith('.meta')) {
                $metadata = Get-Content -LiteralPath $source -Raw
                if ($metadata -match '(?m)^guid: ([0-9a-f]{32})\r?$') { $originalByGuid[$Matches[1]] = $source.Substring(0, $source.Length - 5) }
            }
        }
    }
    foreach ($entry in $native.reused) {
        $source = Join-Path $repository $entry.path
        $target = Join-Path $project $entry.path.Substring(6)
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
            # Preserve other sessions' dynamic font/assets in the main workspace.
            # The isolated fixture uses only the exact originally recorded bytes.
            $source = $originalByGuid[$entry.guid]
            if (-not $source -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw ('Original shared dependency is unavailable: ' + $entry.path) }
        }
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
        Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw 'Original shared dependency staging failed.' }
        $staged[$entry.path] = $entry.sha256
    }
    $data = Join-Path $project 'Assets/HomeFastHangUpNativeValidation/fixture.json'
    [void](New-Item -ItemType Directory -Path (Split-Path $data) -Force)
    Copy-Item -LiteralPath (Join-Path $repository 'assets/reference/home-fast-hangup-native-source-manifest.json') -Destination $data -Force
}
$fixture = Join-Path $project ('Assets/Editor/ItemUseCount/' + $(if ($NativeAcceleration) { 'HomeFastHangUpNativeValidation.cs' } else { 'ItemUseCountValidation.cs' }))
[void](New-Item -ItemType Directory -Path (Split-Path $fixture) -Force)
if ($NativeAcceleration) {
    $text = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_fast_hangup_native_validation.cs') -Raw
    # Only adapt the fixture's project/output separation and disambiguate the
    # preexisting acceleration type; native flow assertions remain unchanged.
    $text = $text.Replace('using Object = UnityEngine.Object;', "using SetUseCountSlider = Zjcs.Official.HomeFastHangUpUI.SetUseCountSlider;`nusing Object = UnityEngine.Object;")
    $text = $text.Replace('StartsWith(work+Path.DirectorySeparatorChar)', 'StartsWith(Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_USE_COUNT_PROJECT"),"Assets")))')
    [IO.File]::WriteAllText($fixture, $text, [Text.UTF8Encoding]::new($false))
} else {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/item_use_count_validation.cs') -Destination $fixture -Force
}
$staged['fixture'] = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $work 'source-fixture.json'), ($staged | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$previous = @{}
foreach ($name in @('ZJCS_USE_COUNT_OUTPUT', 'ZJCS_USE_COUNT_PROJECT', 'ZJCS_VALIDATION_DIR')) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$log = Join-Path $work 'unity.log'
try {
    $env:ZJCS_USE_COUNT_OUTPUT = $work
    $env:ZJCS_USE_COUNT_PROJECT = $project
    $env:ZJCS_VALIDATION_DIR = $work
    $arguments = @('-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-logFile', ('"' + $log + '"'))
    if ($NativeAcceleration) {
        . (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
        $arguments += @('-executeMethod', 'HomeFastHangUpNativeValidation.Run')
        $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList $arguments
    } else {
        $arguments += @('-nographics', '-executeMethod', 'ItemUseCountValidation.Run')
        $process = Start-Process -FilePath $Editor -WindowStyle Hidden -PassThru -ArgumentList $arguments
    }
} finally { foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) } }
Write-Output ('Use-count PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Use-count validation stopped: ' + $log)
    }
}
$process.Refresh()
if ($NativeAcceleration) {
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_FAST_HANGUP_NATIVE_PASSED:' -Quiet)) { throw ('Native acceleration regression failed: ' + $log) }
    foreach ($name in @('fast-hangup-selection.png', 'fast-hangup-quantity.png', 'fast-hangup-closed.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $work $name))) { throw ('Missing actual rendering: ' + $name) }
    }
    Write-Output ('ITEM_USE_COUNT_NATIVE_PASSED: ' + $work)
    return
}
$reportPath = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw ('Use-count validation failed: ' + $log) }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.success -or $report.checks.Count -lt 25) { throw ('Use-count checks failed: ' + $reportPath) }
Write-Output ('ITEM_USE_COUNT_PASSED: ' + $work)

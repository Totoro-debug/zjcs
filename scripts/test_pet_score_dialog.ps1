param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [switch]$DetailOnly,
    [string]$Python = 'D:/miniconda3/envs/comm/python.exe'
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
}).Count) { throw 'Do not stage sources into a running Unity project.' }
foreach ($required in @('Assets/Game/Resources/Character/OfficialCharacterAssetCatalog.asset',
    'Assets/Official/Character/character-progression.json', 'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $project $required))) {
        throw ('Restore the incomplete independent source snapshot before validation: ' + $required)
    }
}
[void](New-Item -ItemType Directory -Path (Join-Path $project 'Assets/Editor') -Force)
# Keep the actively used source snapshot recognizable as current validation work.
(Get-Item -LiteralPath $project).LastWriteTime = Get-Date
(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
# A source archive can apply checkout line endings to JSON payloads whose runtime
# manifests cover exact bytes. Restore only byte differences caused by CRLF/LF.
$sourceUtf8 = [Text.UTF8Encoding]::new($false, $true)
$restoredJson = 0
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $repository 'unity/Assets') -Filter '*.json' -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath((Join-Path $repository 'unity'), $source.FullName)
    $target = Join-Path $project $relative
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $raw = [IO.File]::ReadAllBytes($source.FullName)
    $other = [IO.File]::ReadAllBytes($target)
    if ([Convert]::ToBase64String($raw) -eq [Convert]::ToBase64String($other)) { continue }
    if (-not [string]::Equals($sourceUtf8.GetString($raw).Replace("`r`n", "`n"),
        $sourceUtf8.GetString($other).Replace("`r`n", "`n"), [StringComparison]::Ordinal)) { continue }
    $manifestPath = Join-Path $source.DirectoryName ($source.BaseName + '-source-manifest.json')
    if (Test-Path -LiteralPath $manifestPath) {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.output_sha256 -and (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.output_sha256) {
            throw ('Original JSON payload differs from its source receipt: ' + $relative)
        }
    }
    [IO.File]::WriteAllBytes($target, $raw)
    $restoredJson++
}
Write-Output ('Restored exact original JSON bytes: ' + $restoredJson)
& $Python (Join-Path $PSScriptRoot 'prepare_score_reward_dialog_runtime.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Score reward dialog source provenance must verify before staging.' }
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/score-reward-dialog-runtime-source-manifest.json') -Raw | ConvertFrom-Json
$work = Join-Path $validation ('pet-score-dialog-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
foreach ($entry in $receipt.staged_sources.PSObject.Properties) {
    $source = Join-Path $repository $entry.Name
    $target = Join-Path $project $entry.Name.Substring(6)
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw ('Source changed before staging: ' + $entry.Name) }
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw ('Source changed during staging: ' + $entry.Name) }
}
Copy-Item -LiteralPath (Join-Path $repository 'assets/reference/pet-gacha-controls-native-source-manifest.json') -Destination (Join-Path $work 'control-fixture.json')
Copy-Item -LiteralPath (Join-Path $repository 'assets/reference/score-reward-dialog-runtime-source-manifest.json') -Destination (Join-Path $work 'source-fixture.json')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$names = @('ZJCS_HOME_ACCEPTANCE_ROOT', 'ZJCS_HOME_ACCEPTANCE_OUTPUT', 'ZJCS_PET_GACHA_CONTROL_FIXTURE')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:ZJCS_HOME_ACCEPTANCE_ROOT = Join-Path $work 'profiles'
    $env:ZJCS_HOME_ACCEPTANCE_OUTPUT = $work
    $env:ZJCS_PET_GACHA_CONTROL_FIXTURE = Join-Path $work 'control-fixture.json'
    $extra = if ($DetailOnly) { @('-petScoreDialogOnlyAcceptance') } else { @() }
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList (@(
        '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2', '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'Zjcs.Editor.Home.HomeAcceptanceLauncher.Run', '-homeAcceptance', '-fruitTreeNativeAcceptance',
        '-petGachaControlsAcceptance', '-logFile', ('"' + $log + '"')) + $extra)
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
Write-Output ('Pet score dialog PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 30 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Score dialog validation stopped: ' + $log)
    }
}
$process.Refresh()
$reportPath = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw ('Score dialog validation failed: ' + $log) }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$required = if ($DetailOnly) { 7 } else { 25 }
if (-not $report.success -or @($report.lines | Where-Object { $_ -like 'PASS pet-gacha-controls-*' }).Count -ne $required) {
    throw ('Score dialog and shared controls must all pass: ' + $reportPath)
}
Write-Output ('PET_SCORE_DIALOG_PASSED: ' + $work)

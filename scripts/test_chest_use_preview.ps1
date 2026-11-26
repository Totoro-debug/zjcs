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
$work = Join-Path $validation ('chest-use-preview-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
(Get-Item -LiteralPath $project).LastWriteTime = Get-Date
(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
& $Python (Join-Path $PSScriptRoot 'prepare_pet_score_chest_preview.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original chest preview source check failed.' }
$proof = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/pet-score-chest-preview-source-manifest.json') -Raw | ConvertFrom-Json
$staged = @{}
foreach ($map in @($proof.inputs, $proof.outputs)) {
    foreach ($entry in $map.PSObject.Properties) {
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
}
$fixtureRoot = Join-Path $project 'Assets/Editor/ChestUsePreview'
[void](New-Item -ItemType Directory -Path $fixtureRoot -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/chest_use_preview_validation.cs') -Destination (Join-Path $fixtureRoot 'ChestUsePreviewValidation.cs') -Force
& $Python (Join-Path $PSScriptRoot 'prepare_pet_score_chest_preview.py') --oracle (Join-Path $fixtureRoot 'OriginalChestUsePreview.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original source oracle extraction failed.' }
foreach ($file in Get-ChildItem -LiteralPath $fixtureRoot -Filter '*.cs' -File) {
    $staged[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
[IO.File]::WriteAllText((Join-Path $work 'source-fixture.json'), ($staged | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$previous = $env:ZJCS_CHEST_PREVIEW_OUTPUT
$log = Join-Path $work 'unity.log'
try {
    $env:ZJCS_CHEST_PREVIEW_OUTPUT = $work
    $process = Start-Process -FilePath $Editor -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'ChestUsePreviewValidation.Run',
        '-logFile', ('"' + $log + '"'))
} finally { $env:ZJCS_CHEST_PREVIEW_OUTPUT = $previous }
Write-Output ('Chest use-preview PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Chest preview validation stopped: ' + $log)
    }
}
$process.Refresh()
$reportPath = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw ('Chest preview validation failed: ' + $log) }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.success -or $report.checks.Count -lt 60) { throw ('Chest preview checks failed: ' + $reportPath) }
Write-Output ('CHEST_USE_PREVIEW_PASSED: ' + $work)

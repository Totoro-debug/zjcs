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
$work = Join-Path $validation ('optional-chest-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
(Get-Item -LiteralPath $project).LastWriteTime = Get-Date
(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime = Get-Date
& $Python (Join-Path $PSScriptRoot 'prepare_pet_score_chest_preview.py') --check
if ($LASTEXITCODE -ne 0) { throw 'Original chest configuration verification failed.' }
$proof = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/pet-score-chest-preview-source-manifest.json') -Raw | ConvertFrom-Json
$sources = @{}
foreach ($map in @($proof.inputs, $proof.outputs)) {
    foreach ($entry in $map.PSObject.Properties) {
        if (-not $entry.Name.StartsWith('unity/')) { continue }
        $source = Join-Path $repository $entry.Name
        $target = Join-Path $project $entry.Name.Substring(6)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value -or
            (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw 'Source changed during staging.' }
        $sources[$entry.Name] = $entry.Value
    }
}
$fixture = Join-Path $project 'Assets/Editor/OptionalChest/OptionalChestValidation.cs'
[void](New-Item -ItemType Directory -Path (Split-Path $fixture) -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/optional_chest_validation.cs') -Destination $fixture -Force
$sources['fixture'] = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $work 'source-fixture.json'), ($sources | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$previous = $env:ZJCS_OPTIONAL_CHEST_OUTPUT
$log = Join-Path $work 'unity.log'
try {
    $env:ZJCS_OPTIONAL_CHEST_OUTPUT = $work
    $process = Start-Process -FilePath $Editor -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'OptionalChestValidation.Run',
        '-logFile', ('"' + $log + '"'))
} finally { $env:ZJCS_OPTIONAL_CHEST_OUTPUT = $previous }
Write-Output ('Optional chest PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Optional chest validation stopped: ' + $log)
    }
}
$process.Refresh()
$path = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $path)) { throw ('Optional chest validation failed: ' + $log) }
$report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
if (-not $report.success -or $report.checks.Count -lt 60) { throw ('Optional chest checks failed: ' + $path) }
Write-Output ('OPTIONAL_CHEST_PASSED: ' + $work)

param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe'
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
$work = Join-Path $validation ('shared-achievement-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$sources = @{}
foreach ($name in @('AchievementPanel', 'AchievementPanelBase', 'AchievementPanelItem', 'AchievementPanelData', 'ProfessionAchievementPanelSource')) {
    foreach ($suffix in @('.cs', '.cs.meta')) {
        $relative = 'Assets/Game/Runtime/Official/ProfessionUI/' + $name + $suffix
        $source = Join-Path $repository ('unity/' + $relative)
        $before = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $source -Destination (Join-Path $project $relative) -Force
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $before -or
            (Get-FileHash -LiteralPath (Join-Path $project $relative) -Algorithm SHA256).Hash -ne $before) {
            throw 'Source changed during staging.'
        }
        $sources[$relative] = $before.ToLowerInvariant()
    }
}
$fixture = Join-Path $project 'Assets/Editor/SharedAchievement/SharedAchievementValidation.cs'
[void](New-Item -ItemType Directory -Path (Split-Path $fixture) -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/achievement_panel_shared_validation.cs') -Destination $fixture -Force
$sources['fixture'] = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $work 'source-fixture.json'), ($sources | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$names = @('ZJCS_SHARED_ACHIEVEMENT_OUTPUT', 'ZJCS_SHARED_ACHIEVEMENT_RULES')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$log = Join-Path $work 'unity.log'
try {
    $env:ZJCS_SHARED_ACHIEVEMENT_OUTPUT = $work
    $env:ZJCS_SHARED_ACHIEVEMENT_RULES = Join-Path $repository 'unity/Assets/Official/Combat/rules.json'
    # No graphics/window or system input is involved in component regressions.
    $process = Start-Process -FilePath $Editor -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'AchievementPanelSharedValidation.Run',
        '-logFile', ('"' + $log + '"'))
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
Write-Output ('Shared achievement PID ' + $process.Id + ', output: ' + $work)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Shared achievement validation stopped: ' + $log)
    }
}
$process.Refresh()
$reportPath = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw ('Shared achievement validation failed: ' + $log) }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.success -or $report.checks.Count -lt 30) { throw ('Shared achievement checks failed: ' + $reportPath) }
Write-Output ('SHARED_ACHIEVEMENT_PASSED: ' + $work)

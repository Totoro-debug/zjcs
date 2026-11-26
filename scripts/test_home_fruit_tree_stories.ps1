param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = (Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\', '/')
$validationBase = Join-Path $repository 'output/validation'
if (-not $project.StartsWith($validationBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Use an independent Unity source project under output/validation.'
}
$ancestor = [IO.DirectoryInfo]::new($project)
while ($ancestor.FullName -ne $validationBase) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or
        ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'No worktrees or linked source projects.' }
    $ancestor = $ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('fruit-tree-story-graph-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-fruit-tree-unlock-story-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $receipt.sources.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath (Join-Path $repository $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Original story source changed: ' + $entry.Name)
    }
}
$relative = $receipt.output.path
if ((Get-FileHash -LiteralPath (Join-Path $repository $relative) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.output.sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $repository ($relative + '.meta')) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.output.meta_sha256) {
    throw 'The recovered original fruit-tree story asset changed.'
}
foreach ($path in @($relative, ($relative + '.meta'))) {
    Copy-Item -LiteralPath (Join-Path $repository $path) -Destination (Join-Path $project $path.Substring(6)) -Force
}
$fixture = Join-Path $project 'Assets/Editor/HomeFruitTreeStoryGraphValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_fruit_tree_story_graph_validation.cs') -Destination (Join-Path $fixture 'HomeFruitTreeStoryGraphValidation.cs') -Force
@{name='HomeFruitTreeStoryGraphValidation';includePlatforms=@('Editor');references=@('Zjcs.Offline.HomeStoryDialog')} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeFruitTreeStoryGraphValidation.asmdef') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-nographics', '-quit', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeFruitTreeStoryGraphValidation.Run',
    '-logFile', ('"' + $log + '"')
)
Write-Output ('Fruit-tree graph PID ' + $process.Id + ', log: ' + $log)
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^FRUIT_TREE_STORY_GRAPH_PASSED:' -Quiet)) {
    throw ('Original fruit-tree story graph validation failed: ' + $log)
}
Write-Output ('FRUIT_TREE_STORY_GRAPH_PASSED: ' + $work)

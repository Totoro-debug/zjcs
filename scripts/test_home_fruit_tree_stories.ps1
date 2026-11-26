param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [switch]$Dialogue,
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
$fixtureName = if ($Dialogue) { 'HomeFruitTreeDialogueValidation' } else { 'HomeFruitTreeStoryGraphValidation' }
$fixtureSource = if ($Dialogue) { 'home_fruit_tree_dialogue_validation.cs' } else { 'home_fruit_tree_story_graph_validation.cs' }
if ($Dialogue) {
    Copy-Item -LiteralPath (Join-Path $repository 'unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs') -Destination (Join-Path $project 'Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs') -Force
    $audioReceipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-fruit-tree-unlock-story-audio-source-manifest.json') -Raw | ConvertFrom-Json
    foreach ($section in @('inputs', 'outputs')) {
        foreach ($entry in $audioReceipt.$section.PSObject.Properties) {
            $source = Join-Path $repository $entry.Name
            if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
                throw ('Original fruit-tree dialogue asset differs: ' + $entry.Name)
            }
            if ($section -eq 'outputs') {
                $target = Join-Path $project $entry.Name.Substring(6)
                [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
                Copy-Item -LiteralPath $source -Destination $target -Force
            }
        }
    }
}
$fixture = Join-Path $project ('Assets/Editor/' + $fixtureName)
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('fixtures/' + $fixtureSource)) -Destination (Join-Path $fixture ($fixtureName + '.cs')) -Force
$references = @('Zjcs.Offline.HomeStoryDialog')
if ($Dialogue) { $references += @('Launch', 'Unity.ugui', 'Unity.TextMeshPro', 'Unity.Newtonsoft.Json',
    'Zjcs.Official.HomeStoryDialog', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.StoryPlayables', 'Zjcs.Official.WorldUI',
    'Zjcs.Offline.HomeHudUI', 'Zjcs.Offline.HomeStory', 'Zjcs.Offline.HomeUI', 'Zjcs.Official.ProfessionUI') }
@{name=$fixtureName;includePlatforms=@('Editor');references=$references} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture ($fixtureName + '.asmdef')) -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$modeArguments = if ($Dialogue) { @() } else { @('-batchmode', '-nographics', '-quit') }
$previous = $env:ZJCS_VALIDATION_DIR
try {
$env:ZJCS_VALIDATION_DIR = $work
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList (@(
    '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', ($fixtureName + '.Run'),
    '-logFile', ('"' + $log + '"')
) + $modeArguments)
} finally { $env:ZJCS_VALIDATION_DIR = $previous }
Write-Output ('Fruit-tree graph PID ' + $process.Id + ', log: ' + $log)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(30000)) {
    if ($timer.Elapsed.TotalMinutes -gt 20 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Fruit-tree validation stopped: ' + $log)
    }
}
$process.Refresh()
$marker = if ($Dialogue) { '^FRUIT_TREE_DIALOGUE_PASSED:' } else { '^FRUIT_TREE_STORY_GRAPH_PASSED:' }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern $marker -Quiet)) {
    throw ('Original fruit-tree story graph validation failed: ' + $log)
}
Write-Output ('FRUIT_TREE_STORY_GRAPH_PASSED: ' + $work)

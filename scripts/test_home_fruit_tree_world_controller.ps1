param(
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$Project
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$work = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($work) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) { throw 'Owned validation workspace required.' }
$marker = Get-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation repository.' }
$projectPath = (Get-Item -LiteralPath $Project).FullName.TrimEnd('\','/')
if (-not $projectPath.StartsWith($work + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Independent project required.' }
$ancestor = [IO.DirectoryInfo]::new($projectPath)
while ($ancestor -and $ancestor.FullName -ne $work) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked projects and worktrees are forbidden.' }
    $ancestor = $ancestor.Parent
}
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($projectPath.Replace('\','/'))
})
if ($running.Count) { throw 'Stop the independent project before staging test sources.' }
$source = Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeFruitTreeWorld'
$destination = Join-Path $projectPath 'Assets/Game/Runtime/Official/HomeFruitTreeWorld'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force
$fixture = Join-Path $projectPath 'Assets/Editor/HomeFruitTreeWorldController'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_fruit_tree_world_controller_validation.cs') -Destination (Join-Path $fixture 'HomeFruitTreeWorldControllerValidation.cs') -Force
@{
    name = 'HomeFruitTreeWorldControllerValidation'
    includePlatforms = @('Editor')
    references = @('Zjcs.Official.HomeFruitTreeWorld', 'Zjcs.Official.HomeFruitTreeNative', 'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput', 'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.WorldUI', 'Zjcs.Official.ProfessionUI', 'spine-unity', 'Unity.ugui')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'HomeFruitTreeWorldControllerValidation.asmdef') -Encoding utf8
$editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
$log = Join-Path $work 'fruit-tree-world-controller.log'
$arguments = @('-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2',
    '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'),
    '-executeMethod', 'HomeFruitTreeWorldControllerValidation.Run')
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_FRUIT_TREE_WORLD_CONTROLLER_PASSED' -Quiet)) { throw "Fruit-tree world controller validation failed: $log" }
Write-Output 'HOME_FRUIT_TREE_WORLD_CONTROLLER_PASSED'

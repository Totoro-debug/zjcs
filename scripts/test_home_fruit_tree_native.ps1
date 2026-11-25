param(
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$Project,
    [switch]$Render
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
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-fruit-tree-native-source-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($relative in $receipt.outputs.Keys) {
    $source = Join-Path $repository $relative
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.outputs[$relative]) { throw "Native asset differs: $relative" }
    $destination = Join-Path $projectPath $relative.Substring(6)
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
$fixture = Join-Path $projectPath 'Assets/Editor/HomeFruitTreeNative'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_fruit_tree_native_validation.cs') -Destination (Join-Path $fixture 'HomeFruitTreeNativeValidation.cs') -Force
@{
    name = 'HomeFruitTreeNativeValidation'
    includePlatforms = @('Editor')
    references = @('spine-unity', 'spine-csharp')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'HomeFruitTreeNativeValidation.asmdef') -Encoding utf8
$editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
$log = Join-Path $work 'fruit-tree-native.log'
if ($Render) {
    . (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
    $priorWork = $env:ZJCS_VALIDATION_DIR
    try {
        $env:ZJCS_VALIDATION_DIR = $work
        $arguments = @('-muteAudio', '-job-worker-count', '2', '-projectPath', ('"' + $projectPath + '"'),
            '-logFile', ('"' + $log + '"'), '-executeMethod', 'HomeFruitTreeNativeValidation.Render')
        $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList $arguments
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(30000)) {
            if ($timer.Elapsed.TotalMinutes -gt 10 -or ((Test-Path -LiteralPath $log) -and
                (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
                Stop-Process -Id $process.Id
                throw "Fruit-tree render import timed out or failed: $log"
            }
        }
        $process.Refresh()
        if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_FRUIT_TREE_RENDER_PASSED' -Quiet)) { throw "Fruit-tree render validation failed: $log" }
        Write-Output 'HOME_FRUIT_TREE_RENDER_PASSED'
    } finally { $env:ZJCS_VALIDATION_DIR = $priorWork }
    exit
}
$arguments = @('-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2',
    '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'),
    '-executeMethod', 'HomeFruitTreeNativeValidation.Run')
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_FRUIT_TREE_NATIVE_PASSED' -Quiet)) { throw "Fruit-tree native validation failed: $log" }
Write-Output 'HOME_FRUIT_TREE_NATIVE_PASSED'

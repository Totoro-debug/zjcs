param(
    [string]$Commit = 'HEAD',
    [switch]$PrepareOnly,
    [switch]$BuildOnly,
    [switch]$CleanBuildCache
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
function Invoke-ReleaseGit([string[]]$GitArguments) {
    $result = & git @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "Release Git failed: $($GitArguments -join ' ')" }
    return $result
}
# An OS file lock is released on process exit, including interrupted builds.
$output = Join-Path $repository 'output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$lock = [IO.File]::Open((Join-Path $output 'windows-release.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$environmentNames = @('ZJCS_VALIDATION_DIR','ZJCS_REPOSITORY_ROOT','ZJCS_WINDOWS_RELEASE_EXE','ZJCS_CLEAN_BUILD_CACHE')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $target = Invoke-ReleaseGit -GitArguments @('-C', $repository, 'rev-parse', '--verify', ($Commit+'^{commit}'))
    $pointer = Join-Path $output 'windows-release.json'
    if (Test-Path -LiteralPath $pointer) {
        $workspace = (Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json).workspace
        $root = [IO.Path]::GetFullPath((Get-ZjcsValidationRoot)).TrimEnd('\','/')
        $workspace = [IO.Path]::GetFullPath($workspace)
        if ([IO.Path]::GetDirectoryName($workspace) -ne $root -or
            [IO.Path]::GetFileName($workspace) -notmatch '^windows-release-[a-f0-9]{32}$') { throw 'Invalid release workspace pointer' }
        $marker = Get-Content -LiteralPath (Join-Path $workspace '.zjcs-validation.json') -Raw | ConvertFrom-Json
        if ($marker.repository -ne $repository) { throw 'Release workspace belongs to another repository' }
    } else {
        $workspace = New-ZjcsValidationWorkspace -Name 'windows-release'
        @{workspace=$workspace} | ConvertTo-Json | Set-Content -LiteralPath $pointer -Encoding utf8
    }
    $source = Join-Path $workspace 'source'
    $project = Join-Path $source 'unity'
    $normalizedProject = $project.Replace('\','/')
    $users = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -eq 'Unity.exe' -and $_.CommandLine -and
        $_.CommandLine.Replace('\','/').Contains($normalizedProject)
    })
    if ($users.Count) { throw 'The release Unity project is already in use; wait for that build to exit.' }
    if (-not (Test-Path -LiteralPath (Join-Path $source '.git'))) {
        Invoke-ReleaseGit -GitArguments @('-C',$repository,'worktree','add','--detach','--no-checkout',$source,$target)
        Invoke-ReleaseGit -GitArguments @('-C',$source,'sparse-checkout','set','unity','assets/reference')
        Invoke-ReleaseGit -GitArguments @('-C',$source,'checkout','--detach',$target)
    }
    # Restore only tracked build-mutated files. Do not rewrite unchanged sources or copy Library.
    $dirty = @(Invoke-ReleaseGit -GitArguments @('-C',$source,'diff','--name-only','HEAD'))
    foreach ($path in $dirty) { Invoke-ReleaseGit -GitArguments @('-C',$source,'restore','--source=HEAD','--staged','--worktree','--',$path) }
    $untracked = @(Invoke-ReleaseGit -GitArguments @('-C',$source,'ls-files','--others','--exclude-standard','--','unity/Assets','unity/Packages','unity/ProjectSettings'))
    if ($untracked.Count) { throw "Unexpected untracked release inputs: $($untracked -join ', ')" }
    Invoke-ReleaseGit -GitArguments @('-C',$source,'checkout','--detach',$target)
    Write-Output "Release source: $target; persistent cache: $workspace"
    $syncSeconds = $timer.Elapsed.TotalSeconds
    if ($PrepareOnly) { return }
    if (-not (Select-String -LiteralPath (Join-Path $project 'Assets/Game/Editor/ProjectTools.cs') -SimpleMatch 'ZJCS_WINDOWS_RELEASE_EXE')) {
        throw 'This commit predates the staged release builder; refusing to build directly into the live game.'
    }
    $config = Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json
    $editor = $config.editorExecutable
    if (-not [IO.Path]::IsPathRooted($editor)) { $editor = Join-Path $repository $editor }
    $env:ZJCS_VALIDATION_DIR = $workspace
    $env:ZJCS_REPOSITORY_ROOT = $source
    $env:ZJCS_WINDOWS_RELEASE_EXE = Join-Path $workspace 'Player/Zjcs.exe'
    $env:ZJCS_CLEAN_BUILD_CACHE = if ($CleanBuildCache) { '1' } else { $null }
    $log = Join-Path $workspace 'build.log'
    $build = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
        '-batchmode','-quit','-projectPath',('"'+$project+'"'),
        '-executeMethod','Zjcs.Editor.ProjectTools.BuildWindowsGame','-logFile',('"'+$log+'"'))
    $build.WaitForExit()
    if ($build.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'Windows game built to')) {
        throw "Release build failed. See $log"
    }
    $buildSeconds = $timer.Elapsed.TotalSeconds - $syncSeconds
    @{commit=$target; builtUtc=[DateTime]::UtcNow.ToString('o'); syncSeconds=$syncSeconds; buildSeconds=$buildSeconds} | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $workspace 'build.json') -Encoding utf8
    if ($BuildOnly) { return }
    if (@(Get-Process Zjcs -ErrorAction SilentlyContinue).Count) { throw 'Close the running game before publishing. The built Player and cache are retained.' }
    & python -B (Join-Path $PSScriptRoot 'publish_windows.py') --source (Join-Path $workspace 'Player') --destination 'D:/Desktop/ZjcsGame'
    if ($LASTEXITCODE -ne 0) { throw 'Windows publication failed' }
    Write-Output ('Sync {0:N1}s; build {1:N1}s; publish {2:N1}s' -f $syncSeconds,$buildSeconds,($timer.Elapsed.TotalSeconds-$syncSeconds-$buildSeconds))
    Write-Output "Published $target to D:/Desktop/ZjcsGame/Zjcs.exe"
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    $lock.Dispose()
}

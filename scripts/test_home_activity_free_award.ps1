param(
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$Project
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
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
$source = Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots'
$destination = Join-Path $projectPath 'Assets/Game/Runtime/Official/HomeActivityTipDots'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force
$fixture = Join-Path $projectPath 'Assets/Editor/HomeActivityFreeAward'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_activity_free_award_validation.cs') -Destination (Join-Path $fixture 'HomeActivityFreeAwardValidation.cs') -Force
@{
    name = 'HomeActivityFreeAwardValidation'
    includePlatforms = @('Editor')
    references = @('Zjcs.Official.HomeActivityTipDots', 'Zjcs.Official.ActivityBattleUI', 'Zjcs.Official.HomeHudInfrastructureUI', 'Zjcs.Official.ProfessionUI', 'UniTask', 'Unity.ugui')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'HomeActivityFreeAwardValidation.asmdef') -Encoding utf8
$editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
$log = Join-Path $work 'activity-free-award.log'
$oldWork = $env:ZJCS_VALIDATION_DIR
try {
    $env:ZJCS_VALIDATION_DIR = $work
    $arguments = @('-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'),
        '-executeMethod', 'HomeActivityFreeAwardValidation.Run')
    $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList $arguments
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(30000)) {
        if ($timer.Elapsed.TotalMinutes -gt 10 -or ((Test-Path -LiteralPath $log) -and
            (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
            Stop-Process -Id $process.Id
            throw "Activity free award import timed out or failed: $log"
        }
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_ACTIVITY_FREE_AWARD_PASSED' -Quiet)) { throw "Activity free award validation failed: $log" }
    Write-Output 'HOME_ACTIVITY_FREE_AWARD_PASSED'
} finally { $env:ZJCS_VALIDATION_DIR = $oldWork }

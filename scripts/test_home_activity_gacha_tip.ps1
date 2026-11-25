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
$source = Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots'
$destination = Join-Path $projectPath 'Assets/Game/Runtime/Official/HomeActivityTipDots'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force
$shared = 'Assets/Game/Runtime/Official/ActivityBattleUI/ActivityNodeInstanceInfo.cs'
Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $shared)) -Destination (Join-Path $projectPath $shared) -Force
$fixture = Join-Path $projectPath 'Assets/Editor/HomeActivityGachaTip'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_activity_gacha_tip_validation.cs') -Destination (Join-Path $fixture 'HomeActivityGachaTipValidation.cs') -Force
@{
    name = 'HomeActivityGachaTipValidation'
    includePlatforms = @('Editor')
    references = @('Zjcs.Official.HomeActivityTipDots', 'Zjcs.Official.ActivityBattleUI', 'Zjcs.Official.HomeHudInfrastructureUI', 'Zjcs.Official.ProfessionUI')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'HomeActivityGachaTipValidation.asmdef') -Encoding utf8
$editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
$log = Join-Path $work 'activity-gacha-tip.log'
$arguments = @('-batchmode', '-nographics', '-muteAudio', '-job-worker-count', '2',
    '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'),
    '-executeMethod', 'HomeActivityGachaTipValidation.Run')
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_ACTIVITY_GACHA_TIP_PASSED' -Quiet)) { throw "Activity gacha tip validation failed: $log" }
Write-Output 'HOME_ACTIVITY_GACHA_TIP_PASSED'

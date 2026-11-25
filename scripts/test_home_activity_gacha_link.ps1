param([Parameter(Mandatory)][string]$Workspace,[Parameter(Mandatory)][string]$Project)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')); $work = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
$marker = Get-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation repository.' }
$projectPath = (Get-Item -LiteralPath $Project).FullName.TrimEnd('\','/')
if (-not $projectPath.StartsWith($work + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Independent project required.' }
$source = Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots'; $destination = Join-Path $projectPath 'Assets/Game/Runtime/Official/HomeActivityTipDots'
New-Item -ItemType Directory -Path $destination -Force | Out-Null; Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force
$fixture = Join-Path $projectPath 'Assets/Editor/HomeActivityGachaLink'; New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_activity_gacha_link_validation.cs') -Destination (Join-Path $fixture 'HomeActivityGachaLinkValidation.cs') -Force
@{name='HomeActivityGachaLinkValidation';includePlatforms=@('Editor');references=@('Zjcs.Official.HomeActivityTipDots','Zjcs.Official.ActivityBattleUI','Zjcs.Official.ProfessionUI')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'HomeActivityGachaLinkValidation.asmdef') -Encoding utf8
$editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable; $log=Join-Path $work 'activity-gacha-link.log'
$arguments=@('-batchmode','-nographics','-muteAudio','-job-worker-count','2','-projectPath',('"'+$projectPath+'"'),'-logFile',('"'+$log+'"'),'-executeMethod','HomeActivityGachaLinkValidation.Run')
$process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'HOME_ACTIVITY_GACHA_LINK_PASSED' -Quiet)) { throw "Activity gacha link validation failed: $log" }
Write-Output 'HOME_ACTIVITY_GACHA_LINK_PASSED'

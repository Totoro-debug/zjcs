[CmdletBinding(DefaultParameterSetName='Play')]
param(
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateRange(1,180)][int]$TimeoutMinutes = 90,
    [Parameter(Mandatory,ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory,ParameterSetName='Player')][string]$Player
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'windows_player_snapshot.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use an independent validation session inside output/validation.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation repository.' }
if ($PSCmdlet.ParameterSetName -eq 'Play') {
    $projectPath = (Get-Item -LiteralPath $Project).FullName.TrimEnd('\','/')
    if (-not $projectPath.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Building verification requires an independent source project in this session.'
    }
    $ancestor = [IO.DirectoryInfo]::new($projectPath)
    while ($ancestor -and $ancestor.FullName -ne $workspacePath) {
        if (Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) { throw 'Building verification cannot use a worktree.' }
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Building verification cannot use a linked project.' }
        $ancestor = $ancestor.Parent
    }
    $configuration = Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json
    $executable = $configuration.editorExecutable
} else {
    $executable = Resolve-ZjcsWindowsPlayerSnapshot -PlayerPath $Player -Workspace $workspacePath -Repository $repository
}
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreePhysicalMemory -lt 4MB -or $memory.FreeVirtualMemory -lt 8MB) {
    throw 'Not enough available memory to start Unity verification.'
}
$run = Join-Path $workspacePath ('bed-' + $PSCmdlet.ParameterSetName + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
@{format=1;repository=$repository;ownerPid=$PID} | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $run '.zjcs-validation.json') -Encoding utf8
$log = Join-Path $run 'runtime.log'
$names = @('ZJCS_VALIDATION_DIR','ZJCS_REPOSITORY_ROOT','ZJCS_BATTLE_INPUT_AUTOMATION')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
function Wait-HomeVerificationProcess($OwnedProcess, [string]$OwnedLog, [string]$Stage) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $OwnedProcess.WaitForExit(30000)) {
        if ($timer.Elapsed.TotalMinutes -gt $TimeoutMinutes -or ((Test-Path -LiteralPath $OwnedLog) -and
            (Select-String -LiteralPath $OwnedLog -Pattern 'error CS\d+|Assembly with name .+ already exists|Scripts have compiler errors'))) {
            Stop-Process -Id $OwnedProcess.Id
            throw "$Stage stopped after timeout or compile failure: $OwnedLog"
        }
    }
    $OwnedProcess.Refresh()
}
try {
    $env:ZJCS_VALIDATION_DIR = $run
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    if ($PSCmdlet.ParameterSetName -eq 'Play') {
        # Finish import and compiler checks before opening a rendered Editor.
        $importLog = Join-Path $run 'import.log'
        $importArguments = @('-batchmode','-nographics','-quit','-muteAudio','-job-worker-count','2','-gc-helper-count','2',
            '-projectPath',('"'+$projectPath+'"'),'-logFile',('"'+$importLog+'"'),
            '-executeMethod','Zjcs.Editor.HomeOutputDetailValidation.Validate')
        $importProcess = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $importArguments
        $importProcess.PriorityClass = 'BelowNormal'
        Write-Output "Bed import PID=$($importProcess.Id): $run"
        Wait-HomeVerificationProcess $importProcess $importLog 'Bed import'
        if ($importProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $importLog -SimpleMatch 'HOME_OUTPUT_DETAIL_IMPORT_PASSED')) {
            throw "Bed import or native reference validation failed: $importLog"
        }
    }
    $arguments = @('-muteAudio','-job-worker-count','2','-gc-helper-count','2',
        '-logFile',('"'+$log+'"'),'--zjcs-validate-home-output-detail',('"'+$run+'"'))
    if ($PSCmdlet.ParameterSetName -eq 'Play') {
        $arguments += @('-projectPath',('"'+$projectPath+'"'),'-executeMethod','Zjcs.Editor.HomeOutputDetailValidation.Play')
    } else {
        $arguments += @('-screen-fullscreen','0','-screen-width','675','-screen-height','1463')
    }
    $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $arguments
    $process.PriorityClass = 'BelowNormal'
    Write-Output "Bed flow PID=$($process.Id): $run"
    Wait-HomeVerificationProcess $process $log 'Bed verification'
    $resultPath = Join-Path $run 'output-detail-result.json'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Bed verification produced no result: $log" }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Bed verification failed: $($result.error) ($run)" }
    foreach ($frame in @('bed-collected','bed-free-progress','bed-free-complete','bed-item-selection','bed-item-progress',
        'bed-activity-income','bed-activity-collected','bed-capacity-active','bed-capacity-expired')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run ($frame+'.png')))) { throw "Missing rendered bed frame: $frame" }
    }
    Write-Output "Bed flow passed: $($result.checks) checks; $run"
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}

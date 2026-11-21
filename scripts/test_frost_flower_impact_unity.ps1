param([Parameter(Mandatory)][string]$Project)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validationRoot = [IO.Path]::GetFullPath((Join-Path $repository 'output/validation'))
$projectPath = (Resolve-Path -LiteralPath $Project).Path
if (-not $projectPath.StartsWith($validationRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Unity test project must be an isolated copy under output/validation.'
}
$config = Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json
$editor = if ([IO.Path]::IsPathRooted($config.editorExecutable)) { $config.editorExecutable }
    else { Join-Path $repository $config.editorExecutable }
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) { throw "Unity Editor not found: $editor" }
$run = Join-Path $validationRoot ('frost-flower-native-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$oldRoot = $env:ZJCS_REPOSITORY_ROOT
$oldValidation = $env:ZJCS_VALIDATION_DIR
$oldAutomation = $env:ZJCS_BATTLE_INPUT_AUTOMATION
$oldFlame = $env:ZJCS_FROST_FLOWER_IMPACT_ACCEPTANCE
$process = $null
try {
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $env:ZJCS_VALIDATION_DIR = $run
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    $env:ZJCS_FROST_FLOWER_IMPACT_ACCEPTANCE = '1'
    $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
        '-projectPath', ('"' + $projectPath + '"'),
        '-executeMethod', 'Zjcs.Editor.Combat.OfficialBattleHitPlaybackAcceptance.Run',
        '-logFile', ('"' + (Join-Path $run 'editor.log') + '"'), '-muteAudio',
        '-job-worker-count', '2', '-gc-helper-count', '2')
    $process.PriorityClass = 'BelowNormal'
    $deadline = [DateTime]::UtcNow.AddMinutes(20)
    $resultPath = Join-Path $run 'native-hit-result.json'
    $resultSeen = $null
    $stoppedAfterResult = $false
    while (-not $process.WaitForExit(5000)) {
        $os = Get-CimInstance Win32_OperatingSystem
        if ($os.FreePhysicalMemory -lt 4194304 -or $os.FreeVirtualMemory -lt 8388608) {
            throw 'The isolated Unity validation stopped to preserve available memory.'
        }
        if (-not $resultSeen -and (Test-Path -LiteralPath $resultPath)) { $resultSeen = [DateTime]::UtcNow }
        if ($resultSeen -and [DateTime]::UtcNow -gt $resultSeen.AddSeconds(20)) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
            $stoppedAfterResult = $true
            break
        }
        if ([DateTime]::UtcNow -gt $deadline) { throw "Frost flower validation timed out: $run" }
    }
    if (($process.ExitCode -ne 0 -and -not $stoppedAfterResult) -or -not (Test-Path -LiteralPath $resultPath)) {
        throw "Unity validation failed (exit $($process.ExitCode)): $run"
    }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw "Frost flower validation failed: $($result.error)" }
    Write-Output "Original frost flower impact validation passed: $run"
    if ($stoppedAfterResult) { Write-Output 'The owned Unity Editor was stopped after it failed to exit following the completed result.' }
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    $env:ZJCS_REPOSITORY_ROOT = $oldRoot
    $env:ZJCS_VALIDATION_DIR = $oldValidation
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = $oldAutomation
    $env:ZJCS_FROST_FLOWER_IMPACT_ACCEPTANCE = $oldFlame
}

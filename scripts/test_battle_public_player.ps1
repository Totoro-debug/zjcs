param([Parameter(Mandatory)][string]$Workspace)
$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = [System.IO.Path]::GetFullPath($Workspace)
$validationRoot = Join-Path $repository 'output/validation'
if ([System.IO.Path]::GetDirectoryName($workspacePath) -ne $validationRoot -or
    -not (Test-Path -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json'))) {
    throw 'Use one marked session directly inside output/validation.'
}
$playerPath = Join-Path $workspacePath 'PublicBattleValidation.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw "Build the validation Player first: $playerPath" }
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$previousWorkspace = $env:ZJCS_VALIDATION_DIR
$previousRepository = $env:ZJCS_REPOSITORY_ROOT
$process = $null
try {
    $env:ZJCS_VALIDATION_DIR = $workspacePath
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $resultPath = Join-Path $workspacePath 'public-battle-result.json'
    if (Test-Path -LiteralPath $resultPath) { throw 'Use a fresh session so a previous result cannot pass this run.' }
    $process = Start-UnityOnInternalDisplay -Editor $playerPath -ArgumentList @(
        '-muteAudio',
        '-screen-fullscreen', '0', '-screen-width', '450', '-screen-height', '996',
        '-logFile', ('"' + (Join-Path $workspacePath 'windows-player.log') + '"'),
        '--zjcs-validate-public-battle', ('"' + $workspacePath + '"'))
    $deadline = [DateTime]::UtcNow.AddMinutes(15)
    while (-not $process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Public battle Player did not exit within 15 minutes.' }
    }
    $process.WaitForExit()
    if (-not (Test-Path -LiteralPath $resultPath)) { throw 'The Player did not write its result.' }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Public battle Player failed: $($result.error)" }
    foreach ($scenario in @('movement', 'monster', 'cost', 'lifesteal', 'status-actions', 'critical', 'block', 'special-appearance', 'training-monster')) {
        foreach ($phase in @('preparing', 'paused', 'skill', 'hit', 'native-damage-ui', 'settled')) {
            if (-not (Test-Path -LiteralPath (Join-Path $workspacePath "public-$scenario-$phase.png"))) {
                throw "Missing actual Player capture: public-$scenario-$phase.png"
            }
        }
    }
    foreach ($scenario in @('movement', 'monster', 'cost', 'lifesteal', 'critical', 'block', 'special-appearance', 'training-monster')) {
        if (-not (Test-Path -LiteralPath (Join-Path $workspacePath "public-$scenario-native-hurt.png"))) {
            throw "Missing actual Player hurt capture: public-$scenario-native-hurt.png"
        }
    }
    foreach ($capture in @('public-cost-native-cost-ui', 'public-lifesteal-derived-cure-ui', 'public-status-actions-native-action-ui')) {
        if (-not (Test-Path -LiteralPath (Join-Path $workspacePath ($capture + '.png')))) { throw "Missing actual Player capture: $capture" }
    }
    Write-Output "Public battle Windows Player passed all nine shared interaction scenarios: $workspacePath"
}
finally {
    if ($process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $env:ZJCS_VALIDATION_DIR = $previousWorkspace
    $env:ZJCS_REPOSITORY_ROOT = $previousRepository
}

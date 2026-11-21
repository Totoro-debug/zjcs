param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][ValidateCount(1,2)][string[]]$Cases
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
$projectPath = (Get-Item -LiteralPath $Project).FullName
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/') -or
    -not $projectPath.StartsWith($workspacePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a marked session and its isolated Unity project.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation workspace repository.' }
foreach ($caseName in $Cases) {
    if ($caseName -notmatch '^(10512|10560|10562)/(damage|shield|dodge|blinding|immunity)$') {
        throw "Invalid hit passive case: $caseName"
    }
}
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreePhysicalMemory -lt 4194304 -or $memory.FreeVirtualMemory -lt 8388608) {
    throw 'Available memory is too low to start isolated skill validation.'
}
$run = Join-Path $workspacePath ('b'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $run | Out-Null
$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository; ZJCS_VALIDATION_DIR = $run
    ZJCS_SKILLS_AUTORUN = '1'; ZJCS_SKILLS_PASSIVE_FLOW = '1'
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'; ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    ZJCS_FIRST_PROMOTION_SKILL = '10000'; ZJCS_DIRECTIONAL_SKILL_FACING = 'right'
    ZJCS_HIT_PASSIVE_BATCH = '1'; ZJCS_HIT_PASSIVE_CASES = ($Cases -join ',')
    ZJCS_HIT_PASSIVE_ITEM = '10512'; ZJCS_HIT_PASSIVE_MODE = 'damage'
}
$previous = @{}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key,'Process')
        [Environment]::SetEnvironmentVariable($key,$settings[$key],'Process')
    }
    $editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
    $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
        '-projectPath', ('"'+$projectPath+'"'), '-muteAudio', '-job-worker-count','2', '-gc-helper-count','2',
        '-logFile', ('"'+(Join-Path $run 'player.log')+'"'))
    $process.PriorityClass = 'BelowNormal'
    $process.WaitForExit()
    $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Hit passive batch Play Mode failed: $run" }
    foreach ($caseName in $Cases) {
            $parts = $caseName.Split('/')
            $item = [int]$parts[0]; $mode = $parts[1]
            $case = Join-Path $run "$item-$mode"
            $caseResult = Get-Content -LiteralPath (Join-Path $case 'result.json') -Raw | ConvertFrom-Json
            if (-not $caseResult.passed) { throw "Hit passive case failed: $case" }
            $active = if ($item -eq 10562) { 10008 } else { 10000 }
            foreach ($phase in 1..4) {
                if (-not (Test-Path -LiteralPath (Join-Path $case "first-promotion-$active-phase-$phase.png"))) {
                    throw "Source action frame missing: $case phase $phase"
                }
            }
            if ($mode -in @('damage','shield') -and -not (Test-Path -LiteralPath (Join-Path $case 'hit-passive-native.png'))) {
                throw "Native passive frame missing: $case"
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch "[Hit passive] Original child dispatch and checkpoint passed: $item/$mode")) {
                throw "Native passive assertion missing: $case"
            }
    }
    Write-Output "Hit passive Play Mode cases passed: $run"
}
finally {
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}

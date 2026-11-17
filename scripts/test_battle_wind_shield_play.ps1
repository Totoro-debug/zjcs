param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Workspace
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
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreePhysicalMemory -lt 5242880 -or $memory.FreeVirtualMemory -lt 8388608) {
    throw 'Available memory is too low to start isolated skill validation.'
}
$run = Join-Path $workspacePath ('w'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $run | Out-Null
$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository; ZJCS_VALIDATION_DIR = $run
    ZJCS_SKILLS_AUTORUN = '1'; ZJCS_SKILLS_PASSIVE_FLOW = '1'
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'; ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    ZJCS_WIND_SHIELD_START = '1'; ZJCS_FIRST_PROMOTION_SKILL = '10008'
    ZJCS_DIRECTIONAL_SKILL_FACING = 'right'; ZJCS_HIT_PASSIVE_ITEM = $null
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
    while (-not $process.WaitForExit(5000)) {
        $memory = Get-CimInstance Win32_OperatingSystem
        if ($memory.FreePhysicalMemory -lt 5242880 -or $memory.FreeVirtualMemory -lt 8388608) {
            Stop-Process -Id $process.Id -Force
            throw "Stopped isolated Play Mode to protect foreground activity: $run"
        }
    }
    $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Wind shield Play Mode failed: $run" }
    if (-not (Test-Path -LiteralPath (Join-Path $run 'wind-shield-start-native.png')) -or
        -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Wind shield] Native view and finished battle passed.')) {
        throw "Wind shield view evidence is missing: $run"
    }
    Write-Output "Wind shield Play Mode passed: $run"
}
finally {
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}

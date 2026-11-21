param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10049,10060,10066,10127,10129)][int]$ItemId = 10060
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
if ($memory.FreePhysicalMemory -lt 4194304 -or $memory.FreeVirtualMemory -lt 8388608) {
    throw 'Available memory is too low to start isolated skill validation.'
}
$run = Join-Path $workspacePath ('a'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $run | Out-Null
$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository; ZJCS_VALIDATION_DIR = $run
    ZJCS_SKILLS_AUTORUN = '1'; ZJCS_SKILLS_PASSIVE_FLOW = '1'
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'; ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    ZJCS_AUTO_START_ACTIVE = '1'; ZJCS_FIRST_PROMOTION_SKILL = [string]$ItemId
    ZJCS_DIRECTIONAL_SKILL_FACING = 'right'; ZJCS_HIT_PASSIVE_ITEM = $null
    ZJCS_WIND_SHIELD_START = $null
    ZJCS_SKILLS_PREFLIGHT_TOKEN = [guid]::NewGuid().ToString('N')
    ZJCS_SKILLS_PREFLIGHT_RECEIPT = (Join-Path $run 'preflight.json')
    ZJCS_SKILLS_PREFLIGHT_STAGE = $null
}
$previous = @{}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key,'Process')
        [Environment]::SetEnvironmentVariable($key,$settings[$key],'Process')
    }
    $editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
    # Run full asset validation in its own process, so its loaded assets do not
    # overlap Play Mode's scene and animation memory. The receipt is per invocation.
    [Environment]::SetEnvironmentVariable('ZJCS_SKILLS_AUTORUN', $null, 'Process')
    foreach ($stage in @('project','skills')) {
        [Environment]::SetEnvironmentVariable('ZJCS_SKILLS_PREFLIGHT_STAGE', $stage, 'Process')
        $preflight = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
            '-batchmode', '-quit', '-projectPath', ('"'+$projectPath+'"'), '-muteAudio',
            '-job-worker-count','2', '-gc-helper-count','2',
            '-executeMethod','Zjcs.Editor.Skills.SkillPlayModeAcceptance.ValidateOpeningAssets',
            '-logFile', ('"'+(Join-Path $run "preflight-$stage.log")+'"'))
        $preflight.PriorityClass = 'BelowNormal'
        while (-not $preflight.WaitForExit(5000)) {
            $memory = Get-CimInstance Win32_OperatingSystem
            if ($memory.FreePhysicalMemory -lt 4194304 -or $memory.FreeVirtualMemory -lt 8388608) {
                Stop-Process -Id $preflight.Id -Force
                throw "Stopped opening asset preflight to protect foreground activity: $run"
            }
        }
        if ($preflight.ExitCode -ne 0 -or -not (Test-Path -LiteralPath ($settings.ZJCS_SKILLS_PREFLIGHT_RECEIPT+".$stage"))) {
            throw "Opening asset preflight failed: $run"
        }
    }
    [Environment]::SetEnvironmentVariable('ZJCS_SKILLS_PREFLIGHT_STAGE', $null, 'Process')
    [Environment]::SetEnvironmentVariable('ZJCS_SKILLS_AUTORUN', '1', 'Process')
    $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
        '-projectPath', ('"'+$projectPath+'"'), '-muteAudio', '-job-worker-count','2', '-gc-helper-count','2',
        '-logFile', ('"'+(Join-Path $run 'player.log')+'"'))
    $process.PriorityClass = 'BelowNormal'
    while (-not $process.WaitForExit(5000)) {
        $memory = Get-CimInstance Win32_OperatingSystem
        if ($memory.FreePhysicalMemory -lt 4194304 -or $memory.FreeVirtualMemory -lt 8388608) {
            Stop-Process -Id $process.Id -Force
            throw "Stopped isolated Play Mode to protect foreground activity: $run"
        }
    }
    $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Opening skill Play Mode failed: $run" }
    foreach ($phase in 1..4) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "first-promotion-$ItemId-phase-$phase.png"))) {
            throw "Original opening action frame $phase is missing: $run"
        }
    }
    if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch "[Opening skill] Original pre-battle cast, cooldown and checkpoint passed: $ItemId")) {
        throw "Opening skill gameplay assertion is missing: $run"
    }
    Write-Output "Opening skill Play Mode passed: $run"
}
finally {
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}

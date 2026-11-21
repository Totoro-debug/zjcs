param(
    [Parameter(Mandatory,ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory,ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10512,10560,10562)][int[]]$SkillIds = @(10512,10560,10562),
    [ValidateSet('damage','shield','dodge','blinding','immunity')][string[]]$Modes = @('damage','shield','dodge','blinding','immunity')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use a session created by New-ZjcsValidationWorkspace.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation workspace repository.' }
$projectPath = $null
if ($PSCmdlet.ParameterSetName -eq 'Play') {
    $projectPath = (Get-Item -LiteralPath $Project).FullName
    if (-not $projectPath.StartsWith($workspacePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Use an isolated Unity project inside the marked workspace.'
    }
    $editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
}
$oldItem = [Environment]::GetEnvironmentVariable('ZJCS_HIT_PASSIVE_ITEM','Process')
$oldMode = [Environment]::GetEnvironmentVariable('ZJCS_HIT_PASSIVE_MODE','Process')
$previous = @{}
foreach ($name in @('ZJCS_REPOSITORY_ROOT','ZJCS_VALIDATION_DIR','ZJCS_SKILLS_AUTORUN',
    'ZJCS_SKILLS_PASSIVE_FLOW','ZJCS_GRAPH_CAPABILITY_ACCEPTANCE','ZJCS_BATTLE_INPUT_AUTOMATION',
    'ZJCS_FIRST_PROMOTION_SKILL','ZJCS_DIRECTIONAL_SKILL_FACING')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
}
try {
    foreach ($item in $SkillIds) {
        foreach ($mode in $Modes) {
            $memory = Get-CimInstance Win32_OperatingSystem
            if ($memory.FreePhysicalMemory -lt 4194304 -or $memory.FreeVirtualMemory -lt 8388608) {
                throw 'Available memory is too low to start isolated skill validation.'
            }
            $active = if ($item -eq 10562) { 10008 } else { 10000 }
            $existing = @(Get-ChildItem -LiteralPath $workspacePath -Directory | ForEach-Object { $_.FullName })
            $env:ZJCS_HIT_PASSIVE_ITEM = [string]$item
            $env:ZJCS_HIT_PASSIVE_MODE = $mode
            if ($PSCmdlet.ParameterSetName -eq 'Player') {
                & (Join-Path $PSScriptRoot 'test_first_promotion_skill_player.ps1') -Player $Player -Workspace $workspacePath -ItemIds $active -Directions right
            } else {
                $run = Join-Path $workspacePath ("hit-passive-play-$item-$mode-"+[guid]::NewGuid().ToString('N'))
                New-Item -ItemType Directory -Path $run | Out-Null
                $env:ZJCS_REPOSITORY_ROOT = $repository
                $env:ZJCS_VALIDATION_DIR = $run
                $env:ZJCS_SKILLS_AUTORUN = '1'
                $env:ZJCS_SKILLS_PASSIVE_FLOW = '1'
                $env:ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'
                $env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'
                $env:ZJCS_FIRST_PROMOTION_SKILL = [string]$active
                $env:ZJCS_DIRECTIONAL_SKILL_FACING = 'right'
                $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
                    '-projectPath', ('"'+$projectPath+'"'), '-muteAudio',
                    '-logFile', ('"'+(Join-Path $run 'player.log')+'"'))
                $process.PriorityClass = 'BelowNormal'
                $process.WaitForExit()
                $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
                if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Play Mode failed: $item/$mode ($run)" }
            }
            $created = @(Get-ChildItem -LiteralPath $workspacePath -Directory | Where-Object { $_.FullName -notin $existing })
            if ($created.Count -ne 1) { throw "Expected one Player run for $item/$mode." }
            $run = $created[0].FullName
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch "[Hit passive] Original child dispatch and checkpoint passed: $item/$mode")) {
                throw "Native hit passive assertions missing: $item/$mode ($run)"
            }
            foreach ($phase in 1..4) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "first-promotion-$active-phase-$phase.png"))) {
                    throw "Native source-action frame missing: $item/$mode phase $phase ($run)"
                }
            }
            if ($mode -in @('damage','shield') -and -not (Test-Path -LiteralPath (Join-Path $run 'hit-passive-native.png'))) {
                throw "Native passive frame missing: $item/$mode ($run)"
            }
        }
    }
    Write-Output "Hit passive $($PSCmdlet.ParameterSetName) checks passed: $workspacePath"
}
finally {
    [Environment]::SetEnvironmentVariable('ZJCS_HIT_PASSIVE_ITEM',$oldItem,'Process')
    [Environment]::SetEnvironmentVariable('ZJCS_HIT_PASSIVE_MODE',$oldMode,'Process')
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}

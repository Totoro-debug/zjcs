param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [int[]]$ItemIds = @(),
    [ValidateSet('up','down','left','right')][string[]]$Directions = @('up','down','left','right')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')

$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = (Get-Item -LiteralPath $Player).FullName
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation workspace repository.' }

$manifestPath = Join-Path $repository 'assets/reference/battle-rules/third-profession-skill-migration-source-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$active = @($manifest.skills | Where-Object { $_.item_id -lt 10500 -and $_.entity_id -gt 0 } |
    Select-Object -ExpandProperty item_id | Sort-Object)
if ($active.Count -ne 20) { throw "Third-profession active skill scope changed: $($active.Count)" }
if ($ItemIds.Count -eq 0) { $ItemIds = $active }
foreach ($item in $ItemIds) { if ($item -notin $active) { throw "Not a third-profession active skill: $item" } }

$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'
    ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    ZJCS_FIRST_PROMOTION_SKILL = ''
    ZJCS_DIRECTIONAL_SKILL_FACING = ''
    ZJCS_VALIDATION_DIR = ''
}
$previous = @{}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    foreach ($item in $ItemIds) {
        foreach ($direction in $Directions) {
            $run = Join-Path $workspacePath ("third-skill-$item-$direction-" + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $run | Out-Null
            $env:ZJCS_FIRST_PROMOTION_SKILL = [string]$item
            $env:ZJCS_DIRECTIONAL_SKILL_FACING = $direction
            $env:ZJCS_VALIDATION_DIR = $run
            $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
                '-screen-fullscreen','0','-screen-width','675','-screen-height','1463','-muteAudio',
                '-logFile', ('"' + (Join-Path $run 'player.log') + '"'), '--zjcs-validate-skills', ('"' + $run + '"'))
            $process.PriorityClass = 'BelowNormal'
            while (-not $process.WaitForExit(30000)) { Write-Output "Running $item $direction : $run" }
            $resultPath = Join-Path $run 'result.json'
            if (-not (Test-Path $resultPath)) { throw "Player produced no result: $item $direction ($run)" }
            $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            if ($process.ExitCode -ne 0 -or -not $result.passed) {
                throw "Player failed for $item $direction : $($result.error) ($run)"
            }
            foreach ($phase in 1..4) {
                if (-not (Test-Path (Join-Path $run "first-promotion-$item-phase-$phase.png"))) {
                    throw "Missing rendered phase $phase for $item $direction ($run)"
                }
            }
            Write-Output "Passed $item $direction : $run"
        }
    }
}
finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}

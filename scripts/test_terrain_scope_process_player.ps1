param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10029,10055,10072)][int]$Skill = 10072,
    [ValidateSet('up','down','left','right')][string]$Direction = 'right'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = (Get-Item -LiteralPath $Player).FullName
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use a session created by New-ZjcsValidationWorkspace.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation workspace repository.' }
if ($Skill -ne 10072 -and $Direction -ne 'up') { throw 'Original movement-burning fixtures require facing up.' }
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
$run = Join-Path $workspacePath ('terrain-process-' + $Skill + '-' + $Direction + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$processIds = @()
try {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $env:ZJCS_SKILLS_VALIDATE_SUMMON_ONLY = '1'
    $env:ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    $env:ZJCS_FIRST_PROMOTION_SKILL = [string]$Skill
    $env:ZJCS_DIRECTIONAL_SKILL_FACING = $Direction
    $prefix = if ($Skill -eq 10072) { 'hit-scope' } else { 'move-scope' }
    if ($Skill -eq 10072) {
        $env:ZJCS_STATUS_HIT_SCOPE_ACCEPTANCE = '1'
        $env:ZJCS_HIT_SCOPE_COLD_SCENE = '1'
    } else {
        $env:ZJCS_STATUS_MOVE_SCOPE_ACCEPTANCE = '1'
        $env:ZJCS_MOVE_SCOPE_COLD_SCENE = '1'
    }
    foreach ($phase in @('prepare','resume')) {
        $path = Join-Path $run $phase
        New-Item -ItemType Directory -Path $path | Out-Null
        $env:ZJCS_VALIDATION_DIR = $path
        $env:ZJCS_TERRAIN_PROCESS_PHASE = $phase
        $env:ZJCS_TERRAIN_PROCESS_HANDOFF = Join-Path $run 'prepare/process-handoff.json'
        $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463','-muteAudio',
            '-logFile', ('"' + (Join-Path $path 'player.log') + '"'),
            '--zjcs-validate-skills', ('"' + $path + '"'))
        $process.PriorityClass = 'BelowNormal'
        $processIds += $process.Id
        while (-not $process.WaitForExit(30000)) { Write-Output "Terrain process $Skill $phase : $path" }
        if ($process.ExitCode -ne 0) { throw "Native process failed: $phase ($path)" }
        $resultFile = if ($phase -eq 'prepare') { 'process-prepared.json' } else { 'result.json' }
        if (-not (Get-Content -LiteralPath (Join-Path $path $resultFile) -Raw | ConvertFrom-Json).passed) {
            throw "Native process assertions failed: $phase ($path)"
        }
    }
    if ($processIds[0] -eq $processIds[1]) { throw 'Two distinct native processes are required.' }
    $handoff = Get-Content -LiteralPath (Join-Path $run 'prepare/process-handoff.json') -Raw | ConvertFrom-Json
    $minimum = if ($Skill -eq 10072) { 2 } else { 5 }
    if ($handoff.ProcessId -ne $processIds[0] -or $handoff.ItemId -ne $Skill -or
        $handoff.HitScope -ne ($Skill -eq 10072) -or $handoff.TriggerCount -le 0 -or
        $handoff.GridIds.Count -lt $minimum -or @($handoff.GridIds | Sort-Object -Unique).Count -ne $handoff.GridIds.Count) {
        throw 'Original terrain identities or passive counters were not prepared in the exited process.'
    }
    $before = Get-FileHash -LiteralPath (Join-Path $run 'prepare/process-checkpoint-before.json') -Algorithm SHA256
    $after = Get-FileHash -LiteralPath (Join-Path $run 'resume/process-checkpoint-after.json') -Algorithm SHA256
    if ($before.Hash -cne $after.Hash) { throw 'Ordinary save restoration changed terrain lifetime, RNG or receipts.' }
    foreach ($name in @('terrain-process-restored',"$prefix-restored-expired",'process-leave-confirm','process-result','process-return')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "resume/$name.png"))) { throw "Resumed native frame missing: $name" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $run 'prepare/terrain-process-before-exit.png'))) {
        throw 'Original fire was not captured before the real application exit.'
    }
    if ($Skill -eq 10055 -and -not (Test-Path -LiteralPath (Join-Path $run 'prepare/move-scope-native-walking.png'))) {
        throw 'Original walking action was not observed before process exit.'
    }
    foreach ($message in @('[Terrain process] Different Windows process restored the exact ordinary checkpoint',
        '[Terrain process] Every saved original ground status/entity/graph retired without replaying the passive.')) {
        if (-not (Select-String -LiteralPath (Join-Path $run 'resume/player.log') -SimpleMatch $message)) {
            throw "Resumed native assertion missing: $message"
        }
    }
    @{ passed=$true; processIds=$processIds; checkpointSha256=$before.Hash } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Terrain scope native process quit/resume passed: $run"
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}

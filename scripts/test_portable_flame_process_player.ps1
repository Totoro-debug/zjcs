param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace
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
$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository
    ZJCS_VALIDATION_DIR = ''
    ZJCS_SKILLS_VALIDATE_SUMMON_ONLY = '1'
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'
    ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    ZJCS_FIRST_PROMOTION_SKILL = '10061'
    ZJCS_DIRECTIONAL_SKILL_FACING = 'up'
    ZJCS_PORTABLE_FLAME_LIFECYCLE = '1'
    ZJCS_PORTABLE_FLAME_COLD_SCENE = '1'
    ZJCS_PORTABLE_FLAME_PROCESS_PHASE = ''
    ZJCS_PORTABLE_FLAME_PROCESS_HANDOFF = ''
}
$conflicting = @(Get-ChildItem Env:ZJCS_* | Where-Object { $_.Value -and -not $settings.ContainsKey($_.Name) })
if ($conflicting.Count -gt 0) { throw ('Use an isolated validation environment; unexpected settings: ' + ($conflicting.Name -join ', ')) }
$previous = @{}
$run = Join-Path $workspacePath ('portable-flame-process-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$processIds = @()
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    foreach ($phase in @('prepare','resume')) {
        $path = Join-Path $run $phase
        New-Item -ItemType Directory -Path $path | Out-Null
        $env:ZJCS_VALIDATION_DIR = $path
        $env:ZJCS_PORTABLE_FLAME_PROCESS_PHASE = $phase
        $env:ZJCS_PORTABLE_FLAME_PROCESS_HANDOFF = Join-Path $run 'prepare/process-handoff.json'
        $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile', ('"' + (Join-Path $path 'player.log') + '"'),
            '--zjcs-validate-skills', ('"' + $path + '"'))
        $process.PriorityClass = 'BelowNormal'
        $processIds += $process.Id
        while (-not $process.WaitForExit(30000)) { Write-Output "Portable flame process $phase : $path" }
        if ($process.ExitCode -ne 0) { throw "Fire-ring native process failed: $phase ($path)" }
        $resultFile = if ($phase -eq 'prepare') { 'process-prepared.json' } else { 'result.json' }
        if (-not (Get-Content -LiteralPath (Join-Path $path $resultFile) -Raw | ConvertFrom-Json).passed) {
            throw "Fire-ring native process assertions failed: $phase ($path)"
        }
        if (Select-String -LiteralPath (Join-Path $path 'player.log') -Pattern @(
            '^[\w.]*Exception(?:\s|:)', '^GameObjects can not be made active when they are being destroyed\.')) {
            throw "Fire-ring native process reported a runtime or shutdown failure: $phase ($path)"
        }
    }
    if ($processIds[0] -eq $processIds[1]) { throw 'Two distinct native processes are required.' }
    $handoff = Get-Content -LiteralPath (Join-Path $run 'prepare/process-handoff.json') -Raw | ConvertFrom-Json
    if ($handoff.ProcessId -ne $processIds[0] -or $handoff.ItemId -ne 10061 -or $handoff.SkillId -ne 11200 -or
        $handoff.Owners.Count -ne 4 -or @($handoff.Owners | Sort-Object -Unique).Count -ne 4 -or -not $handoff.SoundEnded) {
        throw 'Fire-ring handoff did not originate in the exited original-skill process.'
    }
    $before = Get-FileHash -LiteralPath (Join-Path $run 'prepare/process-checkpoint-before.json') -Algorithm SHA256
    $after = Get-FileHash -LiteralPath (Join-Path $run 'resume/process-checkpoint-after.json') -Algorithm SHA256
    if ($before.Hash -cne $after.Hash) { throw 'Fire-ring process resume changed the persisted checkpoint.' }
    foreach ($phase in 1..4) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "prepare/first-promotion-10061-phase-$phase.png"))) {
            throw "Original fire-ring cast phase missing: $phase"
        }
    }
    foreach ($name in @('portable-flame-party','portable-flame-restored','portable-flame-before-process-exit')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "prepare/$name.png"))) { throw "Initial fire-ring frame missing: $name" }
    }
    foreach ($name in @('portable-flame-after-process-resume','portable-flame-owner-round-2',
        'portable-flame-ended','process-result','process-return')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "resume/$name.png"))) { throw "Resumed fire-ring frame missing: $name" }
    }
    foreach ($message in @('[Portable flame process] Different process, identical ordinary weekly checkpoint',
        '[Portable flame lifecycle] Four friendly original status views, disk rebind, two personal rounds and retirement passed.',
        '[Portable flame sound] Original Skill_10061 native playback and completion passed.')) {
        if (-not (Select-String -LiteralPath (Join-Path $run 'resume/player.log') -SimpleMatch $message)) {
            throw "Resumed fire-ring assertion missing: $message"
        }
    }
    @{ passed=$true; processIds=$processIds; checkpointSha256=$before.Hash } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Portable flame native process exit/resume passed: $run"
}
finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}

param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use a marked validation workspace.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation repository.' }
if ($PSCmdlet.ParameterSetName -eq 'Play') {
    $projectPath = (Get-Item -LiteralPath $Project).FullName
    $executable = 'D:/Tool/Unity/Editor/Unity.exe'
    if (-not (Test-Path -LiteralPath $executable)) { throw 'Configured Unity Editor is absent.' }
} else { $executable = (Get-Item -LiteralPath $Player).FullName }
$run = Join-Path $workspacePath ('portable-flame-ui-' + $PSCmdlet.ParameterSetName + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$log = Join-Path $run 'runtime.log'
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
try {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $env:ZJCS_VALIDATION_DIR = $run
    $env:ZJCS_SKILLS_PORTABLE_FLAME_FLOW = '1'
    if ($PSCmdlet.ParameterSetName -eq 'Play') {
        $env:ZJCS_SKILLS_AUTORUN = '1'
        $env:ZJCS_SKILLS_HOME_ONLY = '1'
        $arguments = @('-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'))
    } else {
        $arguments = @('-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile', ('"' + $log + '"'), '--zjcs-validate-skill-damage', ('"' + $run + '"'))
    }
    $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $arguments
    $process.PriorityClass = 'BelowNormal'
    Write-Output "Portable flame skill-page flow PID=$($process.Id): $run"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(30000)) {
        if ($timer.Elapsed.TotalMinutes -gt 25 -or ((Test-Path -LiteralPath $log) -and
            (Select-String -LiteralPath $log -Pattern 'error CS\d+'))) {
            Stop-Process -Id $process.Id
            throw "Skill-page flow stopped after timeout or compile failure: $run"
        }
        Write-Output "Portable flame skill-page flow remains active: $run"
    }
    $process.Refresh()
    @{ ProcessId=$process.Id; ExitCode=$process.ExitCode; ElapsedSeconds=$timer.Elapsed.TotalSeconds } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'process-exit.json')
    if (-not (Test-Path -LiteralPath (Join-Path $run 'result.json'))) {
        throw "Skill-page flow produced no success result; exit=$($process.ExitCode), log=$log"
    }
    $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Skill-page flow failed: $($result.error) ($run)" }
    foreach ($text in @(
        '[Portable flame UI] Fragment acquisition, original slot input, level/rank cultivation and disk reload passed.',
        '[Portable flame UI] Saved Fighter 10061 assembled with rank=2 level=2, caster-owned status/damage, original result and return passed.')) {
        if (-not (Select-String -LiteralPath $log -SimpleMatch $text)) { throw "Missing skill-page assertion: $text" }
    }
    foreach ($phase in @('character','pieces','merge-confirm','acquired-detail','equipped','level-up','rank-up','reloaded',
        'settings','preparation','battle','result','return')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-ui-$phase.png"))) {
            throw "Missing skill-page runtime frame: $phase ($run)"
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $run 'portable-flame-ui-checkpoint.json'))) {
        throw "The completed skill-page battle checkpoint is absent: $run"
    }
    Write-Output "Portable flame skill-page flow passed: $run"
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($name in $previous.Keys) { Set-Item ('Env:' + $name) $previous[$name] }
}

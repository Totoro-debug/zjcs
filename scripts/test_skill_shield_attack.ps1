param(
    [Parameter(Mandatory,ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory,ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory)][string]$Workspace
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath=(Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')){throw 'Use one marked validation workspace.'}
$marker=Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw|ConvertFrom-Json
if($marker.format -ne 1 -or $marker.repository -ne $repository){throw 'Wrong validation workspace.'}
if($PSCmdlet.ParameterSetName -eq 'Play'){
    $projectPath=(Get-Item -LiteralPath $Project).FullName
    if(-not $projectPath.StartsWith($workspacePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use an isolated Unity project inside the marked workspace.'}
    $executable=(Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw|ConvertFrom-Json).editorExecutable
} else {
    $executable=(Get-Item -LiteralPath $Player).FullName
    if(-not $executable.StartsWith($workspacePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use the isolated Windows Player.'}
}
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_ACTIVE_LOADOUT_ACCEPTANCE='1'
    $env:ZJCS_SHIELD_ATTACK_ACCEPTANCE='1'
    $env:ZJCS_SKILLS_VALIDATE_SUMMON_ONLY='1'
    if($PSCmdlet.ParameterSetName -eq 'Play'){
        $env:ZJCS_SKILLS_AUTORUN='1'
        $env:ZJCS_SKILLS_PASSIVE_FLOW='1'
    }
    foreach($mode in @('shield-attack')){
        $run=Join-Path $workspacePath ("shield-attack-$($PSCmdlet.ParameterSetName)-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_VALIDATION_DIR=$run
        $env:ZJCS_ACTIVE_LOADOUT_MODE=$mode
        $log=Join-Path $run 'runtime.log'
        $arguments=@('-logFile',('"'+$log+'"'))
        if($PSCmdlet.ParameterSetName -eq 'Play'){
            $arguments+=@('-projectPath',('"'+$projectPath+'"'))
        } else {
            $arguments+=@('-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
                '--zjcs-validate-skills',('"'+$run+'"'))
        }
        $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $arguments
        $process.PriorityClass='BelowNormal'
        Write-Output "Shield attack $($PSCmdlet.ParameterSetName) PID=$($process.Id) mode=$mode run=$run"
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while(-not $process.WaitForExit(30000)){
            if($timer.Elapsed.TotalMinutes -gt 25 -or (Test-Path -LiteralPath $log) -and
                (Select-String -LiteralPath $log -Pattern 'error CS\d+')){
                Stop-Process -Id $process.Id
                throw "Isolated shield attack process stopped after timeout or compilation failure: $run"
            }
            Write-Output "Shield attack remains active: $mode / $run"
        }
        $result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or -not $result.passed){throw "Shield attack failed: $($result.error) ($run)"}
        foreach($phase in @('preparation','lost','gained','result','return')){
            if(-not(Test-Path -LiteralPath (Join-Path $run "shield-attack-$phase.png"))){throw "Missing shield attack screenshot: $phase"}
        }
        if(@(Select-String -LiteralPath $log -SimpleMatch '[Shield attack flow] lost=1 gained=1 ').Count -ne 1){throw 'Requested shield attack flow did not complete.'}
        if(Select-String -LiteralPath $log -Pattern '^(?:[A-Za-z0-9_.]+Exception:|Assertion failed|Error:)|\[Skills acceptance\] failed'){
            throw "Shield attack has runtime or shutdown errors: $run"
        }
        Write-Output "Shield attack passed: $mode / $run"
    }
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($name in $previous.Keys){Set-Item ('Env:'+$name) $previous[$name]}
}

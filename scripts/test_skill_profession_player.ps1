param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(1,2,6,10,11,15)][int[]]$Professions=@(1,10,2,6,11,15)
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath=(Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')){throw 'Use one marked validation workspace.'}
$marker=Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw|ConvertFrom-Json
if($marker.format -ne 1 -or $marker.repository -ne $repository){throw 'Wrong validation workspace.'}
$executable=(Get-Item -LiteralPath $Player).FullName
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_SKILLS_PROFESSION_FLOW='1'
    foreach($profession in $Professions){
        $run=Join-Path $workspacePath ("profession-$profession-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_VALIDATION_DIR=$run
        $env:ZJCS_SKILLS_PROFESSION=[string]$profession
        $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile',('"'+(Join-Path $run 'player.log')+'"'),'--zjcs-validate-skills',('"'+$run+'"'))
        $process.PriorityClass='BelowNormal'
        while(-not $process.WaitForExit(30000)){Write-Output "Original profession skill flow remains active: $profession / $run"}
        $result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or -not $result.passed){throw "Profession Player failed: $($result.error) ($run)"}
        foreach($kind in @(100,101)){
            foreach($phase in @('filtered','all','rejected','installed')){
                if(-not(Test-Path -LiteralPath (Join-Path $run "profession-$kind-$phase.png"))){throw "Missing profession screenshot: $kind / $phase"}
            }
        }
        if(@(Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Skill profession flow]').Count -ne 2){throw 'Original pointer flow did not complete both item types.'}
        if(Select-String -LiteralPath (Join-Path $run 'player.log') -Pattern 'Exception:|NullReferenceException|AssertionException|Error:'){
            throw "Profession flow has runtime or shutdown errors: $run"
        }
        Write-Output "Original profession skill flow passed: $profession / $run"
    }
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($name in $previous.Keys){Set-Item ('Env:'+$name) $previous[$name]}
}

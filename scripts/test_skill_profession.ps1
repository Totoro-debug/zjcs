param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [switch]$Build,
    [switch]$FullBuild
)
$ErrorActionPreference='Stop'
if($FullBuild){$Build=$true}
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath=(Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
$projectPath=(Get-Item -LiteralPath $Project).FullName.TrimEnd('\','/')
if([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')){throw 'Use one marked validation workspace.'}
$marker=Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw|ConvertFrom-Json
if($marker.format -ne 1 -or $marker.repository -ne $repository){throw 'Wrong validation workspace.'}
if(-not $projectPath.StartsWith($workspacePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'The isolated Unity project must be inside the marked workspace.'
}
& python -B (Join-Path $PSScriptRoot 'prepare_skill_profession_reference.py') --project $projectPath
if($LASTEXITCODE -ne 0){throw 'Original profession sources differ.'}
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_VALIDATION_DIR=$workspacePath
    $method='SourceSkillProfessionReference.Validation.Run'
    if($Build){
        $method+='AndBuild'
        if(-not $FullBuild){$env:ZJCS_WINDOWS_SCRIPTS_ONLY='1'}
    }
    $log=Join-Path $workspacePath ('skill-profession-'+[guid]::NewGuid().ToString('N')+'.log')
    $config=Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw|ConvertFrom-Json
    $process=Start-UnityOnInternalDisplay -Editor $config.editorExecutable -ArgumentList @(
        '-batchmode','-projectPath',('"'+$projectPath+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+$log+'"'))
    $process.PriorityClass='BelowNormal'
    Write-Output "Skill profession Editor PID=$($process.Id) log=$log"
    while(-not $process.WaitForExit(30000)){Write-Output 'Skill profession validation remains active.'}
    if($process.ExitCode -ne 0){throw "Skill profession Editor exited $($process.ExitCode): $log"}
    if(-not(Select-String -LiteralPath $log -SimpleMatch '[Skill profession] Unchanged original methods passed ')){
        throw "Skill profession comparisons did not finish: $log"
    }
    if($Build -and -not(Select-String -LiteralPath $log -SimpleMatch '[Skills] Windows Player built:')){
        throw "Skill profession Windows build did not finish: $log"
    }
    Write-Output "Skill profession validation passed: $log"
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($name in $previous.Keys){Set-Item ('Env:'+$name) $previous[$name]}
}

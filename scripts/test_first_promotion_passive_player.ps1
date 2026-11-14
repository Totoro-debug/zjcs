param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [int[]]$SkillIds=@(10500,10501,10505,10507,10510,10514,10516,10520,10521,10522,10524,10535,10550,10554,10555,10559,10583,10588)
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
$rules=Get-Content (Join-Path $repository 'unity/Assets/Official/Combat/rules.json') -Raw|ConvertFrom-Json
$professions=@($rules.profession_base|Where-Object {$_.Profession -in @(1,2,6,10,11,15)}|Sort-Object Profession)
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_SKILLS_PASSIVE_FLOW='1'
    foreach($skill in $SkillIds){
        $owner=@($professions|Where-Object {$skill -in @($_.Skills)+@($_.FightUsedSkills)})
        $row=@($rules.skill|Where-Object ClassId -eq $skill)
        if($owner.Count -eq 0 -or $row.Count -ne 1 -or $row[0].PassivePropFactors.PSObject.Properties.Count -eq 0){throw "Secret is outside the original property scope: $skill"}
        $run=Join-Path $workspacePath ("passive-$skill-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_VALIDATION_DIR=$run
        $env:ZJCS_SKILLS_PASSIVE_ITEM=[string]$skill
        $env:ZJCS_SKILLS_PASSIVE_PROFESSION=[string]$owner[0].Profession
        $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile',('"'+(Join-Path $run 'player.log')+'"'),'--zjcs-validate-skills',('"'+$run+'"'))
        $process.PriorityClass='BelowNormal'
        while(-not $process.WaitForExit(30000)){Write-Output "Original passive property flow remains active: $skill / $run"}
        $result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or -not $result.passed){throw "Passive property Player failed: $($result.error) ($run)"}
        foreach($phase in @('detail','equipped','cultivated','reloaded')){
            if(-not(Test-Path -LiteralPath (Join-Path $run "passive-$phase.png"))){throw "Missing original passive screenshot: $phase"}
        }
        foreach($name in @('passive-save.json','passive-properties-level-1.json','passive-properties-level-2.json')){
            if(-not(Test-Path -LiteralPath (Join-Path $run $name))){throw "Missing passive state evidence: $name"}
        }
        if(Select-String -LiteralPath (Join-Path $run 'player.log') -Pattern 'Exception:|NullReferenceException|AssertionException|Error:'){
            throw "Original passive flow has runtime or shutdown errors: $run"
        }
        Write-Output "Original secret property flow passed: $skill / $run"
    }
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($name in $previous.Keys){Set-Item ('Env:'+$name) $previous[$name]}
}

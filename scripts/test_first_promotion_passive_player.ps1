param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [int[]]$SkillIds=@(10500,10501,10502,10505,10507,10509,10510,10513,10514,10516,10520,10521,10522,10524,10535,10537,10539,10540,10541,10544,10546,10550,10554,10555,10556,10559,10561,10563,10569,10571,10576,10578,10583,10588,10602),
    [ValidateSet(0,1,2,3,4,6,7,8,10,11,12,13,15,16,17)][int]$Profession=0,
    [switch]$AcquireFromPieces
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
    $mainProject=[IO.Path]::GetFullPath((Join-Path $repository 'unity'))
    if(-not $projectPath.StartsWith($workspacePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and
        -not $projectPath.Equals($mainProject,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Use the repository Unity project or an isolated project inside the marked workspace.'
    }
    $normalizedProject=$projectPath.Replace('\','/')
    $projectUsers=@(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($normalizedProject)
    })
    if($projectUsers.Count){throw 'The selected passive validation project is already in use.'}
    $config=Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw|ConvertFrom-Json
    $executable=$config.editorExecutable
}else{$executable=(Get-Item -LiteralPath $Player).FullName}
$rules=Get-Content (Join-Path $repository 'unity/Assets/Official/Combat/rules.json') -Raw|ConvertFrom-Json
$professions=@($rules.profession_base|Where-Object {$_.Profession -in @(1,2,3,4,6,7,8,10,11,12,13,15,16,17)}|Sort-Object Profession)
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_SKILLS_PASSIVE_FLOW='1'
    if($AcquireFromPieces){$env:ZJCS_SKILLS_PASSIVE_PIECES='1'}
    foreach($skill in $SkillIds){
        $owner=@($professions|Where-Object {$skill -in @($_.Skills)+@($_.FightUsedSkills)})
        $row=@($rules.skill|Where-Object ClassId -eq $skill)
        if($owner.Count -eq 0 -or $row.Count -ne 1 -or ($row[0].PassivePropFactors.PSObject.Properties.Count -eq 0 -and @($row[0].PassiveStatusIdList | Where-Object {$_ -gt 0}).Count -eq 0)){throw "Secret has no original property or passive status: $skill"}
        $selectedProfession=[int]$owner[0].Profession
        if($Profession -gt 0){
            $candidate=$Profession
            $available=$false
            while($candidate -gt 0){
                $professionRow=@($rules.profession_base|Where-Object Profession -eq $candidate)
                if($professionRow.Count -ne 1){throw "Missing original profession row: $candidate"}
                if($skill -in @($professionRow[0].Skills)+@($professionRow[0].FightUsedSkills)){$available=$true; break}
                $candidate=[int]$professionRow[0].PrePro
            }
            if(-not $available){throw "Secret $skill is unavailable for original profession $Profession"}
            $selectedProfession=$Profession
        }
        $prefix=if($AcquireFromPieces){'passive-pieces'}else{'passive'}
        $run=Join-Path $workspacePath ("$prefix-$skill-$($PSCmdlet.ParameterSetName)-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_VALIDATION_DIR=$run
        $env:ZJCS_SKILLS_PASSIVE_ITEM=[string]$skill
        $env:ZJCS_SKILLS_PASSIVE_PROFESSION=[string]$selectedProfession
        if($PSCmdlet.ParameterSetName -eq 'Play'){
            $env:ZJCS_SKILLS_AUTORUN='1'
            $env:ZJCS_SKILLS_HOME_ONLY='1'
            $arguments=@('-muteAudio','-projectPath',('"'+$projectPath+'"'),'-logFile',('"'+(Join-Path $run 'player.log')+'"'))
        }else{
            $arguments=@('-muteAudio','-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
                '-logFile',('"'+(Join-Path $run 'player.log')+'"'),'--zjcs-validate-skills',('"'+$run+'"'))
        }
        $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $arguments
        $process.PriorityClass='BelowNormal'
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while(-not $process.WaitForExit(30000)){
            if($timer.Elapsed.TotalMinutes -gt 25 -or ((Test-Path -LiteralPath (Join-Path $run 'player.log')) -and
                (Select-String -LiteralPath (Join-Path $run 'player.log') -Pattern 'error CS\d+'))){
                Stop-Process -Id $process.Id
                throw "Passive flow stopped after timeout or compile failure: $run"
            }
            Write-Output "Original passive property flow remains active: $skill / $run"
        }
        $process.Refresh()
        @{ProcessId=$process.Id;ExitCode=$process.ExitCode;ElapsedSeconds=$timer.Elapsed.TotalSeconds}|
            ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'process-exit.json')
        $result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or -not $result.passed){throw "Passive property Player failed: $($result.error) ($run)"}
        foreach($phase in @('detail','equipped','cultivated','reloaded')){
            if(-not(Test-Path -LiteralPath (Join-Path $run "passive-$phase.png"))){throw "Missing original passive screenshot: $phase"}
        }
        if($AcquireFromPieces){
            foreach($phase in @('list','insufficient','detail','confirm','merged','material','rank-detail','ranked')){
                if(-not(Test-Path -LiteralPath (Join-Path $run "passive-piece-$phase.png"))){throw "Missing native fragment screenshot: $phase"}
            }
            foreach($marker in @(
                '[Passive piece flow] Original active/passive list, insufficient progress and native secret acquisition passed.',
                '[Passive piece flow] Native passive material icon, fragment detail and original rank transaction passed.')){
                if(-not(Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $marker)){throw "Missing native fragment assertion: $marker"}
            }
        }
        foreach($name in @('passive-save.json','passive-properties-level-1.json','passive-properties-level-2.json')){
            if(-not(Test-Path -LiteralPath (Join-Path $run $name))){throw "Missing passive state evidence: $name"}
        }
        $errors=@(Select-String -LiteralPath (Join-Path $run 'player.log') -Pattern 'Exception:|NullReferenceException|AssertionException|Error:')
        if($PSCmdlet.ParameterSetName -eq 'Play'){
            $errors=@($errors | Where-Object {$_.Line -notmatch '^\[Licensing::(?:Module|Client)\] Error:|^EditorUpdateCheck: Failed '})
        }
        if($errors.Count -gt 0){
            throw "Original passive flow has runtime or shutdown errors: $run"
        }
        Write-Output "Original secret property flow passed: $skill / $run"
    }
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($name in $previous.Keys){Set-Item ('Env:'+$name) $previous[$name]}
}

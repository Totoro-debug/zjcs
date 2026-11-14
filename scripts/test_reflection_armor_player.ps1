param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet('up','down','left','right')][string[]]$Directions=@('up','down','left','right'),
    [ValidateSet('partial','full')][string[]]$Modes=@('partial','full')
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable=(Get-Item -LiteralPath $Player).FullName
$workspacePath=(Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use a session created by New-ZjcsValidationWorkspace.'
}
$marker=Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw|ConvertFrom-Json
if($marker.format -ne 1 -or $marker.repository -ne $repository){throw 'Wrong validation workspace repository.'}
python -B (Join-Path $repository 'scripts/prepare_battle_reflection_armor_sources.py') --verify
if($LASTEXITCODE -ne 0){throw 'Original reflection source identities differ.'}
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
$process=$null
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_SKILLS_VALIDATE_SUMMON_ONLY='1'
    $env:ZJCS_GRAPH_CAPABILITY_ACCEPTANCE='11480'
    $env:ZJCS_BATTLE_INPUT_AUTOMATION='1'
    $env:ZJCS_FIRST_PROMOTION_SKILL='10008'
    $env:ZJCS_REFLECTION_ARMOR='1'
    foreach($mode in $Modes){foreach($direction in $Directions){
        $run=Join-Path $workspacePath ("reflect-10567-$mode-$direction-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_VALIDATION_DIR=$run
        $env:ZJCS_DIRECTIONAL_SKILL_FACING=$direction
        $env:ZJCS_REFLECTION_ARMOR_FULL_SHIELD=$(if($mode -eq 'full'){'1'}else{'0'})
        $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile',('"'+(Join-Path $run 'player.log')+'"'),'--zjcs-validate-skills',('"'+$run+'"'))
        $process.PriorityClass='BelowNormal'
        $deadline=[DateTime]::UtcNow.AddMinutes(30)
        while(-not $process.WaitForExit(30000)){
            if([DateTime]::UtcNow -gt $deadline){throw "Reflection Player timed out: $run"}
            Write-Output "Running reflection $mode $direction : $run"
        }
        $process.WaitForExit()
        $result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or -not $result.passed){throw "Reflection Player failed: $($result.error) ($run)"}
        foreach($phase in 1..4){
            if(-not(Test-Path -LiteralPath (Join-Path $run "reflection-armor-phase-$phase.png"))){
                throw "Original reflection phase $phase is missing: $run"
            }
        }
        $saved=Get-Content -LiteralPath (Join-Path $run 'reflection-armor-checkpoint.json') -Raw|ConvertFrom-Json
        $cast=@($saved.checkpoint.Receipts[-1].Event.SkillCasts|Where-Object SkillEntityId -eq 11480)
        $expectedFacing=@{up=2;down=0;left=1;right=3}[$direction]
        if($cast.Count -ne 1 -or $cast[0].Presentation.FacingIndex -ne $expectedFacing){
            throw "Original reflection facing differs from the requested body arrangement: $run"
        }
        foreach($message in @('[Reflection armor] Actual shield absorption, 11480/11481/11482, cultivation and checkpoint passed.',
            '[Reflection armor] Original reaction entity, particles, sound and four frames passed.')){
            if(-not(Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)){
                throw "Reflection native evidence is missing: $message ($run)"
            }
        }
        Write-Output "Original reflection armor Player passed: 10567 / $mode / $direction"
    }}
}
finally {
    if($process -and -not $process.HasExited){$process.Kill();$process.WaitForExit()}
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}
}

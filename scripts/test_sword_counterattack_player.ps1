param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet('up','down','left','right')][string[]]$Directions=@('up','down','left','right'),
    [ValidateSet('normal','shield')][string[]]$Modes=@('normal','shield')
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
$proof=Get-Content (Join-Path $repository 'assets/reference/battle-rules/sword-counterattack-source-manifest.json') -Raw|ConvertFrom-Json
foreach($entry in @($proof.native_binding.shared_resource_proof)+@($proof.native_binding.inputs)){
    if((Get-FileHash -LiteralPath (Join-Path $repository $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256){
        throw "Original counterattack native input identity changed: $($entry.path)"
    }
}
$previous=@{}
Get-ChildItem Env:ZJCS_*|ForEach-Object {$previous[$_.Name]=$_.Value}
try {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    $env:ZJCS_REPOSITORY_ROOT=$repository
    $env:ZJCS_SKILLS_VALIDATE_SUMMON_ONLY='1'
    $env:ZJCS_GRAPH_CAPABILITY_ACCEPTANCE='10600'
    $env:ZJCS_BATTLE_INPUT_AUTOMATION='1'
    # Select only the original warrior identity; the fixture equips enemy 14678
    # and secret 10537. The player has no ordinary slot before the counterattack.
    $env:ZJCS_FIRST_PROMOTION_SKILL='10008'
    $env:ZJCS_SWORD_COUNTERATTACK='1'
    foreach($mode in $Modes){foreach($direction in $Directions){
        $run=Join-Path $workspacePath ("counter-10537-$mode-$direction-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_VALIDATION_DIR=$run
        $env:ZJCS_DIRECTIONAL_SKILL_FACING=$direction
        $env:ZJCS_SWORD_COUNTERATTACK_SHIELD=$(if($mode -eq 'shield'){'1'}else{'0'})
        $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile',('"'+(Join-Path $run 'player.log')+'"'),'--zjcs-validate-skills',('"'+$run+'"'))
        $process.PriorityClass='BelowNormal'
        while(-not $process.WaitForExit(30000)){Write-Output "Running counterattack $mode $direction : $run"}
        $result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or -not $result.passed){throw "Counterattack Player failed: $($result.error) ($run)"}
        foreach($phase in 1..4){
            if(-not(Test-Path -LiteralPath (Join-Path $run "sword-counterattack-phase-$phase.png"))){
                throw "Original counterattack phase $phase is missing: $run"
            }
        }
        if(-not(Test-Path -LiteralPath (Join-Path $run 'sword-counterattack-checkpoint.json'))){
            throw "Counterattack checkpoint is missing: $run"
        }
        $saved=Get-Content -LiteralPath (Join-Path $run 'sword-counterattack-checkpoint.json') -Raw|ConvertFrom-Json
        $cast=@($saved.checkpoint.Receipts[-1].Event.SkillCasts|Where-Object SkillEntityId -eq 11210)
        $expectedFacing=@{up=2;down=0;left=1;right=3}[$direction]
        if($cast.Count -ne 1 -or $cast[0].Presentation.FacingIndex -ne $expectedFacing){
            throw "Original counterattack facing differs from the requested body arrangement: $run"
        }
        foreach($message in @('[Sword counterattack] Original enemy hit, 11210 reaction, cultivation and checkpoint passed;',
            '[Sword counterattack sound] Original Skill_10537_1 is playing in the native engine.',
            '[Sword counterattack] Original 11210 native view, body action and sound completion passed.')){
            if(-not(Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)){
                throw "Counterattack native evidence is missing: $message ($run)"
            }
        }
        Write-Output "Original sword counterattack Player passed: 10537 / $mode / $direction"
    }}
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}
}

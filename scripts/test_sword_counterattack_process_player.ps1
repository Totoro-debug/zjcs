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
foreach($entry in @($proof.native_binding.shared_resource_proof)+@($proof.native_binding.inputs)+@($proof.native_binding.process_resource_proof)){
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
    $env:ZJCS_FIRST_PROMOTION_SKILL='10008'
    $env:ZJCS_SWORD_COUNTERATTACK='1'
    foreach($mode in $Modes){foreach($direction in $Directions){
        $run=Join-Path $workspacePath ("process-10537-$mode-$direction-"+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run|Out-Null
        $env:ZJCS_DIRECTIONAL_SKILL_FACING=$direction
        $env:ZJCS_SWORD_COUNTERATTACK_SHIELD=$(if($mode -eq 'shield'){'1'}else{'0'})
        $processIds=@()
        foreach($phase in @('prepare','resume')){
            $path=Join-Path $run $phase
            New-Item -ItemType Directory -Path $path|Out-Null
            $env:ZJCS_VALIDATION_DIR=$path
            $env:ZJCS_SWORD_PROCESS_PHASE=$phase
            $env:ZJCS_SWORD_PROCESS_HANDOFF=Join-Path $run 'prepare/process-handoff.json'
            $process=Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
                '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
                '-logFile',('"'+(Join-Path $path 'player.log')+'"'),'--zjcs-validate-skills',('"'+$path+'"'))
            $process.PriorityClass='BelowNormal';$processIds+=$process.Id
            while(-not $process.WaitForExit(30000)){Write-Output "Sword process $mode $direction $phase : $path"}
            $name=$(if($phase -eq 'prepare'){'process-prepared.json'}else{'result.json'})
            if($process.ExitCode -ne 0 -or -not(Test-Path (Join-Path $path $name))){throw "Sword process failed: $phase ($path)"}
            if(-not(Get-Content (Join-Path $path $name) -Raw|ConvertFrom-Json).passed){throw "Sword process assertions failed: $phase ($path)"}
            if(Select-String -LiteralPath (Join-Path $path 'player.log') -Pattern 'Exception:|error CS|^GameObjects can not be made active when they are being destroyed\.'){
                throw "Sword process runtime or shutdown failure: $phase ($path)"
            }
        }
        $handoff=Get-Content (Join-Path $run 'prepare/process-handoff.json') -Raw|ConvertFrom-Json
        if($processIds[0] -eq $processIds[1] -or $handoff.ProcessId -ne $processIds[0] -or $handoff.Shield -ne ($mode -eq 'shield')){
            throw "Sword handoff did not cross two native processes: $run"
        }
        $before=Get-FileHash (Join-Path $run 'prepare/process-checkpoint-before.json') -Algorithm SHA256
        $after=Get-FileHash (Join-Path $run 'resume/process-checkpoint-after.json') -Algorithm SHA256
        if($before.Hash -cne $after.Hash){throw "Sword resume changed its complete persisted checkpoint: $run"}
        foreach($name in @('sword-process-after-resume','graph-capability-11210','sword-counterattack-phase-1',
            'sword-counterattack-phase-2','sword-counterattack-phase-3','sword-counterattack-phase-4','process-result','process-return')){
            if(-not(Test-Path (Join-Path $run "resume/$name.png"))){throw "Sword resumed native frame is missing: $name ($run)"}
        }
        $saved=Get-Content (Join-Path $run 'resume/sword-counterattack-checkpoint.json') -Raw|ConvertFrom-Json
        $casts=@($saved.checkpoint.Receipts.Event.SkillCasts|Where-Object SkillEntityId -eq 11210)
        $expectedFacing=@{up=2;down=0;left=1;right=3}[$direction]
        if($casts.Count -ne 1 -or $casts[0].Presentation.FacingIndex -ne $expectedFacing){throw "Sword resumed counterattack repeated or changed direction: $run"}
        foreach($message in @('[Sword process] Different process restored the exact first-hit checkpoint,',
            '[Sword counterattack] Original enemy hit, 11210 reaction, cultivation and checkpoint passed;',
            '[Sword counterattack sound] Original Skill_10537_1 is playing in the native engine.',
            '[Sword counterattack] Original 11210 native view, body action and sound completion passed.')){
            if(-not(Select-String -LiteralPath (Join-Path $run 'resume/player.log') -SimpleMatch -Pattern $message)){throw "Sword resume evidence missing: $message ($run)"}
        }
        @{passed=$true;processIds=$processIds;checkpointSha256=$before.Hash}|ConvertTo-Json|
            Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
        Write-Output "Original sword counterattack process exit/resume passed: $mode / $direction ($run)"
    }}
}
finally {
    Get-ChildItem Env:ZJCS_*|ForEach-Object {Remove-Item ('Env:'+$_.Name)}
    foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}
}

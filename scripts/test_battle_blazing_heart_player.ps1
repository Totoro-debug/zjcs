param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$Profile,
    [ValidateSet(10520,10521,10522,10550,10555,10583,10559,10554,10578,10524,10535,10505,10510,10501,10500,10507,10509,10516,10540,10541,10563,10587)][int]$SecretId = 10520
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
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation workspace.' }
$profilePath = (Get-Item -LiteralPath $Profile).FullName
if (-not $profilePath.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The acquired profile must be inside the isolated validation workspace.'
}
if ($PSCmdlet.ParameterSetName -eq 'Play') {
    $projectPath = (Get-Item -LiteralPath $Project).FullName
    if (-not $projectPath.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The isolated Unity project must be inside the marked workspace.'
    }
    $config = Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json
    $executable = $config.editorExecutable
} else {
    $executable = (Get-Item -LiteralPath $Player).FullName
}
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
$evidence = @{}
try {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $env:ZJCS_SKILLS_VALIDATE_SUMMON_ONLY = '1'
    $env:ZJCS_ACTIVE_LOADOUT_ACCEPTANCE = '1'
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    $env:ZJCS_BLAZING_HEART_FLOW = '1'
    $env:ZJCS_BLAZING_HEART_PROFILE = $profilePath
    $env:ZJCS_AFFINITY_SECRET_ID = [string]$SecretId
    if ($PSCmdlet.ParameterSetName -eq 'Play') {
        $env:ZJCS_SKILLS_AUTORUN = '1'
        $env:ZJCS_SKILLS_HOME_ONLY = '1'
        $env:ZJCS_SKILLS_PASSIVE_FLOW = '1'
    }
    foreach ($equipped in @($false, $true)) {
        $run = Join-Path $workspacePath ('blazing-heart-battle-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run | Out-Null
        $env:ZJCS_VALIDATION_DIR = $run
        $env:ZJCS_BLAZING_HEART_EQUIPPED = if ($equipped) { '1' } else { '0' }
        if ($PSCmdlet.ParameterSetName -eq 'Play') {
            $arguments = @('-muteAudio','-projectPath',('"' + $projectPath + '"'),
                '-logFile',('"' + (Join-Path $run 'player.log') + '"'))
        } else {
            $arguments = @('-muteAudio','-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
                '-logFile',('"' + (Join-Path $run 'player.log') + '"'),'--zjcs-validate-skills',('"' + $run + '"'))
        }
        $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $arguments
        $process.PriorityClass = 'BelowNormal'
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $limitMinutes = if ($PSCmdlet.ParameterSetName -eq 'Play') { 60 } else { 15 }
        while (-not $process.WaitForExit(30000)) {
            if ($timer.Elapsed.TotalMinutes -gt $limitMinutes) {
                Stop-Process -Id $process.Id
                throw "Blazing heart Player validation timed out: $run"
            }
            Write-Output "Blazing heart battle remains active: equipped=$equipped / $run"
        }
        $process.Refresh()
        $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Blazing heart Player failed: $($result.error) ($run)" }
        foreach ($phase in @('preparation','battle','result','return')) {
            if (-not (Test-Path -LiteralPath (Join-Path $run "blazing-heart-$phase.png"))) { throw "Missing battle screenshot: $phase" }
        }
        $errors = @(Select-String -LiteralPath (Join-Path $run 'player.log') -Pattern 'Exception:|NullReferenceException|AssertionException|Error:')
        if ($PSCmdlet.ParameterSetName -eq 'Play') {
            $start = @(Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Blazing heart battle] validation started')
            if ($start.Count -ne 1 -or @(Select-String -LiteralPath (Join-Path $run 'player.log') -Pattern 'error CS\d+').Count -gt 0) {
                throw "The Editor did not compile and start the battle diagnostic: $run"
            }
            # The Editor may report unresolved dependencies while inspecting disabled
            # official APK plugins during import. Runtime errors remain failures.
            $errors = @($errors | Where-Object { $_.LineNumber -ge $start[0].LineNumber -and
                $_.Line -notmatch '^\[Licensing::(?:Module|Client)\] Error:|^EditorUpdateCheck: Failed ' })
        }
        if ($errors.Count -gt 0) {
            throw "The actual battle produced a runtime or shutdown error: $run"
        }
        if (-not $equipped) {
            $wishIds=if($SecretId -eq 10587){@(10587)}else{@(10520,10521,10522,10550,10555,10583,10559,10554,10578,10513,10561,10502,10556,10569,10576,10602,10509,10516,10540,10541,10563,10500,10501,10505,10507,10510,10524,10535,10537,10539,10544,10546,10571)}
            foreach ($skill in $wishIds) {
                if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch "[Passive affinity wish] item=$skill granted")) {
                    throw "Original skill wish acquisition did not complete for $skill : $run"
                }
            }
        }
        $evidence[[string]$equipped] = Get-Content -LiteralPath (Join-Path $run 'blazing-heart-battle.json') -Raw | ConvertFrom-Json
        Write-Output "Blazing heart actual battle passed: equipped=$equipped / $run"
    }
    if ($evidence['True'].affinity -le $evidence['False'].affinity -or
        ($SecretId -notin @(10550,10505,10510,10583,10501,10559,10554,10500,10507,10587) -and $evidence['True'].damage -le $evidence['False'].damage) -or
        ($SecretId -in @(10554,10500,10507,10587) -and $evidence['True'].secondary -le $evidence['False'].secondary) -or
        ($SecretId -eq 10559 -and ($evidence['True'].effectRate -le $evidence['False'].effectRate -or
            $evidence['True'].damage -ne $evidence['False'].damage)) -or
        ($SecretId -in @(10583,10501,10587) -and $evidence['True'].damage -ge $evidence['False'].damage) -or
        ($SecretId -in @(10550,10505,10510) -and $evidence['True'].damage -ne $evidence['False'].damage) -or
        ($SecretId -eq 10510 -and ($evidence['True'].firstActor -ne 1 -or $evidence['False'].firstActor -le 1))) {
        throw 'Equipping the acquired secret produced unexpected battle properties or damage.'
    }
    if ($SecretId -in @(10554,10500)) { Write-Output "Actual critical value: $($evidence['False'].secondary) -> $($evidence['True'].secondary)." }
    if ($SecretId -eq 10507) { Write-Output "Actual block value: $($evidence['False'].secondary) -> $($evidence['True'].secondary)." }
    if ($SecretId -eq 10559) { Write-Output "Actual effect rate: $($evidence['False'].effectRate) -> $($evidence['True'].effectRate)." }
    Write-Output "Actual property for $SecretId : $($evidence['False'].affinity) -> $($evidence['True'].affinity)."
    Write-Output "Actual skill damage for $SecretId : $($evidence['False'].damage) -> $($evidence['True'].damage)."
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($name in $previous.Keys) { Set-Item ('Env:' + $name) $previous[$name] }
}

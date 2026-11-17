param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10016,10026,10053)][int]$SkillId = 10026
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
$run = Join-Path $workspacePath ('aa-' + $SkillId + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12))
New-Item -ItemType Directory -Path $run | Out-Null
$log = Join-Path $run 'runtime.log'
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
try {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    $env:ZJCS_REPOSITORY_ROOT = $repository
    $env:ZJCS_VALIDATION_DIR = $run
    $env:ZJCS_SKILLS_ACQUIRED_ACTIVE = [string]$SkillId
    if ($PSCmdlet.ParameterSetName -eq 'Play') {
        $env:ZJCS_SKILLS_AUTORUN = '1'
        $env:ZJCS_SKILLS_HOME_ONLY = '1'
        $arguments = @('-muteAudio','-job-worker-count','2','-gc-helper-count','2',
            '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'))
    } else {
        $arguments = @('-muteAudio','-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile', ('"' + $log + '"'), '--zjcs-validate-skill-damage', ('"' + $run + '"'))
    }
    $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList $arguments
    $process.PriorityClass = 'BelowNormal'
    Write-Output "Acquired active skill-page flow PID=$($process.Id): $run"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(30000)) {
        if ($timer.Elapsed.TotalMinutes -gt 25 -or ((Test-Path -LiteralPath $log) -and
            (Select-String -LiteralPath $log -Pattern 'error CS\d+'))) {
            Stop-Process -Id $process.Id
            throw "Skill-page flow stopped after timeout or compile failure: $run"
        }
        Write-Output "Acquired active skill-page flow remains active: $run"
    }
    $process.Refresh()
    @{ ProcessId=$process.Id; ExitCode=$process.ExitCode; ElapsedSeconds=$timer.Elapsed.TotalSeconds } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'process-exit.json')
    if (-not (Test-Path -LiteralPath (Join-Path $run 'result.json'))) {
        throw "Skill-page flow produced no success result; exit=$($process.ExitCode), log=$log"
    }
    $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Skill-page flow failed: $($result.error) ($run)" }
    $identity = Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/Game/Runtime/Offline/SkillAssets/OfficialSkillUiData.Identity.cs') -Raw
    if ($identity -notmatch 'LayoutSha256 = "([0-9a-f]{64})"') { throw 'The compiled skill UI identity is absent.' }
    $expectedUi = $Matches[1]
    if (-not (Select-String -LiteralPath $log -Pattern ('\[Skill UI data\] Verified (embedded TextAsset|native Windows pack): ' + $expectedUi))) {
        throw "The runtime skill UI did not verify its current payload identity: $run"
    }
    $combatRules = Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/Official/Combat/rules.json') -Raw | ConvertFrom-Json
    $skillRules = Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/Official/Skills/skill-runtime-rules.json') -Raw | ConvertFrom-Json
    $skillItems = @($combatRules.item | Where-Object ClassId -eq $SkillId)
    if ($skillItems.Count -ne 1) { throw "The original skill item is not unique: $SkillId" }
    $rankRows = @($skillRules.RankDisplays | Where-Object Quality -eq $skillItems[0].Quality)
    if ($rankRows.Count -eq 0) { throw "The original skill quality has no rank display: $SkillId" }
    $cultivatedRank = ($rankRows | Measure-Object Rank -Minimum).Minimum + 1
    foreach ($text in @(
        "[Acquired active UI] $SkillId fragment acquisition, original slot input, level/rank cultivation and disk reload passed.",
        "[Acquired active UI] $SkillId saved rank=$cultivatedRank level=2, original damage/result/return and unchanged save passed.")) {
        if (-not (Select-String -LiteralPath $log -SimpleMatch $text)) { throw "Missing skill-page assertion: $text" }
    }
    foreach ($phase in @('character','pieces','merge-confirm','merge-skill-detail','merge-piece-detail','acquired-detail','equipped','level-up','rank-up','reloaded',
        'settings','preparation','battle','result','return')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run "acquired-active-ui-$phase.png"))) {
            throw "Missing skill-page runtime frame: $phase ($run)"
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $run 'acquired-active-ui-checkpoint.json'))) {
        throw "The completed skill-page battle checkpoint is absent: $run"
    }
    $receiptVerifier = if ($SkillId -eq 10016) { 'verify_lightning_dash_receipt.py' } elseif ($SkillId -eq 10053) { 'verify_sunset_sword_receipt.py' } else { 'verify_fire_slash_receipt.py' }
    & python -B (Join-Path $PSScriptRoot $receiptVerifier) (Join-Path $run 'acquired-active-ui-checkpoint.json')
    if ($LASTEXITCODE -ne 0) { throw "The acquired skill's original battle receipts failed: $run" }
    Write-Output "Acquired active skill-page flow passed: $run"
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($name in $previous.Keys) { Set-Item ('Env:' + $name) $previous[$name] }
}

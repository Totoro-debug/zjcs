param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10000,10008,10009,10010,10011,10012,10013,10014,10015,10016,10017,10018,10024,10026,10028,10032,10035,10039,10043,10044,10046,10048,10049,10051,10053,10066)][int]$SkillId = 10026
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
    $resultSeenAt = $null
    while (-not $process.WaitForExit(5000)) {
        if ($null -eq $resultSeenAt -and (Test-Path -LiteralPath (Join-Path $run 'result.json'))) {
            $resultSeenAt = $timer.Elapsed.TotalSeconds
        }
        if ($timer.Elapsed.TotalMinutes -gt 25 -or ($null -ne $resultSeenAt -and
            $timer.Elapsed.TotalSeconds - $resultSeenAt -gt 120) -or ((Test-Path -LiteralPath $log) -and
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
    if ($SkillId -in @(10000,10010,10017,10028,10035,10044,10046,10048,10049,10066) -and -not (Select-String -LiteralPath $log -SimpleMatch "[Passive affinity wish] item=$SkillId granted")) {
        throw "The original knight wish pool did not grant skill $SkillId : $run"
    }
    # PowerShell's JSON conversion of the complete official combat tables is
    # disproportionately expensive; read only the selected item and ranks.
    $rankQuery = "import json,sys; c=json.load(open(sys.argv[1],encoding='utf-8')); s=json.load(open(sys.argv[2],encoding='utf-8')); rows=[x for x in c['item'] if x['ClassId']==int(sys.argv[3])]; assert len(rows)==1; print(min(x['Rank'] for x in s['RankDisplays'] if x['Quality']==rows[0]['Quality'])+1)"
    $cultivatedRank = & python -B -c $rankQuery (Join-Path $repository 'unity/Assets/Official/Combat/rules.json') (Join-Path $repository 'unity/Assets/Official/Skills/skill-runtime-rules.json') $SkillId
    if ($LASTEXITCODE -ne 0 -or $cultivatedRank -notmatch '^\d+$') { throw "The original skill rank could not be resolved: $SkillId" }
    $kind = if ($SkillId -in @(10024,10032,10046,10051,10066)) { 'status' } else { 'damage' }
    foreach ($text in @(
        "[Acquired active UI] $SkillId fragment acquisition, original slot input, level/rank cultivation and disk reload passed.",
        "[Acquired active UI] $SkillId saved rank=$cultivatedRank level=2, original $kind/result/return and unchanged save passed.")) {
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
    if ($SkillId -in @(10032,10039)) {
        # The Player asserts the official shield or physical strike for each equipped cast.
        Write-Output "Acquired active skill-page flow passed: $run"
        return
    }
    $receiptVerifier = if ($SkillId -eq 10000) { 'verify_water_bullet_receipt.py' } elseif ($SkillId -eq 10008) { 'verify_warrior_slash_receipt.py' } elseif ($SkillId -eq 10009) { 'verify_heavy_strike_receipt.py' } elseif ($SkillId -eq 10010) { 'verify_stun_strike_receipt.py' } elseif ($SkillId -eq 10011) { 'verify_lion_combo_receipt.py' } elseif ($SkillId -eq 10012) { 'verify_wind_blade_receipt.py' } elseif ($SkillId -eq 10013) { 'verify_four_way_slash_receipt.py' } elseif ($SkillId -eq 10014) { 'verify_whirlwind_slash_receipt.py' } elseif ($SkillId -eq 10015) { 'verify_jump_slash_receipt.py' } elseif ($SkillId -eq 10016) { 'verify_lightning_dash_receipt.py' } elseif ($SkillId -eq 10017) { 'verify_water_thread_receipt.py' } elseif ($SkillId -eq 10018) { 'verify_pull_skill_receipt.py' } elseif ($SkillId -eq 10024) { 'verify_arc_shield_receipt.py' } elseif ($SkillId -eq 10028) { 'verify_fireball_receipt.py' } elseif ($SkillId -eq 10035) { 'verify_ice_cone_receipt.py' } elseif ($SkillId -eq 10043) { 'verify_falling_wind_receipt.py' } elseif ($SkillId -eq 10044) { 'verify_frost_flower_receipt.py' } elseif ($SkillId -eq 10046) { 'verify_guard_shield_receipt.py' } elseif ($SkillId -eq 10048) { 'verify_bouncing_shield_receipt.py' } elseif ($SkillId -eq 10049) { 'verify_challenge_receipt.py' } elseif ($SkillId -eq 10051) { 'verify_battle_spirit_receipt.py' } elseif ($SkillId -eq 10053) { 'verify_sunset_sword_receipt.py' } elseif ($SkillId -eq 10066) { 'verify_encouragement_receipt.py' } else { 'verify_fire_slash_receipt.py' }
    $receiptArgs = @((Join-Path $run 'acquired-active-ui-checkpoint.json'))
    if ($SkillId -eq 10017) { $receiptArgs += '--require-cold' }
    & python -B (Join-Path $PSScriptRoot $receiptVerifier) @receiptArgs
    if ($LASTEXITCODE -ne 0) { throw "The acquired skill's original battle receipts failed: $run" }
    Write-Output "Acquired active skill-page flow passed: $run"
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($name in $previous.Keys) { Set-Item ('Env:' + $name) $previous[$name] }
}

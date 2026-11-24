param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10000,10002,10008,10009,10010,10011,10012,10013,10014,10015,10016,10017,10018,10024,10026,10028,10032,10035,10039,10043,10044,10046,10047,10048,10049,10051,10053,10056,10059,10066,10068,10073,10077,10079,10082,10083,10084,10085,10088,10089,10091,10092,10093,10096,10099,10102,10103,10104)][int]$SkillId = 10026,
    [Parameter(ParameterSetName='Player')]
    [ValidateSet(10000,10002,10008,10009,10010,10011,10012,10013,10014,10015,10016,10017,10018,10024,10026,10028,10032,10035,10039,10043,10044,10046,10047,10048,10049,10051,10053,10056,10059,10066,10068,10073,10077,10079,10082,10083,10084,10085,10088,10089,10091,10092,10093,10096,10099,10102,10103,10104)][int[]]$AdditionalSkillIds = @(),
    [string]$PassiveSave,
    [ValidateSet(0,39521)][int]$SkillSkinId = 0
)
function Assert-AcquiredPassiveFlowEvidence([string]$Run, [int]$PassiveId) {
    $result = Get-Content -LiteralPath (Join-Path $Run 'result.json') -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw 'The cultivated passive combat flow did not pass.' }
    $runtimeLog = Join-Path $Run 'runtime.log'
    if ($PassiveId -eq 10593) {
        foreach ($file in @('companion-cultivated-save.json','companion-saint-guard-checkpoint.json',
            'companion-saint-guard-shield.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'The actual allied defence shield evidence is incomplete.' }
        }
        $source = Get-Content -LiteralPath (Join-Path $Run 'companion-cultivated-save.json') -Raw | ConvertFrom-Json
        $receipt = Get-Content -LiteralPath (Join-Path $Run 'companion-saint-guard-shield.json') -Raw | ConvertFrom-Json
        $message = '[Acquired passive combat] 10593 cultivated allied defence shield, player source and restore replay passed.'
        if ($source.Player.Skills.Plans[$source.Player.Skills.SelectedPlan].PassiveSkills[0] -ne 10593 -or
            $receipt.ally -le 1 -or $receipt.actual -ne $receipt.expected -or $receipt.actual -le $receipt.basis -or
            -not $receipt.restored -or -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $message)) {
            throw 'The cultivated allied defence shield did not preserve its value, source or restore receipt.'
        }
        return
    }
    if ($PassiveId -in @(10606,10607)) {
        if (-not (Select-String -LiteralPath $runtimeLog -SimpleMatch "[Acquired passive combat] $PassiveId cultivated") -or
            -not (Test-Path -LiteralPath (Join-Path $Run 'companion-cold-checkpoint.json'))) { throw 'The cultivated secret did not exercise its original cold path.' }
    } elseif ($PassiveId -in @(10503,10543,10557,10568,10573,10579,10592,10594,10595,10597,10598,10599,10601,10608,10609)) {
        $source = Get-Content -LiteralPath (Join-Path $Run 'companion-cultivated-save.json') -Raw | ConvertFrom-Json
        if ($source.Player.Skills.Plans[$source.Player.Skills.SelectedPlan].PassiveSkills[0] -ne $PassiveId) {
            throw 'The battle used another cultivated secret.'
        }
    } else { throw 'Unsupported acquired passive evidence.' }
    if ($PassiveId -eq 10599 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-flame-soul-checkpoint.json')) -or
        -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png')))) {
        throw 'The cultivated fire-cast critical stack was not captured and persisted.'
    }
    if ($PassiveId -eq 10598) {
        foreach ($file in @('companion-hurricane-grief-status.json','companion-hurricane-grief-movement.json',
            'acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'The cultivated wind laceration was not rendered and settled during movement.' }
        }
    }
    if ($PassiveId -eq 10597) {
        foreach ($file in @('companion-fire-life-checkpoint.json','companion-fire-life-status-events.json',
            'acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) {
                throw 'The cultivated fire life did not render and persist its round-end cure.'
            }
        }
        $receipt = Get-Content -LiteralPath (Join-Path $Run 'companion-fire-life-status-events.json') -Raw | ConvertFrom-Json
        if ($receipt.invokes -lt 1 -or $receipt.cures -lt 1 -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10597 cultivated fire life round-end Invoke, cure, HP cap and restore replay passed.')) {
            throw 'The cultivated fire life status Invoke or restore replay receipt is incomplete.'
        }
        $checkpoint = Get-Content -LiteralPath (Join-Path $Run 'companion-fire-life-checkpoint.json') -Raw | ConvertFrom-Json
        $actor = $checkpoint.checkpoint.Units | Where-Object { $_.Id -eq 1 }
        $listener = $actor.Statuses | Where-Object { $_.ClassId -eq 25098 -and $_.ApplyingSkillItemId -eq 10597 } | Select-Object -First 1
        if ($null -eq $listener -or $listener.RoundEndTriggeredCount -lt 1 -or
            -not ($checkpoint.checkpoint.GridItems | Where-Object { $_.EntityClassId -eq 3051 -and -not $_.Ended })) {
            throw 'The fire life checkpoint lost its cultivated listener, trigger count or live burning tile.'
        }
    }
    if ($PassiveId -in @(10607,10608) -and -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png'))) {
        throw 'The original cultivated passive effect was not captured.'
    }
    if ($PassiveId -eq 10609) {
        foreach ($status in @(24394,24393)) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run "acquired-active-ui-tactical-$status.png")) -or
                -not (Test-Path -LiteralPath (Join-Path $Run "companion-tactical-$status.json"))) {
                throw 'The original tactical threshold state/effect was not captured.'
            }
        }
    }
    if ($PassiveId -eq 10568 -and -not (Test-Path -LiteralPath (Join-Path $Run 'companion-assault-checkpoint.json'))) {
        throw 'The original cultivated defence ignore was not exercised.'
    }
    if ($PassiveId -eq 10594 -and -not (Test-Path -LiteralPath (Join-Path $Run 'companion-soul-breaker-checkpoint.json'))) {
        throw 'The original cultivated target missing-health bonus was not exercised.'
    }
    if ($PassiveId -eq 10592 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-steel-faith-checkpoint.json')) -or
        -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10592 actual cultivated units/taunt'))) {
        throw 'The cultivated steel faith was not checked with the actual taunted combat units.'
    }
    if ($PassiveId -eq 10579 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-alienation-checkpoint.json')) -or
        -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10579 cultivated afflicted attacker passed.'))) {
        throw 'The cultivated alienation did not exercise the afflicted attacker.'
    }
    if ($PassiveId -eq 10595 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-wind-orb-checkpoint.json')) -or
        -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png')))) {
        throw 'The cultivated wind orb was not rendered and released.'
    }
    if ($PassiveId -eq 10543 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-flame-momentum-checkpoint.json')) -or
        -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png')))) {
        throw 'The cultivated critical fire blast was not rendered and settled.'
    }
    if ($PassiveId -eq 10503 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-fire-core-checkpoint.json')) -or
        -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png')) -or
        -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10503 cultivated fire core burst and restore replay passed.'))) {
        throw 'The cultivated fire core did not render, settle or restore its original burst.'
    }
    if ($PassiveId -eq 10557 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-ice-crystal-checkpoint.json')) -or
        -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png')) -or
        -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10557 cultivated ice crystal thaw and restore replay passed.'))) {
        throw 'The cultivated ice crystal did not render, settle or restore its original thaw strike.'
    }
    if ($PassiveId -eq 10601) {
        foreach ($file in @('companion-overflow-shield-checkpoint.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) {
                throw 'The original cultivated overflow shield did not settle and render.'
            }
        }
    }
    if ($PassiveId -eq 10573 -and (-not (Test-Path -LiteralPath (Join-Path $Run 'companion-overload-protection-checkpoint.json')) -or
        -not (Test-Path -LiteralPath (Join-Path $Run 'acquired-active-ui-passive-battle.png')) -or
        -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10573 cultivated overload protection shield and restore replay passed.'))) {
        throw 'The cultivated overload protection did not render, settle or restore its original shield.'
    }
}
$ErrorActionPreference = 'Stop'
$requestedSkills = @(@($SkillId) + $AdditionalSkillIds | Select-Object -Unique)
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'windows_player_snapshot.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use a marked validation workspace.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation repository.' }
if ($SkillSkinId -gt 0 -and ($SkillId -ne 10082 -or $AdditionalSkillIds.Count -gt 0 -or $PassiveSave)) { throw 'Use the original water-blade skin with its base active skill.' }
if ($PassiveSave) {
    $PassiveSave = (Get-Item -LiteralPath $PassiveSave).FullName
    if (-not $PassiveSave.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $SkillId -notin @(10028,10035,10047,10068,10084,10088,10089,10091,10096) -or $AdditionalSkillIds.Count -gt 0) { throw 'Use the isolated passive cultivation save with its corresponding active skill.' }
    $passiveResult = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PassiveSave) 'result.json') -Raw | ConvertFrom-Json
    if (-not $passiveResult.passed) { throw 'The original passive cultivation flow has not passed.' }
    $passiveProfile = Get-Content -LiteralPath $PassiveSave -Raw | ConvertFrom-Json
    $passiveId = $passiveProfile.Player.Skills.Plans[$passiveProfile.Player.Skills.SelectedPlan].PassiveSkills[0]
}
if ($PSCmdlet.ParameterSetName -eq 'Play') {
    $projectPath = (Get-Item -LiteralPath $Project).FullName
    $executable = 'D:/Tool/Unity/Editor/Unity.exe'
    if (-not (Test-Path -LiteralPath $executable)) { throw 'Configured Unity Editor is absent.' }
} else { $executable = Resolve-ZjcsWindowsPlayerSnapshot -PlayerPath $Player -Workspace $workspacePath -Repository $repository }
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
    if ($SkillSkinId -gt 0) { $env:ZJCS_SKILLS_ACQUIRED_SKIN = [string]$SkillSkinId }
    if ($PassiveSave) { $env:ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE = $PassiveSave }
    if ($requestedSkills.Count -gt 1) {
        $env:ZJCS_SKILLS_ACQUIRED_ACTIVE_SEQUENCE = $requestedSkills -join ','
    }
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
        if ($timer.Elapsed.TotalMinutes -gt 25 * $requestedSkills.Count -or ($null -ne $resultSeenAt -and
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
    if ($SkillSkinId -gt 0) {
        foreach ($phase in @('skin-selection','skin-equipped')) {
            if (-not (Test-Path -LiteralPath (Join-Path $run "acquired-active-ui-$phase.png"))) { throw 'The original skin selection flow was not captured.' }
        }
        if (-not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active skin] 39521 selected, equipped and persisted through original input.')) { throw 'The original skill skin did not persist.' }
    }
    if ($PassiveSave) {
        Assert-AcquiredPassiveFlowEvidence $run $passiveId
    }
    $identity = Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/Game/Runtime/Offline/SkillAssets/OfficialSkillUiData.Identity.cs') -Raw
    if ($identity -notmatch 'LayoutSha256 = "([0-9a-f]{64})"') { throw 'The compiled skill UI identity is absent.' }
    $expectedUi = $Matches[1]
    if (-not (Select-String -LiteralPath $log -Pattern ('\[Skill UI data\] Verified (embedded TextAsset|native Windows pack): ' + $expectedUi))) {
        throw "The runtime skill UI did not verify its current payload identity: $run"
    }
    foreach ($SkillId in $requestedSkills) {
        $skillRun = if ($requestedSkills.Count -gt 1) { Join-Path $run "skill-$SkillId" } else { $run }
        $skillResult = Get-Content -LiteralPath (Join-Path $skillRun 'result.json') -Raw | ConvertFrom-Json
        if (-not $skillResult.passed) { throw "Skill $SkillId failed: $($skillResult.error) ($skillRun)" }
        if (-not ($PassiveSave -and (($passiveId -in @(10503,10573,10579,10599) -and $SkillId -eq 10028) -or
            ($passiveId -eq 10557 -and $SkillId -eq 10035))) -and
            $SkillId -in @(10000,10010,10017,10028,10035,10044,10046,10048,10049,10066) -and
            -not (Select-String -LiteralPath $log -SimpleMatch "[Passive affinity wish] item=$SkillId granted")) {
            throw "The original profession wish pool did not grant skill $SkillId : $run"
        }
        # PowerShell's JSON conversion of the complete official combat tables is
        # disproportionately expensive; read only the selected item and ranks.
        $rankQuery = "import json,sys; c=json.load(open(sys.argv[1],encoding='utf-8')); s=json.load(open(sys.argv[2],encoding='utf-8')); rows=[x for x in c['item'] if x['ClassId']==int(sys.argv[3])]; assert len(rows)==1; print(min(x['Rank'] for x in s['RankDisplays'] if x['Quality']==rows[0]['Quality'])+1)"
        $cultivatedRank = & python -B -c $rankQuery (Join-Path $repository 'unity/Assets/Official/Combat/rules.json') (Join-Path $repository 'unity/Assets/Official/Skills/skill-runtime-rules.json') $SkillId
        if ($LASTEXITCODE -ne 0 -or $cultivatedRank -notmatch '^\d+$') { throw "The original skill rank could not be resolved: $SkillId" }
        $kind = if ($SkillId -in @(10024,10032,10046,10051,10066,10068,10077,10092,10102)) { 'status' } else { 'damage' }
        foreach ($text in @(
            "[Acquired active UI] $SkillId fragment acquisition, original slot input, level/rank cultivation and disk reload passed.",
            "[Acquired active UI] $SkillId saved rank=$cultivatedRank level=2, original $kind/result/return and unchanged save passed.")) {
            if (-not (Select-String -LiteralPath $log -SimpleMatch $text)) { throw "Missing skill-page assertion: $text" }
        }
        foreach ($phase in @('character','pieces','merge-confirm','merge-skill-detail','merge-piece-detail','acquired-detail','equipped','level-up','rank-up','reloaded',
            'settings','preparation','battle','result','return')) {
            if (-not (Test-Path -LiteralPath (Join-Path $skillRun "acquired-active-ui-$phase.png"))) {
                throw "Missing skill-page runtime frame: $phase ($run)"
            }
        }
        if (-not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-active-ui-checkpoint.json'))) {
            throw "The completed skill-page battle checkpoint is absent: $run"
        }
        if ($SkillId -eq 10102 -and (-not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-protective-runes-checkpoint.json')) -or
            -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10102 cultivated conditional reduction,'))) {
            throw 'The cultivated protective runes omitted their original conditional damage check.'
        }
        if ($SkillId -eq 10092 -and (-not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-active-ui-battle.png')) -or
            -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-battlecry-checkpoint.json')))) {
            throw 'The cultivated Battlecry omitted its native attack buff or immediate next-action advance.'
        }
        if ($SkillId -eq 10002 -and (-not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-active-ui-blinding.png')) -or
            -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-star-burst-blinding-checkpoint.json')))) { throw 'The original cultivated blinding was not rendered and persisted.' }
        if ($SkillId -eq 10083 -and -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-active-ui-opening.png'))) {
            throw "The original four opening swords were not rendered: $run"
        }
        if ($SkillId -eq 10091 -and (-not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-active-ui-laceration.png')) -or
            -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-spiral-wind-blade-checkpoint.json')))) {
            throw 'The original cultivated laceration was not captured and persisted.'
        }
        if ($SkillId -eq 10059 -and (-not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-active-ui-confusion.png')) -or
            -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-chaos-rune-checkpoint.json')))) {
            throw 'The original cultivated confusion was not captured and persisted.'
        }
        if ($SkillId -eq 10103 -and -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-sage-dark-blast-checkpoint.json'))) {
            throw 'The original cultivated dark blast checkpoint was not persisted.'
        }
        if ($SkillId -eq 10077 -and -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-sage-psychic-ward-checkpoint.json'))) {
            throw 'The consumed psychic ward checkpoint was not persisted.'
        }
        if ($SkillId -eq 10096 -and -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-heavenly-rain-checkpoint.json'))) {
            throw 'The original cultivated healing checkpoint was not persisted.'
        }
        if ($SkillId -in @(10002,10032,10039,10047,10056,10059,10068,10073,10077,10079,10082,10083,10084,10085,10088,10089,10091,10092,10093,10096,10099,10102,10103,10104)) {
            # The Player asserts the original effects for each equipped cast.
            Write-Output "Acquired active skill-page flow passed: $skillRun"
            continue
        }
        $receiptVerifier = if ($SkillId -eq 10000) { 'verify_water_bullet_receipt.py' } elseif ($SkillId -eq 10008) { 'verify_warrior_slash_receipt.py' } elseif ($SkillId -eq 10009) { 'verify_heavy_strike_receipt.py' } elseif ($SkillId -eq 10010) { 'verify_stun_strike_receipt.py' } elseif ($SkillId -eq 10011) { 'verify_lion_combo_receipt.py' } elseif ($SkillId -eq 10012) { 'verify_wind_blade_receipt.py' } elseif ($SkillId -eq 10013) { 'verify_four_way_slash_receipt.py' } elseif ($SkillId -eq 10014) { 'verify_whirlwind_slash_receipt.py' } elseif ($SkillId -eq 10015) { 'verify_jump_slash_receipt.py' } elseif ($SkillId -eq 10016) { 'verify_lightning_dash_receipt.py' } elseif ($SkillId -eq 10017) { 'verify_water_thread_receipt.py' } elseif ($SkillId -eq 10018) { 'verify_pull_skill_receipt.py' } elseif ($SkillId -eq 10024) { 'verify_arc_shield_receipt.py' } elseif ($SkillId -eq 10028) { 'verify_fireball_receipt.py' } elseif ($SkillId -eq 10035) { 'verify_ice_cone_receipt.py' } elseif ($SkillId -eq 10043) { 'verify_falling_wind_receipt.py' } elseif ($SkillId -eq 10044) { 'verify_frost_flower_receipt.py' } elseif ($SkillId -eq 10046) { 'verify_guard_shield_receipt.py' } elseif ($SkillId -eq 10048) { 'verify_bouncing_shield_receipt.py' } elseif ($SkillId -eq 10049) { 'verify_challenge_receipt.py' } elseif ($SkillId -eq 10051) { 'verify_battle_spirit_receipt.py' } elseif ($SkillId -eq 10053) { 'verify_sunset_sword_receipt.py' } elseif ($SkillId -eq 10066) { 'verify_encouragement_receipt.py' } else { 'verify_fire_slash_receipt.py' }
        $receiptArgs = @((Join-Path $skillRun 'acquired-active-ui-checkpoint.json'))
        if ($SkillId -eq 10017) { $receiptArgs += '--require-cold' }
        & python -B (Join-Path $PSScriptRoot $receiptVerifier) @receiptArgs
        if ($LASTEXITCODE -ne 0) { throw "The acquired skill's original battle receipts failed: $run" }
        Write-Output "Acquired active skill-page flow passed: $skillRun"
    }
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($name in $previous.Keys) { Set-Item ('Env:' + $name) $previous[$name] }
}

param(
    [Parameter(Mandatory, ParameterSetName='Player')][string]$Player,
    [Parameter(Mandatory, ParameterSetName='Play')][string]$Project,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10000,10002,10008,10009,10010,10011,10012,10013,10014,10015,10016,10017,10018,10024,10026,10028,10032,10035,10037,10039,10043,10044,10046,10047,10048,10049,10051,10053,10056,10057,10059,10062,10063,10065,10066,10070,10068,10073,10077,10079,10082,10083,10084,10085,10088,10089,10091,10092,10093,10095,10096,10097,10098,10099,10100,10101,10102,10103,10104)][int]$SkillId = 10026,
    [Parameter(ParameterSetName='Player')]
    [ValidateSet(10000,10002,10008,10009,10010,10011,10012,10013,10014,10015,10016,10017,10018,10024,10026,10028,10032,10035,10037,10039,10043,10044,10046,10047,10048,10049,10051,10053,10056,10057,10059,10062,10063,10065,10066,10070,10068,10073,10077,10079,10082,10083,10084,10085,10088,10089,10091,10092,10093,10095,10096,10097,10098,10099,10100,10101,10102,10103,10104)][int[]]$AdditionalSkillIds = @(),
    [string]$PassiveSave,
    [string]$ActiveSave,
    [ValidateSet(0,39521,39523)][int]$SkillSkinId = 0,
    [ValidateRange(0,2147483646)][int]$BattleSeed = 0
)
function Assert-AcquiredAsuraHandEvidence([string]$Run) {
    foreach ($file in @('acquired-asura-hand-checkpoint.json','acquired-asura-hand-receipt.json',
        'companion-active-cultivated-save.json','acquired-active-ui-battle.png','acquired-active-ui-asura-attack.png')) {
        if (-not (Test-Path (Join-Path $Run $file))) { throw 'The cultivated Asura Hand evidence is incomplete.' }
    }
    $receipt = Get-Content (Join-Path $Run 'acquired-asura-hand-receipt.json') -Raw | ConvertFrom-Json
    if ($receipt.buffs -lt 1 -or $receipt.convertedHits -lt 6 -or
        -not $receipt.restored -or -not $receipt.attackCaptured) {
        throw 'Asura Hand lost its buff, full skill conversion, consumption or restoration.'
    }
    if (-not (Select-String -LiteralPath (Join-Path $Run 'runtime.log') -SimpleMatch '[Acquired active combat] 10063 cultivated self buff, six-hit breaking conversion, consumption and restoration passed.')) {
        throw 'The real Asura Hand battle assertion did not pass.'
    }
}
function Assert-AcquiredEclipseSlashesEvidence([string]$Run) {
    $result = Get-Content (Join-Path $Run 'result.json') -Raw | ConvertFrom-Json
    $exit = Get-Content (Join-Path $Run 'process-exit.json') -Raw | ConvertFrom-Json
    if (-not $result.passed -or $exit.ExitCode -ne 0) { throw 'The actual Eclipse Slashes run did not complete.' }
    foreach ($file in @('acquired-eclipse-slashes-checkpoint.json','acquired-active-ui-battle.png')) {
        if (-not (Test-Path (Join-Path $Run $file))) { throw 'Missing cultivated Eclipse Slashes battle evidence.' }
    }
    $checkpoint = Get-Content (Join-Path $Run 'acquired-eclipse-slashes-checkpoint.json') -Raw | ConvertFrom-Json
    $casts = 0
    foreach ($receipt in $checkpoint.checkpoint.Receipts) {
        foreach ($cast in $receipt.Event.SkillCasts | Where-Object { $_.SkillItemId -eq 10065 -and $_.Slot -eq 0 }) {
            $damage = @($receipt.Event.DamageEvents | Where-Object { $_.DamageId -in $cast.DamageEventIds })
            if ($cast.SkillEntityId -ne 11440 -or $damage.Count -ne 6 -or
                @($damage | Where-Object { $_.ElementType -ne 0 -or $_.DamageType -ne 1 -or $_.TrueDamage -or $_.IsCure }).Count) {
                throw 'Eclipse Slashes lost one of its original six physical damage receipts.'
            }
            $casts++
        }
    }
    $verified = Select-String -LiteralPath (Join-Path $Run 'runtime.log') -SimpleMatch -Pattern '[Acquired active combat] 10065 cultivated six physical hits, final strike, timing and restoration passed.'
    if ($casts -lt 1 -or -not $verified) {
        throw 'The actual Eclipse Slashes cultivation, hit timing or restore verification was not recorded.'
    }
}
function Assert-AcquiredPassiveFlowEvidence([string]$Run, [int]$PassiveId) {
    $result = Get-Content -LiteralPath (Join-Path $Run 'result.json') -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw 'The cultivated passive combat flow did not pass.' }
    $runtimeLog = Join-Path $Run 'runtime.log'
    if ($PassiveId -eq 10547) {
        foreach ($file in @('companion-cultivated-save.json','companion-desperate-valor-checkpoint.json',
            'companion-desperate-valor-receipt.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path (Join-Path $Run $file))) { throw 'Missing cultivated Desperate Valor battle evidence.' }
        }
        $receipt = Get-Content (Join-Path $Run 'companion-desperate-valor-receipt.json') -Raw | ConvertFrom-Json
        if ($receipt.hits -lt 6 -or $receipt.maximumLayers -ne 5 -or -not $receipt.restored -or -not $receipt.captured -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10547 cultivated current-HP five-layer damage, native status and restore replay passed.')) {
            throw 'Desperate Valor did not exercise its cultivated capped layers, actual damage, native view and restoration.'
        }
        return
    }
    if ($PassiveId -eq 10581) {
        foreach ($file in @('companion-cultivated-save.json','companion-potential-rebirth-checkpoint.json',
            'companion-potential-rebirth-receipt.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'Missing cultivated Potential Rebirth battle evidence.' }
        }
        $receipt = Get-Content (Join-Path $Run 'companion-potential-rebirth-receipt.json') -Raw | ConvertFrom-Json
        if ($receipt.cures -ne 1 -or $receipt.healed -lt 1 -or -not $receipt.restored -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10581 fatal survival, cultivated healing, dispersal and restore passed.')) {
            throw 'Potential Rebirth did not exercise its single cultivated cure, dispersal and restoration.'
        }
        return
    }
    if ($PassiveId -eq 10605) {
        foreach ($file in @('companion-cultivated-save.json','companion-ice-guardian-checkpoint.json',
            'companion-ice-guardian-receipt.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'Missing cultivated ice guardian battle evidence.' }
        }
        $receipt = Get-Content (Join-Path $Run 'companion-ice-guardian-receipt.json') -Raw | ConvertFrom-Json
        if ($receipt.activeRounds -lt 1 -or $receipt.idleRounds -lt 1 -or $receipt.starts -lt 1 -or
            $receipt.ends -lt 1 -or $receipt.heals -lt 1 -or $receipt.restoredHp -lt 1 -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10605 idle-round protection, thaw healing, cultivation and restore passed.')) {
            throw 'Ice guardian did not exercise its cultivated protection, active-round suppression, thaw cure or restore.'
        }
        return
    }
    if ($PassiveId -eq 10577) {
        foreach ($file in @('companion-cultivated-save.json','companion-light-scorch-checkpoint.json',
            'companion-light-scorch-receipt.json','acquired-active-ui-passive-battle.png','acquired-active-ui-passive-status.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'Missing cultivated light scorch battle evidence.' }
        }
        $receipt = Get-Content (Join-Path $Run 'companion-light-scorch-receipt.json') -Raw | ConvertFrom-Json
        if ($receipt.parentHits -lt $receipt.procs -or $receipt.procs -lt 1 -or $receipt.lightDamage -lt 1 -or
            $receipt.starts -lt 1 -or $receipt.ends -lt 1 -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10577 light damage, blinding consumption, hit timing and restore passed.')) {
            throw 'Light scorch did not exercise its actual light damage, timing, blinding and restoration.'
        }
        return
    }
    if ($PassiveId -eq 10604) {
        foreach ($file in @('companion-cultivated-save.json','companion-quick-casting-checkpoint.json','companion-quick-casting-receipt.json',
            'acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'Missing cultivated quick casting battle evidence.' }
        }
        if (-not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive combat] 10604 opening cooldown, timing and restore passed.')) {
            throw 'Cultivated quick casting did not reach the actual opening battle.'
        }
        $receipt = Get-Content (Join-Path $Run 'companion-quick-casting-receipt.json') -Raw | ConvertFrom-Json
        if ($receipt.openers -ne 1 -or $receipt.cooldownActions -ne [int]($receipt.eligibleSlots -gt 0) -or
            ($receipt.active -eq 10093 -and $receipt.eligibleSlots -lt 1) -or
            ($receipt.cooldownActions -gt 0 -and [Math]::Abs($receipt.delay - 0.35) -gt 0.0001)) {
            throw 'Quick casting did not exercise its actual eligible cooldown and authored timing.'
        }
        return
    }
    if ($PassiveId -eq 10525) {
        foreach ($file in @('companion-cultivated-save.json','companion-affinity-checkpoint.json','acquired-active-ui-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'Missing cultivated affinity battle evidence.' }
        }
        if (-not (Select-String -LiteralPath $runtimeLog -SimpleMatch '[Acquired passive affinity] 10525 actual light damage and restore passed.')) {
            throw 'Cultivated affinity did not reach the actual battle.'
        }
        return
    }
    if ($PassiveId -in @(10591,10600)) {
        foreach ($file in @('companion-cultivated-save.json', 'acquired-active-ui-passive-battle.png',
            "companion-sage-summon-$PassiveId-checkpoint.json", "companion-sage-summon-$PassiveId-receipt.json")) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'The actual summon-secret evidence is incomplete.' }
        }
        $receipt = Get-Content -LiteralPath (Join-Path $Run "companion-sage-summon-$PassiveId-receipt.json") -Raw | ConvertFrom-Json
        $message = "[Acquired passive combat] $PassiveId cultivated summon secret, actual properties, native view and restore replay passed."
        if ($receipt.item -ne $PassiveId -or $receipt.rank -lt 1 -or $receipt.level -ne 2 -or
            $receipt.observations -lt 1 -or -not $receipt.restored -or -not $receipt.captured -or
            ($PassiveId -eq 10600 -and $receipt.cures -lt 1) -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $message)) {
            throw 'The cultivated summon secret lost its source, actual effect, rendering or restore receipt.'
        }
        return
    }
    if ($PassiveId -in @(10610,10611)) {
        foreach ($file in @('companion-cultivated-save.json',
            "companion-sage-round-$PassiveId-checkpoint.json", "companion-sage-round-$PassiveId-receipt.json")) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) {
                throw 'The cultivated personal-round secret evidence is incomplete.'
            }
        }
        $source = Get-Content -LiteralPath (Join-Path $Run 'companion-cultivated-save.json') -Raw | ConvertFrom-Json
        $receipt = Get-Content -LiteralPath (Join-Path $Run "companion-sage-round-$PassiveId-receipt.json") -Raw | ConvertFrom-Json
        $expected = if ($PassiveId -eq 10610) { @(24395) } else { @(24396,24398,24399) }
        $message = "[Acquired passive combat] $PassiveId cultivated personal-round gains, actual properties, native effects and restore replay passed."
        if ($source.Player.Skills.Plans[$source.Player.Skills.SelectedPlan].PassiveSkills[0] -ne $PassiveId -or
            $receipt.item -ne $PassiveId -or $receipt.rank -lt 1 -or $receipt.level -lt 1 -or
            $receipt.turns -lt 2 -or -not $receipt.restored -or
            @($receipt.seen).Count -ne $expected.Count -or @($receipt.captured).Count -ne $expected.Count -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $message)) {
            throw 'The cultivated personal-round secret lost its source, gain, native effect or restore receipt.'
        }
        foreach ($id in $expected) {
            if ($id -notin $receipt.seen -or $id -notin $receipt.captured -or
                -not (Test-Path -LiteralPath (Join-Path $Run "acquired-active-ui-sage-round-$id.png"))) {
                throw 'Every original personal-round gain must be observed and rendered.'
            }
        }
        return
    }
    if ($PassiveId -eq 10572) {
        foreach ($file in @('companion-cultivated-save.json','companion-vitality-spring-checkpoint.json',
            'companion-vitality-spring-status-events.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'The actual vitality spring cure evidence is incomplete.' }
        }
        $source = Get-Content -LiteralPath (Join-Path $Run 'companion-cultivated-save.json') -Raw | ConvertFrom-Json
        $receipt = Get-Content -LiteralPath (Join-Path $Run 'companion-vitality-spring-status-events.json') -Raw | ConvertFrom-Json
        $message = '[Acquired passive combat] 10572 cultivated vitality spring round-start Invoke, cure, formula and restore replay passed.'
        if ($source.Player.Skills.Plans[$source.Player.Skills.SelectedPlan].PassiveSkills[0] -ne 10572 -or
            $receipt.invokes -lt 1 -or $receipt.cures -lt 1 -or $receipt.actual -ne $receipt.expected -or
            $receipt.expected -le 0 -or $receipt.remaining -ne -1 -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $message)) {
            throw 'The cultivated vitality spring did not preserve its unlimited trigger, cure formula or restore receipt.'
        }
        return
    }
    if ($PassiveId -eq 10580) {
        foreach ($file in @('companion-cultivated-save.json','companion-sanctuary-grant.json',
            'companion-sanctuary-transfer.json','companion-sanctuary-receipt.json',
            'acquired-active-ui-opening.png','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'The actual sanctuary grant or transferred hit evidence is incomplete.' }
        }
        $source = Get-Content -LiteralPath (Join-Path $Run 'companion-cultivated-save.json') -Raw | ConvertFrom-Json
        $receipt = Get-Content -LiteralPath (Join-Path $Run 'companion-sanctuary-receipt.json') -Raw | ConvertFrom-Json
        $message = '[Acquired passive combat] 10580 cultivated allied sanctuary, ordered original transfer and restore replay passed.'
        if ($source.Player.Skills.Plans[$source.Player.Skills.SelectedPlan].PassiveSkills[0] -ne 10580 -or
            $receipt.ally -le 1 -or $receipt.attacker -le 1 -or $receipt.parentIndex -ge $receipt.transferIndex -or
            $receipt.guardianDamage -gt 0 -or -not $receipt.restored -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $message)) {
            throw 'The cultivated sanctuary did not preserve its target, ordered transfer or restore receipt.'
        }
        return
    }
    if ($PassiveId -eq 10575) {
        foreach ($file in @('companion-cultivated-save.json','companion-soul-guard-opening.json',
            'companion-soul-guard-return.json','companion-soul-guard-receipt.json','acquired-active-ui-passive-battle.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Run $file))) { throw 'The actual soul guard opening or victory evidence is incomplete.' }
        }
        $source = Get-Content -LiteralPath (Join-Path $Run 'companion-cultivated-save.json') -Raw | ConvertFrom-Json
        $receipt = Get-Content -LiteralPath (Join-Path $Run 'companion-soul-guard-receipt.json') -Raw | ConvertFrom-Json
        $opening = '[Acquired passive combat] 10575 cultivated opening HP cost, converted shield and restore replay passed.'
        $victory = '[Acquired passive combat] 10575 surviving victory return, original cure and restore replay passed.'
        if ($source.Player.Skills.Plans[$source.Player.Skills.SelectedPlan].PassiveSkills[0] -ne 10575 -or
            $receipt.cost -le 0 -or $receipt.principal -le 0 -or $receipt.actual -ne $receipt.expected -or
            $receipt.healed -le 0 -or -not $receipt.restored -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $opening) -or
            -not (Select-String -LiteralPath $runtimeLog -SimpleMatch $victory)) {
            throw 'The cultivated soul guard did not preserve its HP cost, original return cure or restore receipts.'
        }
        return
    }
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
if ($SkillSkinId -gt 0 -and ((($SkillId -ne 10082 -or $SkillSkinId -ne 39521) -and ($SkillId -ne 10100 -or $SkillSkinId -ne 39523)) -or $AdditionalSkillIds.Count -gt 0 -or $PassiveSave)) { throw 'Use an original skill skin with its corresponding base active skill.' }
if ($PassiveSave) {
    $PassiveSave = (Get-Item -LiteralPath $PassiveSave).FullName
    if (-not $PassiveSave.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $SkillId -notin @(10017,10028,10032,10035,10047,10065,10068,10084,10088,10089,10091,10093,10095,10096,10098) -or $AdditionalSkillIds.Count -gt 0) { throw 'Use the isolated passive cultivation save with its corresponding active skill.' }
    $passiveResult = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PassiveSave) 'result.json') -Raw | ConvertFrom-Json
    if (-not $passiveResult.passed) { throw 'The original passive cultivation flow has not passed.' }
    $passiveProfile = Get-Content -LiteralPath $PassiveSave -Raw | ConvertFrom-Json
    $passiveId = $passiveProfile.Player.Skills.Plans[$passiveProfile.Player.Skills.SelectedPlan].PassiveSkills[0]
}
if ($ActiveSave) {
    $ActiveSave = (Get-Item -LiteralPath $ActiveSave).FullName
    if ($SkillId -ne 10063 -or $PassiveSave -or $AdditionalSkillIds.Count -gt 0 -or
        -not $ActiveSave.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Use the actual isolated Eclipse cultivation save as the Asura companion.'
    }
    $sourceDirectory = Split-Path -Parent $ActiveSave
    while ($sourceDirectory.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        -not (Test-Path (Join-Path $sourceDirectory 'result.json'))) {
        $sourceDirectory = Split-Path -Parent $sourceDirectory
    }
    $sourceResult = Get-Content (Join-Path $sourceDirectory 'result.json') -Raw | ConvertFrom-Json
    if (-not $sourceResult.passed) { throw 'The active companion acquisition/cultivation flow has not passed.' }
}
if ($SkillId -eq 10063 -and -not $ActiveSave) { throw 'Asura Hand requires its actual cultivated active companion save.' }
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
    if ($BattleSeed -gt 0) { $env:ZJCS_SKILLS_BATTLE_SEED = [string]$BattleSeed }
    $env:ZJCS_SKILLS_ACQUIRED_ACTIVE = [string]$SkillId
    if ($SkillSkinId -gt 0) { $env:ZJCS_SKILLS_ACQUIRED_SKIN = [string]$SkillSkinId }
    if ($PassiveSave) { $env:ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE = $PassiveSave }
    if ($ActiveSave) { $env:ZJCS_SKILLS_ACQUIRED_ACTIVE_SAVE = $ActiveSave }
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
    # Editor success is followed by native asset unloading and database refresh.
    $exitGraceSeconds = if ($PSCmdlet.ParameterSetName -eq 'Play') { 600 } else { 120 }
    while (-not $process.WaitForExit(5000)) {
        if ($null -eq $resultSeenAt -and (Test-Path -LiteralPath (Join-Path $run 'result.json'))) {
            $resultSeenAt = $timer.Elapsed.TotalSeconds
        }
        if ($timer.Elapsed.TotalMinutes -gt 25 * $requestedSkills.Count -or ($null -ne $resultSeenAt -and
            $timer.Elapsed.TotalSeconds - $resultSeenAt -gt $exitGraceSeconds) -or ((Test-Path -LiteralPath $log) -and
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
        if (-not (Select-String -LiteralPath $log -SimpleMatch "[Acquired active skin] $SkillSkinId selected, equipped and persisted through original input.")) { throw 'The original skill skin did not persist.' }
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
        $kind = if ($SkillId -in @(10024,10032,10046,10051,10063,10066,10068,10077,10092,10102)) { 'status' } else { 'damage' }
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
        if ($SkillId -eq 10100) {
            $receipt = Get-Content -LiteralPath (Join-Path $skillRun 'acquired-flowing-fire-receipt.json') -Raw | ConvertFrom-Json
            if ($receipt.extras -lt 1 -or $receipt.extras -gt 2 -or $receipt.rank -ne [int]$cultivatedRank -or $receipt.level -ne 2 -or
                $receipt.skin -ne ($SkillSkinId -eq 39523) -or -not $receipt.restored -or
                -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10100 cultivated sequential extras, original fire impacts and restore replay passed.')) {
                throw 'The cultivated flowing fire lost its extra-cast chain or restore receipt.'
            }
            foreach ($frame in 1..8) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun "acquired-active-ui-fire-hit-$frame.png"))) {
                    throw 'The original flowing-fire sequence was not captured.'
                }
            }
        }
        if ($SkillId -eq 10101) {
            foreach ($file in @('acquired-fire-leap-checkpoint.json','acquired-fire-leap-receipt.json',
                'acquired-active-ui-jump-hit-1.png','acquired-active-ui-jump-hit-2.png',
                'acquired-active-ui-jump-hit-3.png','acquired-active-ui-jump-hit-4.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The cultivated fire leap evidence is incomplete.' }
            }
            $leap = Get-Content -LiteralPath (Join-Path $skillRun 'acquired-fire-leap-receipt.json') -Raw | ConvertFrom-Json
            if ($leap.target -le 1 -or $leap.distance -lt 1 -or $leap.distance -gt 2 -or $leap.pathCount -ne 2 -or
                $leap.rank -ne $cultivatedRank -or $leap.level -ne 2 -or $leap.damage -ge 0 -or -not $leap.restored -or
                -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10101 cultivated direct jump, adjacent landing, original fire impact and restore replay passed.')) {
                throw 'The acquired fire leap changed its cultivated jump, original impact or restore receipt.'
            }
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
        if ($SkillId -eq 10097) {
            foreach ($file in @('acquired-voodoo-doll-checkpoint.json','acquired-voodoo-doll-receipt.json',
                'acquired-active-ui-opening.png','acquired-active-ui-battle.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The actual substitute evidence is incomplete.' }
            }
            $receipt = Get-Content -LiteralPath (Join-Path $skillRun 'acquired-voodoo-doll-receipt.json') -Raw | ConvertFrom-Json
            if ($receipt.summons -lt 1 -or $receipt.transfers -lt 1 -or $receipt.expirations -lt 1 -or
                -not $receipt.restored -or -not $receipt.captured) { throw 'The original substitute lost its link, true damage, lifetime or restore receipt.' }
        }
        if ($SkillId -eq 10095) {
            foreach ($file in @('acquired-rock-emperor-checkpoint.json','acquired-active-ui-opening.png','acquired-active-ui-battle.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The cultivated rock emperor did not render and persist its original summon.' }
            }
        }
        if ($SkillId -eq 10063) { Assert-AcquiredAsuraHandEvidence $skillRun }
        if ($SkillId -eq 10065) { Assert-AcquiredEclipseSlashesEvidence $skillRun }
        if ($SkillId -eq 10070) {
            foreach ($file in @('acquired-meteor-fire-checkpoint.json','acquired-meteor-fire-strikes.json',
                'acquired-meteor-fire-receipt.json','acquired-active-ui-meteor-formation.png','acquired-active-ui-meteor-burning.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The cultivated meteor fire evidence is incomplete.' }
            }
            $meteor = Get-Content (Join-Path $skillRun 'acquired-meteor-fire-receipt.json') -Raw | ConvertFrom-Json
            if ($meteor.casts -lt 1 -or $meteor.hits -lt 1 -or $meteor.burningHits -lt 1 -or $meteor.grids -lt 1 -or
                $meteor.ended -lt 1 -or -not $meteor.burnCaptured -or -not $meteor.impactCaptured -or -not $meteor.restored -or
                -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10070 fire impact, burning ground, current status view and restore passed.')) {
                throw 'The cultivated meteor fire omitted its fire impact, burning child, native ground view or restore receipt.'
            }
        }
        if ($SkillId -eq 10062) {
            foreach ($file in @('acquired-water-vortex-checkpoint.json','acquired-water-vortex-strikes.json',
                'acquired-water-vortex-receipt.json','acquired-active-ui-vortex-formation.png',
                'acquired-active-ui-vortex-pull.png','acquired-active-ui-wet.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The cultivated water vortex evidence is incomplete.' }
            }
            $water = Get-Content (Join-Path $skillRun 'acquired-water-vortex-receipt.json') -Raw | ConvertFrom-Json
            if ($water.casts -lt 1 -or $water.hits -lt 1 -or $water.pulls -lt 1 -or $water.wet -lt 1 -or
                -not $water.moveCaptured -or -not $water.wetCaptured -or -not $water.restored -or
                -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10062 water damage, current inward movement, wet icon and restore passed.')) {
                throw 'The cultivated water vortex omitted its original current movement, wet icon or restore receipt.'
            }
        }
        if ($SkillId -eq 10057) {
            foreach ($file in @('acquired-wind-roar-checkpoint.json','acquired-wind-roar-strikes.json',
                'acquired-wind-roar-receipt.json','acquired-active-ui-airborne-launch.png','acquired-active-ui-airborne-recovery.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The cultivated wind roar evidence is incomplete.' }
            }
            $wind = Get-Content (Join-Path $skillRun 'acquired-wind-roar-receipt.json') -Raw | ConvertFrom-Json
            if ($wind.casts -lt 1 -or $wind.hits -lt 5 -or $wind.invokes -lt 1 -or -not $wind.launchCaptured -or
                -not $wind.recoveryCaptured -or -not $wind.geometryAdvanced -or
                -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10057 five wind hits, current airborne event chain, native animation and restore passed.')) {
                throw 'The cultivated wind roar omitted its original current airborne event, animated geometry or restore receipt.'
            }
        }
        if ($SkillId -eq 10037) {
            foreach ($file in @('acquired-divine-thunder-checkpoint.json','acquired-divine-thunder-strikes.json',
                'acquired-active-ui-thunder-hit-1.png','acquired-active-ui-thunder-hit-2.png',
                'acquired-active-ui-thunder-hit-3.png','acquired-active-ui-thunder-hit-4.png')) {
                if (-not (Test-Path -LiteralPath (Join-Path $skillRun $file))) { throw 'The cultivated divine thunder evidence is incomplete.' }
            }
            $thunder = Get-Content (Join-Path $skillRun 'acquired-divine-thunder-strikes.json') -Raw | ConvertFrom-Json
            $strikes = @($thunder.HitSelections | Where-Object { $_.HitEntityId -ge 10922 -and $_.HitEntityId -le 10925 })
            if ($strikes.Count -ne 16 -or -not (Select-String -LiteralPath $log -SimpleMatch '[Acquired active combat] 10037 sixteen lightning centres, minimum, light damage and restore passed.')) {
                throw 'The cultivated divine thunder lost its original strikes, minimum or restore receipt.'
            }
        }
        if ($SkillId -eq 10096 -and -not (Test-Path -LiteralPath (Join-Path $skillRun 'acquired-heavenly-rain-checkpoint.json'))) {
            throw 'The original cultivated healing checkpoint was not persisted.'
        }
        if ($SkillId -in @(10002,10032,10037,10039,10047,10056,10057,10059,10062,10063,10065,10068,10070,10073,10077,10079,10082,10083,10084,10085,10088,10089,10091,10092,10093,10095,10096,10097,10098,10099,10100,10101,10102,10103,10104)) {
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

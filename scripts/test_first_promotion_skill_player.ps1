param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [int[]]$ItemIds = @(),
    [ValidateSet('up','down','left','right')][string[]]$Directions = @('up','down','left','right'),
    [switch]$AirCombo,
    [switch]$ActionInvisible,
    [switch]$SummonLife,
    [switch]$DarkStatus,
    [switch]$AllStatusViews,
    [switch]$ColdStatusScene,
    [switch]$ColdSummonScene,
    [switch]$MoveScopeColdScene,
    [switch]$HitScopeColdScene,
    [switch]$PortableFlameLife,
    [switch]$SunsetSwordSound,
    [switch]$PortableFlameColdScene,
    [switch]$PortableFlameMovement,
    [switch]$PortableFlameReplacement,
    [switch]$PortableFlameAllyMovement,
    [switch]$PortableFlameEnemyEntry,
    [switch]$FighterHpSecrets
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = (Get-Item -LiteralPath $Player).FullName
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) {
    throw 'Use a session created by New-ZjcsValidationWorkspace.'
}
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation workspace repository.' }
$rules = Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/Official/Combat/rules.json') -Raw | ConvertFrom-Json
$scope = @($rules.profession_base | Where-Object Profession -in @(1,2,6,10,11,15) |
    ForEach-Object { $_.Skills; $_.FightUsedSkills }) | Sort-Object -Unique
$active = @($rules.item | Where-Object { $_.ClassId -in $scope -and $_.ItemType -eq 100 } |
    Select-Object -ExpandProperty ClassId | Sort-Object)
if ($scope.Count -ne 104 -or $active.Count -ne 57) { throw 'Review the original six-profession skill scope.' }
if (@($AirCombo,$ActionInvisible,$SummonLife,$DarkStatus,$MoveScopeColdScene,$HitScopeColdScene,$PortableFlameLife,$SunsetSwordSound,$FighterHpSecrets | Where-Object { $_ }).Count -gt 1) { throw 'Select one isolated status diagnostic.' }
if ($AllStatusViews -and -not $DarkStatus) { throw 'All original status views require the isolated dark-status host.' }
if ($ColdStatusScene -and (-not $DarkStatus -or $AllStatusViews)) { throw 'Cold status scene requires the isolated dark-status lifecycle without the all-view probe.' }
if ($ColdSummonScene -and (-not $SummonLife -or $ColdStatusScene)) { throw 'Cold summon scene requires the original summon lifespan diagnostic.' }
if ($PortableFlameColdScene -and -not $PortableFlameLife) { throw 'Cold fire-ring hosts require the isolated friendly fire-ring lifecycle.' }
if ($PortableFlameMovement -and (-not $PortableFlameLife -or $PortableFlameColdScene)) { throw 'Moving fire rings require the isolated lifecycle without a cold-host handoff.' }
if ($PortableFlameReplacement -and (-not $PortableFlameLife -or $PortableFlameColdScene -or $PortableFlameMovement)) { throw 'Replacing fire rings require their own two-caster lifecycle.' }
if ($PortableFlameAllyMovement -and -not $PortableFlameReplacement) { throw 'Ally-held movement requires the original two-caster lifecycle.' }
if ($PortableFlameEnemyEntry -and (-not $PortableFlameLife -or $PortableFlameMovement -or $PortableFlameReplacement -or $PortableFlameColdScene)) { throw 'Enemy entry requires its own original fire-ring lifecycle.' }
if ($ItemIds.Count -eq 0) { $ItemIds = if ($FighterHpSecrets) { @(10008) } elseif ($SunsetSwordSound) { @(10053) } elseif ($PortableFlameLife) { @(10061) } elseif ($AirCombo) { @(10006,10009,10052) } elseif ($ActionInvisible) { @(10025,10058) } elseif ($SummonLife) { @(10019,10025,10058) } elseif ($MoveScopeColdScene) { @(10029,10055) } elseif ($HitScopeColdScene) { @(10072) } elseif ($DarkStatus) { @(10029) } else { $active } }
if ($FighterHpSecrets -and @($ItemIds | Where-Object { $_ -ne 10008 }).Count -gt 0) {
    throw 'The combined HP-secret fixture requires the original warrior profile 10008.'
}
foreach ($item in $ItemIds) { if ($item -notin $active) { throw "Not an original active skill in scope: $item" } }
if ($AirCombo -and @($ItemIds | Where-Object { $_ -notin @(10006,10009,10052) }).Count -gt 0) {
    throw 'The airborne diagnostic requires original 10006/10009/10052.'
}
if ($ActionInvisible -and @($ItemIds | Where-Object { $_ -notin @(10025,10058) }).Count -gt 0) {
    throw 'The protected summon diagnostic requires original 10025/10058.'
}
if ($SummonLife -and @($ItemIds | Where-Object { $_ -notin @(10019,10025,10058) }).Count -gt 0) {
    throw 'The natural lifespan diagnostic requires original 10019/10025/10058.'
}
if ($DarkStatus -and @($ItemIds | Where-Object { $_ -ne 10029 }).Count -gt 0) {
    throw 'The dark-status diagnostic requires original 10029/10810.'
}
if ($MoveScopeColdScene -and (@($ItemIds | Where-Object { $_ -notin @(10029,10055) }).Count -gt 0 -or
    @($Directions | Where-Object { $_ -ne 'up' }).Count -gt 0)) {
    throw 'The movement-burning scene diagnostic currently requires original 10029/10055 facing up.'
}
if ($HitScopeColdScene -and @($ItemIds | Where-Object { $_ -ne 10072 }).Count -gt 0) {
    throw 'The fire-hit terrain scene diagnostic requires original 10072/11690.'
}
if ($PortableFlameLife -and (@($ItemIds | Where-Object { $_ -ne 10061 }).Count -gt 0 -or
    @($Directions | Where-Object { $_ -ne 'up' }).Count -gt 0)) {
    throw 'The controlled four-friendly fire-ring lifecycle requires original 10061 facing up.'
}
if ($SunsetSwordSound -and @($ItemIds | Where-Object { $_ -ne 10053 }).Count -gt 0) {
    throw 'The three-event sound diagnostic requires original 10053.'
}
$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository
    ZJCS_SKILLS_VALIDATE_SUMMON_ONLY = '1'
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'
    ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    ZJCS_FIGHTER_HP_SECRETS = $(if ($FighterHpSecrets) { '1' } else { $null })
    ZJCS_FIRST_PROMOTION_SKILL = ''
    ZJCS_DIRECTIONAL_SKILL_FACING = ''
    ZJCS_VALIDATION_DIR = ''
    ZJCS_AIR_COMBO_ACCEPTANCE = $(if ($AirCombo) { '1' } else { $null })
    ZJCS_ACTION_INVISIBLE_ACCEPTANCE = $(if ($ActionInvisible) { '1' } else { $null })
    ZJCS_SUMMON_LIFE_ACCEPTANCE = $(if ($SummonLife) { '1' } else { $null })
    ZJCS_STATUS_DARK_HIT_ACCEPTANCE = $(if ($DarkStatus) { '1' } else { [Environment]::GetEnvironmentVariable('ZJCS_STATUS_DARK_HIT_ACCEPTANCE', 'Process') })
    ZJCS_DARK_STATUS_LIFECYCLE_ACCEPTANCE = $(if ($DarkStatus) { '1' } else { $null })
    ZJCS_FIRST_PROMOTION_STATUS_VIEWS = $(if ($AllStatusViews) { '1' } else { $null })
    ZJCS_STATUS_COLD_SCENE_ACCEPTANCE = $(if ($ColdStatusScene) { '1' } else { $null })
    ZJCS_SUMMON_COLD_SCENE_ACCEPTANCE = $(if ($ColdSummonScene) { '1' } else { $null })
    ZJCS_STATUS_MOVE_SCOPE_ACCEPTANCE = $(if ($MoveScopeColdScene) { '1' } else { [Environment]::GetEnvironmentVariable('ZJCS_STATUS_MOVE_SCOPE_ACCEPTANCE', 'Process') })
    ZJCS_MOVE_SCOPE_COLD_SCENE = $(if ($MoveScopeColdScene) { '1' } else { $null })
    ZJCS_STATUS_HIT_SCOPE_ACCEPTANCE = $(if ($HitScopeColdScene) { '1' } else { [Environment]::GetEnvironmentVariable('ZJCS_STATUS_HIT_SCOPE_ACCEPTANCE', 'Process') })
    ZJCS_HIT_SCOPE_COLD_SCENE = $(if ($HitScopeColdScene) { '1' } else { $null })
    ZJCS_PORTABLE_FLAME_LIFECYCLE = $(if ($PortableFlameLife) { '1' } else { $null })
    ZJCS_PORTABLE_FLAME_COLD_SCENE = $(if ($PortableFlameColdScene) { '1' } else { $null })
    ZJCS_PORTABLE_FLAME_REPLACEMENT = $(if ($PortableFlameReplacement) { '1' } else { $null })
    ZJCS_PORTABLE_FLAME_ALLY_MOVEMENT = $(if ($PortableFlameAllyMovement) { '1' } else { $null })
    ZJCS_PORTABLE_FLAME_ENEMY_ENTRY = $(if ($PortableFlameEnemyEntry) { '1' } else { $null })
    ZJCS_PORTABLE_FLAME_MOVEMENT = $(if ($PortableFlameMovement) { '1' } else { $null })
    ZJCS_SUNSET_SWORD_SOUND = $(if ($SunsetSwordSound) { '1' } else { $null })
    ZJCS_STATUS_PROCESS_PHASE = $null
    ZJCS_STATUS_PROCESS_HANDOFF = $null
    ZJCS_SUMMON_DEATH_ACCEPTANCE = $(if ($ActionInvisible -or $SummonLife) { $null } else { [Environment]::GetEnvironmentVariable('ZJCS_SUMMON_DEATH_ACCEPTANCE', 'Process') })
}
$previous = @{}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    foreach ($item in $ItemIds) { foreach ($direction in $Directions) {
        $prefix = if ($PortableFlameEnemyEntry) { "portable-flame-enemy-entry-$direction-" } elseif ($PortableFlameAllyMovement) { "portable-flame-ally-movement-$direction-" } elseif ($PortableFlameReplacement) { "portable-flame-replacement-$direction-" } elseif ($PortableFlameMovement) { "portable-flame-movement-$direction-" } elseif ($PortableFlameColdScene) { "portable-flame-cold-$direction-" } elseif ($SunsetSwordSound) { "sunset-sword-sound-$direction-" } elseif ($PortableFlameLife) { "portable-flame-life-$direction-" } else { "native-$item-$direction-" }
        $run = Join-Path $workspacePath ($prefix + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run | Out-Null
        $env:ZJCS_FIRST_PROMOTION_SKILL = [string]$item
        $env:ZJCS_DIRECTIONAL_SKILL_FACING = $direction
        $env:ZJCS_VALIDATION_DIR = $run
        # The fixture mutes only its isolated save and injects input inside the Player.
        $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463','-muteAudio',
            '-logFile', ('"' + (Join-Path $run 'player.log') + '"'), '--zjcs-validate-skills', ('"' + $run + '"'))
        $process.PriorityClass = 'BelowNormal'
        while (-not $process.WaitForExit(30000)) { Write-Output "Running $item $direction : $run" }
        $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Player failed for $item $direction : $($result.error) ($run)" }
        if ($item -eq 10016) {
            & python -B (Join-Path $PSScriptRoot 'verify_lightning_dash_receipt.py') (Join-Path $run 'summon-final.json')
            if ($LASTEXITCODE -ne 0) { throw "Original 10016 dash/hit receipt validation failed: $run" }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Lightning dash sound] Original cast binding, native playback and completion passed.')) {
                throw "Original Skill_10016 native sound did not complete: $run"
            }
        }
        if ($item -eq 10026) {
            & python -B (Join-Path $PSScriptRoot 'verify_fire_slash_receipt.py') --direction $direction (Join-Path $run 'summon-final.json')
            if ($LASTEXITCODE -ne 0) { throw "Original 10026 directional damage receipt validation failed: $run" }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Fire slash sound] Original cast binding, native playback and completion passed.')) {
                throw "Original Skill_10026 native sound did not complete: $run"
            }
        }
        if ($item -eq 10042 -and -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Breaking brand] Original 11010/11011/11012 damage and 24052 application/source/checkpoint passed;')) {
            throw "Original 10042 damage/mark assertions did not complete: $run"
        }
        if ($item -eq 10053 -and -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Sunset sword] Original 11250/11251/10022 five timed hits passed;')) {
            throw "Original 10053 five-hit assertions did not complete: $run"
        }
        if ($item -eq 10061 -and -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Portable flame] Original 11200/24210 creator and 10860 proximity damage passed.')) {
            throw "Original 10061 proximity/creator assertions did not complete: $run"
        }
        if ($PortableFlameLife -and -not $PortableFlameReplacement) {
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Portable flame lifecycle] Four friendly original status views, disk rebind, two personal rounds and retirement passed.')) {
                throw "Original friendly fire-ring lifecycle assertions did not complete: $run"
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Portable flame sound] Original Skill_10061 native playback and completion passed.')) {
                throw "Original Skill_10061 native sound assertions did not complete: $run"
            }
            foreach ($frame in @('party','restored','owner-round-2','ended')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-$frame.png"))) {
                    throw "Original friendly fire-ring lifecycle frame $frame was not captured: $run"
                }
            }
        }
        if ($PortableFlameReplacement) {
            foreach ($message in @(
                $(if ($PortableFlameAllyMovement) { '[Portable flame ally movement] Two original old entities ended and the new caster''s one eligible acquisition hit passed.' } else { '[Portable flame replacement] Two original old entities ended, new creator/cultivation and two acquisition hits passed.' }),
                '[Portable flame replacement] Old native views retired, second original hero action, unique new views and disk rebind passed.',
                '[Portable flame replacement] New two-personal-round clocks and complete native retirement passed.',
                '[Portable flame replacement] Two original caster sounds completed without a restore replay.',
                '[Portable flame replacement] Original solo result resolved the cloned local appearance.')) {
                if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)) {
                    throw "Original fire-ring replacement did not complete: $message ($run)"
                }
            }
            foreach ($frame in @('first','before','after','restored','next-round','ended')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-replacement-$frame.png"))) {
                    throw "Original fire-ring replacement frame is missing: $frame ($run)"
                }
            }
        }
        if ($SunsetSwordSound -and -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Sunset sword sound] Original three native events, authored order/times and completion passed.')) {
            throw "Original Skill_10053 native sound assertions did not complete: $run"
        }
        if ($PortableFlameColdScene) {
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Portable flame cold scene] Destroyed home/battle hosts, ordinary weekly disk checkpoint, four new original bodies/statuses, identical state and no cast-sound replay passed.')) {
                throw "Original friendly fire-ring host restoration assertions did not complete: $run"
            }
            $before = Get-FileHash -LiteralPath (Join-Path $run 'portable-flame-cold-before.json') -Algorithm SHA256
            $after = Get-FileHash -LiteralPath (Join-Path $run 'portable-flame-cold-after.json') -Algorithm SHA256
            if ($before.Hash -cne $after.Hash) { throw "Friendly fire-ring host restoration changed the checkpoint: $run" }
            foreach ($frame in @('before','after')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-$frame-cold-scene.png"))) {
                    throw "Original friendly fire-ring host restoration frame $frame is missing: $run"
                }
            }
        }
        if ($PortableFlameEnemyEntry) {
            foreach ($message in @(
                '[Portable flame enemy entry] Original enemy 19810/19811 path and one stationary-caster 10860/10861/10862 hit passed.',
                '[Portable flame enemy entry] Native enemy body interpolation, unchanged holder and same visible status passed.',
                '[Portable flame enemy entry] Native caster-owned child began after the enemy reached the adjacent original cell.')) {
                if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)) {
                    throw "Original enemy-entry fire-ring flow did not finish: $message ($run)"
                }
            }
            foreach ($frame in @('before','during','after')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-entry-$frame-move.png"))) {
                    throw "Original enemy-entry frame is missing: $frame ($run)"
                }
            }
        }
        if ($PortableFlameAllyMovement) {
            foreach ($message in @(
                '[Portable flame ally movement] Original 15300/15301 approach and one other-caster 10860/10861/10862 hit passed.',
                '[Portable flame ally movement] Same native status, body interpolation and visible follow-bone arrival passed.',
                '[Portable flame ally movement] Other-caster native child began after the ally reached the original landing cell.')) {
                if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)) {
                    throw "Original ally-held movement did not finish: $message ($run)"
                }
            }
            foreach ($frame in @('before','during','after')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-ally-$frame-move.png"))) {
                    throw "Original ally-held fire-ring frame is missing: $frame ($run)"
                }
            }
        }
        if ($PortableFlameMovement) {
            foreach ($message in @('[Portable flame movement] Original ordinary path and single creator-owned 10860/10861/10862 damage passed;',
                '[Portable flame movement] Original 15300/15301 body interpolation, same live 24210 attachment and visible arrival passed.',
                '[Portable flame movement] Native proximity child began after the holder reached the original landing cell.')) {
                if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)) {
                    throw "Original moving fire-ring assertions did not complete: $message ($run)"
                }
            }
            foreach ($frame in @('before','during','after')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "portable-flame-$frame-move.png"))) {
                    throw "Original moving fire-ring frame is missing: $frame ($run)"
                }
            }
        }
        foreach ($phase in 1..4) {
            $frame = if ($FighterHpSecrets) { "fighter-hp-phase-$phase.png" } else { "first-promotion-$item-phase-$phase.png" }
            if (-not (Test-Path -LiteralPath (Join-Path $run $frame))) {
                throw "Original presentation phase $phase was not captured: $run"
            }
        }
        if ($FighterHpSecrets) {
            foreach ($message in @(
                '[Fighter HP secrets] Current-HP tiers, fatal prevention, original 10990/24082, cultivation and checkpoint passed.',
                '[Fighter HP secrets] Original recovery view, body animation and four rendered frames passed.')) {
                if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch $message)) {
                    throw "Combined HP-secret assertions did not complete: $message ($run)"
                }
            }
            if (-not (Test-Path -LiteralPath (Join-Path $run 'fighter-hp-checkpoint.json'))) {
                throw "Combined HP-secret checkpoint missing: $run"
            }
        }
        if ($AirCombo) {
            foreach ($phase in @('floating','paused','recovery','ended')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "air-combo-$item-$phase.png"))) {
                    throw "Original airborne phase $phase was not captured: $run"
                }
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Air combo] Original floating_01 mesh')) {
                throw "Original airborne lifecycle assertions did not complete: $run"
            }
        }
        if ($MoveScopeColdScene -or $HitScopeColdScene) {
            $terrainPrefix = if ($HitScopeColdScene) { 'hit-scope' } else { 'move-scope' }
            $terrainStatusId = if ($HitScopeColdScene) { 11582 } else { 11632 }
            if ($item -eq 10055 -and -not (Test-Path -LiteralPath (Join-Path $run 'move-scope-native-walking.png'))) {
                throw "Original walking action was not captured: $run"
            }
            foreach ($name in @("$terrainPrefix-before-cold-scene.png","$terrainPrefix-after-cold-scene.png",
                "$terrainPrefix-restored-expired.png","$terrainPrefix-cold-before.json","$terrainPrefix-cold-after.json")) {
                if (-not (Test-Path -LiteralPath (Join-Path $run $name))) { throw "Ground restoration proof missing: $name ($run)" }
            }
            if ((Get-FileHash -LiteralPath (Join-Path $run "$terrainPrefix-cold-before.json")).Hash -ne
                (Get-FileHash -LiteralPath (Join-Path $run "$terrainPrefix-cold-after.json")).Hash -or
                -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch "[$terrainPrefix cold scene] Original 3320/$terrainStatusId")) {
                throw "Original ground scene restoration or native lifetime assertions did not finish: $run"
            }
        }
        if ($ActionInvisible) {
            foreach ($phase in @('protected','disabled','reenabled','removed')) {
                foreach ($extension in @('png','json')) {
                    if (-not (Test-Path -LiteralPath (Join-Path $run "action-invisible-$item-$phase.$extension"))) {
                        throw "Original protection phase $phase was not captured/persisted: $run"
                    }
                }
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Action invisible] Actual original enemy AI/59460 graph')) {
                throw "Original protection battle assertions did not complete: $run"
            }
        }
        if ($SummonLife) {
            $expectedUnits = if ($item -eq 10058) { 2 } else { 1 }
            foreach ($phase in @('born','last-round','final-cast','final-frame','retired','post-retirement')) {
                foreach ($extension in @('png','json')) {
                    if (@(Get-ChildItem -LiteralPath $run -Filter "summon-life-$item-$phase-*.$extension").Count -ne $expectedUnits) {
                        throw "Original lifespan phase $phase was not captured/persisted for every summon: $run"
                    }
                }
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Summon lifespan] Original applicator turns')) {
                throw "Original summon lifespan battle assertions did not complete: $run"
            }
        }
        if ($ColdSummonScene) {
            foreach ($name in @('summon-before-cold-scene.png','summon-after-cold-scene.png',
                'summon-cold-scene-before.json','summon-cold-scene-after.json')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run $name))) { throw "Cold summon scene proof missing: $name ($run)" }
            }
            if ((Get-FileHash -LiteralPath (Join-Path $run 'summon-cold-scene-before.json')).Hash -ne
                (Get-FileHash -LiteralPath (Join-Path $run 'summon-cold-scene-after.json')).Hash) {
                throw "Restored summon checkpoint changed: $run"
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Summon cold scene] Original weekly disk checkpoint')) {
                throw "Original summon scene restoration did not finish: $run"
            }
        }
        if ($DarkStatus) {
            foreach ($phase in @('count-3','count-2','count-1','restored','ended')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "dark-status-$phase.png"))) {
                    throw "Original dark-status phase was not captured: $phase ($run)"
                }
            }
            if (-not (Test-Path -LiteralPath (Join-Path $run 'dark-status-native-restore.json')) -or
                -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Dark status] Original 24222 body particles')) {
                throw "Original dark-status lifecycle/disk assertions did not complete: $run"
            }
        }
        if ($ColdStatusScene) {
            foreach ($name in @('dark-status-before-cold-scene.png','dark-status-after-cold-scene.png',
                'dark-status-cold-scene-before.json','dark-status-cold-scene-after.json')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run $name))) { throw "Cold status scene proof missing: $name ($run)" }
            }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Dark status cold scene] Original weekly disk checkpoint')) {
                throw "Original dark-status cold scene did not finish: $run"
            }
        }
        if ($AllStatusViews) {
            $catalog = Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json') -Raw | ConvertFrom-Json
            $characterStatuses = @($catalog.graphs | Where-Object id -notin @(11582,11632,24269,24270))
            if ($catalog.graphs.Count -ne 83 -or $characterStatuses.Count -ne 79) { throw 'Review the original status host scope.' }
            foreach ($status in $characterStatuses) { foreach ($phase in @('start','update')) {
                if (-not (Test-Path -LiteralPath (Join-Path $run "original-status-$($status.id)-$phase.png"))) {
                    throw "Original character-status view was not captured: $($status.id) $phase ($run)"
                }
            } }
            if (-not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Status views] All 83 original prefab hierarchies bound; 79 character statuses passed native Start/Update/End.')) {
                throw "Original status view assertions did not finish: $run"
            }
        }
        Write-Output "Passed $item $direction : $run"
    } }
}
finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
# Keep each run until its actual screenshots have been reviewed; workspace cleanup is caller-owned.

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
    [switch]$ColdStatusScene
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
if (@($AirCombo,$ActionInvisible,$SummonLife,$DarkStatus | Where-Object { $_ }).Count -gt 1) { throw 'Select one isolated status diagnostic.' }
if ($AllStatusViews -and -not $DarkStatus) { throw 'All original status views require the isolated dark-status host.' }
if ($ColdStatusScene -and (-not $DarkStatus -or $AllStatusViews)) { throw 'Cold status scene requires the isolated dark-status lifecycle without the all-view probe.' }
if ($ItemIds.Count -eq 0) { $ItemIds = if ($AirCombo) { @(10006,10009,10052) } elseif ($ActionInvisible) { @(10025,10058) } elseif ($SummonLife) { @(10019,10025,10058) } elseif ($DarkStatus) { @(10029) } else { $active } }
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
$settings = @{
    ZJCS_REPOSITORY_ROOT = $repository
    ZJCS_SKILLS_VALIDATE_SUMMON_ONLY = '1'
    ZJCS_GRAPH_CAPABILITY_ACCEPTANCE = '10600'
    ZJCS_BATTLE_INPUT_AUTOMATION = '1'
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
        $run = Join-Path $workspacePath ("native-$item-$direction-" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $run | Out-Null
        $env:ZJCS_FIRST_PROMOTION_SKILL = [string]$item
        $env:ZJCS_DIRECTIONAL_SKILL_FACING = $direction
        $env:ZJCS_VALIDATION_DIR = $run
        # The fixture mutes only its isolated save and injects input inside the Player.
        $process = Start-UnityOnInternalDisplay -Editor $executable -ArgumentList @(
            '-screen-fullscreen','0','-screen-width','675','-screen-height','1463',
            '-logFile', ('"' + (Join-Path $run 'player.log') + '"'), '--zjcs-validate-skills', ('"' + $run + '"'))
        $process.PriorityClass = 'BelowNormal'
        while (-not $process.WaitForExit(30000)) { Write-Output "Running $item $direction : $run" }
        $result = Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Player failed for $item $direction : $($result.error) ($run)" }
        foreach ($phase in 1..4) {
            if (-not (Test-Path -LiteralPath (Join-Path $run "first-promotion-$item-phase-$phase.png"))) {
                throw "Original presentation phase $phase was not captured: $run"
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

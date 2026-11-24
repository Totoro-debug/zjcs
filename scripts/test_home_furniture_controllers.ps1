param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [switch]$AllWeapons
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = [IO.Path]::GetFullPath($ProjectPath)
$validationBase = [IO.Path]::GetFullPath((Join-Path $repository 'output/validation'))
if (-not $project.StartsWith($validationBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Select an existing independent Unity project below output/validation; the shared main project is forbidden.'
}
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
})
if ($running.Count -ne 0) { throw 'Do not stage files into a running Unity project.' }
$work = Join-Path $validationBase ('home-furniture-controllers-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$source = Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeFurniture'
$destination = Join-Path $project 'Assets/Game/Runtime/Official/HomeFurniture'
Copy-Item -LiteralPath $source -Destination (Split-Path $destination) -Recurse -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force
$nativeSpine = Join-Path $repository 'unity/Assets/Game/Runtime/Offline/NativeSpine'
Copy-Item -LiteralPath $nativeSpine -Destination (Join-Path $project 'Assets/Game/Runtime/Offline') -Recurse -Force
Copy-Item -LiteralPath ($nativeSpine + '.meta') -Destination (Join-Path $project 'Assets/Game/Runtime/Offline/NativeSpine.meta') -Force
$fixture = Join-Path $project 'Assets/Editor/HomeFurnitureControllerValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_furniture_controller_validation.cs') -Destination (Join-Path $fixture 'HomeFurnitureControllerValidation.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_weapon_rack_controller_validation.cs') -Destination (Join-Path $fixture 'HomeWeaponRackControllerValidation.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_native_weapon_validation.cs') -Destination (Join-Path $fixture 'HomeNativeWeaponValidation.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_native_weapon_controller_validation.cs') -Destination (Join-Path $fixture 'HomeNativeWeaponControllerValidation.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_native_weapon_catalog_validation.cs') -Destination (Join-Path $fixture 'HomeNativeWeaponCatalogValidation.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_appearance_availability_validation.cs') -Destination (Join-Path $fixture 'HomeAppearanceAvailabilityValidation.cs') -Force
foreach ($relative in @(
    'Game/Runtime/Offline/Progression/OfflineAppearanceAvailability.cs',
    'Game/Runtime/Offline/Progression/OfflineAppearanceAvailability.cs.meta',
    'Game/Runtime/Offline/Progression/OfflineWardrobeRules.cs',
    'Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs',
    'Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs',
    'Game/Runtime/Official/PartnersUI/Roster/TipDots/TipDotNodeManager.cs',
    'Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotRuntime.cs',
    'Game/Runtime/Offline/PartnersUI/PartnerDialoguePlaybackView.cs',
    'Game/Runtime/Presentation/Character/OfficialWardrobePanelView.cs',
    'Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
    'Game/Runtime/Presentation/Home/HomeAssetCatalog.cs',
    'Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
    'Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
    'Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStories.cs',
    'Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStories.cs.meta',
    'Game/Runtime/Presentation/Home/HomeHudView.cs',
    'Game/Runtime/Presentation/Home/HomeHudView.Partners.cs',
    'Game/Runtime/Presentation/Home/HomeHudView.Repair.cs',
    'Game/Runtime/Presentation/Home/HomeHudView.Tutorial.cs',
    'Game/Runtime/Presentation/Home/HomeSceneView.cs'
)) {
    $src = Join-Path $repository ('unity/Assets/' + $relative)
    $dst = Join-Path $project ('Assets/' + $relative)
    [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
    Copy-Item -LiteralPath $src -Destination $dst -Force
}
$rack = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-storage-weapon-rack-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $rack.outputs.PSObject.Properties) {
    $inputPath = Join-Path $repository $entry.Name
    if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Bound rack resource differs: ' + $entry.Name)
    }
    $relative = $entry.Name.Substring('unity/'.Length)
    $target = Join-Path $project $relative
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $inputPath -Destination $target -Force
}
$weapon = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-weapon-parts-source-manifest.json') -Raw | ConvertFrom-Json
if ($AllWeapons) {
    $selectionPath = @($weapon.inputs.PSObject.Properties.Name | Where-Object { $_.EndsWith('/home-weapon-rack/selection.json') })
    if ($selectionPath.Count -ne 1) { throw 'Resolve the exact official Home weapon selection input.' }
    $selection = Get-Content -LiteralPath (Join-Path $repository $selectionPath[0]) -Raw | ConvertFrom-Json
    if ($selection.manifest -ne $weapon.manifest -or
        (Compare-Object @($selection.weapons.path | Sort-Object) @($weapon.selections.asset.path | Sort-Object))) {
        throw 'The installed native weapon catalog is not the complete selected official catalog.'
    }
    $env:ZJCS_FURNITURE_EXPECTED_PREFABS = [string]$selection.weapons.Count
}
foreach ($selection in $weapon.selections) {
    $relative = 'unity/Assets/Official/Home/WeaponParts/' + $selection.asset.path.Substring('Assets/'.Length)
    if (-not $weapon.outputs.PSObject.Properties[$relative] -or -not (Test-Path -LiteralPath (Join-Path $repository $relative))) {
        throw ('Recovered native Home weapon prefab is not installed: ' + $relative)
    }
}
foreach ($entry in $weapon.outputs.PSObject.Properties) {
    $inputPath = Join-Path $repository $entry.Name
    if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Bound native weapon resource differs: ' + $entry.Name)
    }
    $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $inputPath -Destination $target -Force
}
@{ name = 'HomeFurnitureControllerValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput',
    'Zjcs.Official.HomeMainUiControls', 'Zjcs.Offline.HomeSceneInput', 'Zjcs.Official.WorldUI',
    'Zjcs.Official.ProfessionUI', 'Unity.TextMeshPro', 'Unity.ugui', 'Unity.RenderPipelines.Universal.Runtime',
    'Zjcs.Offline.NativeSpine', 'spine-unity', 'spine-csharp'
    'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Presentation.Character'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeFurnitureControllerValidation.asmdef') -Encoding utf8
$env:ZJCS_FURNITURE_PROJECT = $project
$env:ZJCS_FURNITURE_VALIDATION_DIR = $work
$env:ZJCS_FURNITURE_ALL_WEAPONS = if ($AllWeapons) { '1' } else { '0' }
$env:ZJCS_FURNITURE_WEAPON_MANIFEST = Join-Path $repository 'assets/reference/home-weapon-parts-source-manifest.json'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeFurnitureControllerValidation.Run',
    '-logFile', ('"' + $log + '"')
)
Write-Output ('Furniture controller log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or
    -not (Select-String -LiteralPath $log -Pattern '^HOME_FURNITURE_CONTROLLERS_PASSED:' -Quiet) -or
    -not (Select-String -LiteralPath $log -Pattern '^HOME_NATIVE_WEAPON_PASSED:' -Quiet) -or
    -not (Select-String -LiteralPath $log -Pattern '^HOME_NATIVE_WEAPON_CONTROLLERS_PASSED:' -Quiet)) {
    throw ('Furniture controller runtime validation failed: ' + $log)
}
if (-not (Select-String -LiteralPath $log -Pattern '^HOME_APPEARANCE_AVAILABILITY_PASSED:' -Quiet)) {
    throw ('Closed appearance selection validation failed: ' + $log)
}
if ($AllWeapons -and -not (Select-String -LiteralPath $log -Pattern '^HOME_NATIVE_WEAPON_CATALOG_PASSED:' -Quiet)) {
    throw ('Full native Home weapon catalog validation failed: ' + $log)
}
Write-Output 'HOME_FURNITURE_CONTROLLERS_PASSED'

param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe'
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
    'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput',
    'Zjcs.Official.HomeMainUiControls', 'Zjcs.Offline.HomeSceneInput', 'Zjcs.Official.WorldUI',
    'Zjcs.Official.ProfessionUI', 'Unity.TextMeshPro', 'Unity.ugui', 'Unity.RenderPipelines.Universal.Runtime',
    'Zjcs.Offline.NativeSpine', 'spine-unity', 'spine-csharp'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeFurnitureControllerValidation.asmdef') -Encoding utf8
$env:ZJCS_FURNITURE_PROJECT = $project
$env:ZJCS_FURNITURE_VALIDATION_DIR = $work
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
Write-Output 'HOME_FURNITURE_CONTROLLERS_PASSED'

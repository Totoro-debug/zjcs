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
    throw 'Select an independent Unity source project below output/validation; the main project is forbidden.'
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('home-snow-runtime-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
foreach ($receiptName in @('home-weather-snow-source-manifest.json', 'home-snow-effect-native-source-manifest.json')) {
    $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $receiptName)) -Raw | ConvertFrom-Json
    foreach ($section in @('inputs', 'validation_inputs', 'runtime_bindings')) {
        foreach ($entry in $receipt.$section.PSObject.Properties) {
            $source = Join-Path $repository $entry.Name
            if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
                throw ('Snow source identity differs (' + $receiptName + '): ' + $entry.Name)
            }
        }
    }
    foreach ($entry in $receipt.outputs.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Native snow source differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
foreach ($relative in @(
    'Assets/Game/Runtime/Presentation/Home/HomeNativeSnowEffectView.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeNativeSnowEffectView.cs.meta',
    'Assets/Game/Runtime/Presentation/Home/HomeNativeSceneSlots.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs',
    'Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef'
)) {
    Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $relative)) -Destination (Join-Path $project $relative) -Force
}
# This shared source can contain other sessions' unfinished work. Validate its
# committed/staged source, including only explicitly staged changes to this module.
foreach ($sharedPath in @(
    'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
    'unity/Assets/Game/Runtime/Official/HomeFurniture/HomeFurnitureServices.cs'
)) {
    $sharedSource = & git -C $repository show (':' + $sharedPath)
    if ($LASTEXITCODE -ne 0) { throw ('Cannot read committed/staged Home source: ' + $sharedPath) }
    [IO.File]::WriteAllText((Join-Path $project $sharedPath.Substring('unity/'.Length)),
        (($sharedSource -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
}
$furniturePath = 'unity/Assets/Game/Runtime/Official/HomeFurniture/'
$indexedFurniture = @(& git -C $repository ls-files -- ($furniturePath + '*.cs'))
if ($LASTEXITCODE -ne 0) { throw 'Cannot list committed/staged furniture sources.' }
$furnitureDirectory = Join-Path $project $furniturePath.Substring('unity/'.Length)
foreach ($extra in Get-ChildItem -LiteralPath $furnitureDirectory -Filter '*.cs') {
    if ($indexedFurniture -contains ($furniturePath + $extra.Name)) { continue }
    $preserved = Join-Path $work 'concurrent-source'
    [void](New-Item -ItemType Directory -Path $preserved -Force)
    Move-Item -LiteralPath $extra.FullName -Destination (Join-Path $preserved $extra.Name)
    if (Test-Path -LiteralPath ($extra.FullName + '.meta')) {
        Move-Item -LiteralPath ($extra.FullName + '.meta') -Destination (Join-Path $preserved ($extra.Name + '.meta'))
    }
}
$fixture = Join-Path $project 'Assets/Editor/HomeSnowRuntimeValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_snow_runtime_validation.cs') -Destination (Join-Path $fixture 'HomeSnowRuntimeValidation.cs') -Force
@{ name = 'HomeSnowRuntimeValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Presentation.Home', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.Home', 'Zjcs.Offline.Progression',
    'Zjcs.Official.HomeEntityCreation', 'Zjcs.Official.HomeEnvironmentTime', 'Zjcs.Official.HomeFurniture',
    'Zjcs.Official.HomeWeather', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput',
    'Zjcs.Official.HomeMainUiControls'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeSnowRuntimeValidation.asmdef') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$previous = $env:ZJCS_SNOW_RUNTIME_VALIDATION
try {
    $env:ZJCS_SNOW_RUNTIME_VALIDATION = $work
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode', '-muteAudio', '-job-worker-count', '2', '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'HomeSnowRuntimeValidation.Run', '-logFile', ('"' + $log + '"')
    )
} finally { $env:ZJCS_SNOW_RUNTIME_VALIDATION = $previous }
Write-Output ('Home snow runtime PID ' + $process.Id + ', log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_SNOW_RUNTIME_PASSED:' -Quiet)) {
    throw ('Original snow Home runtime validation failed: ' + $log)
}
Write-Output ('HOME_SNOW_RUNTIME_PASSED: ' + $work)

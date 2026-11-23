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
    throw 'Select an existing independent project below output/validation; the shared main project is forbidden.'
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('home-native-prefab-resources-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receiptPath = Join-Path $repository 'assets/reference/home-native-prefab-references-source-manifest.json'
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
Copy-Item -LiteralPath $receiptPath -Destination (Join-Path $work 'routes.json')
$files = @{}; foreach ($entry in $receipt.outputs.PSObject.Properties) { $files[$entry.Name] = $entry.Value }
foreach ($entry in $receipt.inputs.PSObject.Properties) {
    if ($entry.Name.StartsWith('unity/') -and ($entry.Name.EndsWith('.prefab') -or $entry.Name.EndsWith('.prefab.meta'))) { $files[$entry.Name] = $entry.Value }
}
foreach ($name in $files.Keys) {
    $source = Join-Path $repository $name
    if (-not $name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $files[$name]) {
        throw ('Home native prefab input differs: ' + $name)
    }
    $target = Join-Path $project $name.Substring('unity/'.Length)
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $files[$name]) { continue }
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$fixture = Join-Path $project 'Assets/Editor/HomeNativePrefabResourcesValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_native_prefab_resources_validation.cs') -Destination (Join-Path $fixture 'HomeNativePrefabResourcesValidation.cs') -Force
@{ name = 'HomeNativePrefabResourcesValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Offline.HomeNativeAssets', 'Zjcs.Official.HomeEntityCreation', 'Zjcs.Official.HomeFurniture',
    'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeModelEnvironment', 'Zjcs.Official.HomeSceneInput'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeNativePrefabResourcesValidation.asmdef') -Encoding utf8
$env:ZJCS_HOME_NATIVE_RESOURCE_PROJECT = $project
$env:ZJCS_HOME_NATIVE_RESOURCE_INPUT = Join-Path $work 'routes.json'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeNativePrefabResourcesValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Home native prefab resource log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_NATIVE_PREFAB_RESOURCES_PASSED:' -Quiet)) {
    throw ('Local native prefab resource validation failed: ' + $log)
}
Write-Output 'HOME_NATIVE_PREFAB_RESOURCES_PASSED'

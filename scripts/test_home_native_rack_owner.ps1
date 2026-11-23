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
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('home-native-rack-owner-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
# Reuse the selected, already migrated inputs; never select a newer source for
# an existing payload or start/import the shared main Unity project.
foreach ($name in @('home-furniture-controller', 'home-weapon-parts', 'home-storage-native', 'home-native-prefab-references')) {
    $path = Join-Path $repository ('assets/reference/' + $name + '-source-manifest.json')
    $receipt = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    foreach ($entry in $receipt.outputs.PSObject.Properties) {
        if (-not $entry.Name.StartsWith('unity/')) { continue }
        $source = Join-Path $repository $entry.Name
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Verified native rack input differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$probe = Join-Path $project 'Assets/Game/Validation/HomeNativeRackOwner'
[void](New-Item -ItemType Directory -Path $probe -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_native_factory_probe.cs') -Destination (Join-Path $probe 'HomeNativeFactoryProbe.cs') -Force
@{ name = 'Zjcs.Validation.HomeNativeRackProbe'; references = @() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $probe 'Zjcs.Validation.HomeNativeRackProbe.asmdef') -Encoding utf8
$fixture = Join-Path $project 'Assets/Editor/HomeNativeRackOwnerValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_native_rack_owner_validation.cs') -Destination (Join-Path $fixture 'HomeNativeRackOwnerValidation.cs') -Force
@{ name = 'HomeNativeRackOwnerValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Offline.HomeNativeAssets', 'Zjcs.Offline.NativeSpine', 'Zjcs.Official.HomeFurniture',
    'Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeEntityCreation', 'Zjcs.Validation.HomeNativeRackProbe',
    'spine-unity', 'spine-csharp', 'Unity.RenderPipelines.Universal.Runtime'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeNativeRackOwnerValidation.asmdef') -Encoding utf8
$env:ZJCS_HOME_NATIVE_RACK_PROJECT = $project
$env:ZJCS_HOME_NATIVE_RACK_OUTPUT = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeNativeRackOwnerValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Home native rack owner log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_NATIVE_RACK_OWNER_PASSED:' -Quiet)) {
    throw ('Native rack owner validation failed: ' + $log)
}
Write-Output 'HOME_NATIVE_RACK_OWNER_PASSED'

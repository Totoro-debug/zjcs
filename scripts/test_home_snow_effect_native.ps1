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
    throw 'Select an independent source project below output/validation; the shared main project is forbidden.'
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('home-snow-effect-render-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
foreach ($receiptName in @('home-weather-snow-source-manifest.json', 'home-snow-effect-native-source-manifest.json')) {
    $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $receiptName)) -Raw | ConvertFrom-Json
    foreach ($entry in $receipt.inputs.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $repository $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Original snow input differs: ' + $entry.Name)
        }
    }
    foreach ($entry in $receipt.validation_inputs.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $repository $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Snow validation source differs: ' + $entry.Name)
        }
    }
    foreach ($entry in $receipt.outputs.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Native snow payload differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture = Join-Path $project 'Assets/Editor/HomeSnowEffectNativeValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_snow_effect_native_validation.cs') -Destination (Join-Path $fixture 'HomeSnowEffectNativeValidation.cs') -Force
@{ name = 'HomeSnowEffectNativeValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.HomeWeather', 'Zjcs.Official.HomeEnvironmentTime', 'Zjcs.Official.HomeEntityCreation',
    'Zjcs.Official.HomeFurniture', 'Zjcs.Offline.HomeNativeAssets'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeSnowEffectNativeValidation.asmdef') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$previous = $env:ZJCS_SNOW_EFFECT_VALIDATION
try {
    $env:ZJCS_SNOW_EFFECT_VALIDATION = $work
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode', '-muteAudio', '-job-worker-count', '2', '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'HomeSnowEffectNativeValidation.Run', '-logFile', ('"' + $log + '"')
    )
} finally { $env:ZJCS_SNOW_EFFECT_VALIDATION = $previous }
Write-Output ('Native snow validation PID ' + $process.Id + ', log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_SNOW_EFFECT_NATIVE_PASSED' -Quiet)) {
    throw ('Original snow native render validation failed: ' + $log)
}
Write-Output 'HOME_SNOW_EFFECT_NATIVE_PASSED'

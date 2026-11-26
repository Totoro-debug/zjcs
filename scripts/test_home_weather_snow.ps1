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
    throw 'Select an existing independent source project below output/validation; the shared main project is forbidden.'
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('home-weather-snow-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-weather-snow-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $receipt.outputs.PSObject.Properties) {
    $source = Join-Path $repository $entry.Name
    if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Original snow controller input differs: ' + $entry.Name)
    }
    $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$fixture = Join-Path $project 'Assets/Editor/HomeWeatherSnowValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_weather_snow_validation.cs') -Destination (Join-Path $fixture 'HomeWeatherSnowValidation.cs') -Force
@{ name = 'HomeWeatherSnowValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.HomeWeather', 'Zjcs.Official.HomeEnvironmentTime', 'Zjcs.Official.HomeEntityCreation',
    'Zjcs.Official.HomeFurniture'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeWeatherSnowValidation.asmdef') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'HomeWeatherSnowValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Home weather snow validation PID ' + $process.Id + ', log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_WEATHER_SNOW_PASSED' -Quiet)) {
    throw ('Original snow lifecycle validation failed: ' + $log)
}
Write-Output 'HOME_WEATHER_SNOW_PASSED'

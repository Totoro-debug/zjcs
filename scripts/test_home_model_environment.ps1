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
$work = Join-Path $validationBase ('home-model-environment-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
foreach ($name in @('home-model-environment', 'home-house-native', 'home-relations', 'home-entity-runtime', 'home-entity-creation', 'home-entity-bindings')) {
    $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $name + '-source-manifest.json')) -Raw | ConvertFrom-Json
    foreach ($entry in $receipt.outputs.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Home model dependency differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture = Join-Path $project 'Assets/Editor/HomeModelEnvironmentValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_model_environment_validation.cs') -Destination (Join-Path $fixture 'HomeModelEnvironmentValidation.cs') -Force
@{ name = 'HomeModelEnvironmentValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.HomeModelEnvironment', 'Zjcs.Official.HomeEnvironmentTime', 'Zjcs.Official.HomeRelations',
    'Zjcs.Official.HomeEntityRuntime', 'Zjcs.Official.HomeEntityBindings', 'Zjcs.Official.HomeEntityCreation',
    'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI', 'Unity.RenderPipelines.Universal.Runtime', 'spine-unity', 'spine-csharp'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeModelEnvironmentValidation.asmdef') -Encoding utf8
$env:ZJCS_HOME_MODEL_ENVIRONMENT_PROJECT = $project
$env:ZJCS_HOME_MODEL_ENVIRONMENT_OUTPUT = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeModelEnvironmentValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Home model environment log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_MODEL_ENVIRONMENT_PASSED:' -Quiet)) {
    throw ('Original home model environment validation failed: ' + $log)
}
Write-Output 'HOME_MODEL_ENVIRONMENT_PASSED'

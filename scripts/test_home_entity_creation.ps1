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
$work = Join-Path $validationBase ('home-entity-creation-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
foreach ($name in @('home-entity-creation', 'home-storage-native', 'home-entity-bindings', 'home-furniture-controller', 'bag-tip-dots')) {
    $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $name + '-source-manifest.json')) -Raw | ConvertFrom-Json
    foreach ($entry in $receipt.outputs.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Creator dependency differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture = Join-Path $project 'Assets/Editor/HomeEntityCreationValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_entity_creation_validation.cs') -Destination (Join-Path $fixture 'HomeEntityCreationValidation.cs') -Force
@{ name = 'HomeEntityCreationValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.HomeEntityCreation', 'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeEntityCreationValidation.asmdef') -Encoding utf8
$env:ZJCS_ENTITY_CREATION_PROJECT = $project
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeEntityCreationValidation.Run',
    '-logFile', ('"' + $log + '"')
)
Write-Output ('Home entity creator log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_ENTITY_CREATION_PASSED:' -Quiet)) {
    throw ('Original home entity creation validation failed: ' + $log)
}
Write-Output 'HOME_ENTITY_CREATION_PASSED'

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
    throw 'Select an independent Unity project below output/validation; the shared main project is forbidden.'
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('bag-item-query-bindings-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/bag-item-query-bindings-source-manifest.json') -Raw | ConvertFrom-Json
$files = @{}
foreach ($group in @($receipt.inputs, $receipt.outputs)) {
    foreach ($entry in $group.PSObject.Properties) { if ($entry.Name.StartsWith('unity/')) { $files[$entry.Name] = $entry.Value } }
}
foreach ($name in $files.Keys) {
    $source = Join-Path $repository $name
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $files[$name]) { throw ('Verified Bag binding input differs: ' + $name) }
    $target = Join-Path $project $name.Substring('unity/'.Length)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$fixture = Join-Path $project 'Assets/Editor/BagItemQueryBindingsValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/bag_item_query_bindings_validation.cs') -Destination (Join-Path $fixture 'BagItemQueryBindingsValidation.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_storage_pointer_validation.cs') -Destination (Join-Path $fixture 'HomeStoragePointerValidation.cs') -Force
@{ name = 'BagItemQueryBindingsValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Offline.BagItemQueries', 'Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagTipDots',
    'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Offline.Equipment', 'Zjcs.Offline.CharacterCreation',
    'Zjcs.Offline.Cooking', 'Zjcs.Offline.Commerce', 'Zjcs.Presentation.Home', 'Zjcs.Presentation.Inventory',
    'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput', 'Zjcs.Official.HomeMainUiControls', 'Unity.ugui'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'BagItemQueryBindingsValidation.asmdef') -Encoding utf8
$env:ZJCS_BAG_BINDINGS_PROJECT = $project
$env:ZJCS_BAG_BINDINGS_OUTPUT = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'BagItemQueryBindingsValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Local Bag query binding log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^BAG_ITEM_QUERY_BINDINGS_PASSED:' -Quiet)) {
    throw ('Local Bag query binding validation failed: ' + $log)
}
Write-Output 'BAG_ITEM_QUERY_BINDINGS_PASSED'

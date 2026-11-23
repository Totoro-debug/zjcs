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
$work = Join-Path $validationBase ('bag-recycle-queries-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/bag-recycle-queries-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($group in @($receipt.inputs, $receipt.outputs, $receipt.validation_inputs)) {
    foreach ($entry in $group.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Selected recycling input differs: ' + $entry.Name)
        }
        if (-not $entry.Name.StartsWith('unity/')) { continue }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture = Join-Path $project 'Assets/Editor/BagRecycleQueriesValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/bag_recycle_queries_validation.cs') -Destination (Join-Path $fixture 'BagRecycleQueriesValidation.cs') -Force
@{ name = 'BagRecycleQueriesValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.BagRecycleQueries', 'Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagTipDots',
    'Zjcs.Offline.BagRecycleQueries', 'Zjcs.Offline.BagItemQueries', 'Zjcs.Offline.Progression',
    'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.State'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'BagRecycleQueriesValidation.asmdef') -Encoding utf8
$fixtureMetadata = @{
    ($fixture + '.meta') = @('folder', 'DefaultImporter')
    (Join-Path $fixture 'BagRecycleQueriesValidation.cs.meta') = @('script', 'MonoImporter')
    (Join-Path $fixture 'BagRecycleQueriesValidation.asmdef.meta') = @('assembly', 'AssemblyDefinitionImporter')
}
foreach ($path in $fixtureMetadata.Keys) {
    if (Test-Path -LiteralPath $path) { continue }
    $entry = $fixtureMetadata[$path]
    $md5 = [Security.Cryptography.MD5]::Create()
    try { $guid = ([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('zjcs:BagRecycleQueriesValidation:' + $entry[0])))).Replace('-', '').ToLowerInvariant() }
    finally { $md5.Dispose() }
    $folder = if ($entry[0] -eq 'folder') { "folderAsset: yes`n" } else { '' }
    [IO.File]::WriteAllText($path, "fileFormatVersion: 2`nguid: $guid`n$folder$($entry[1]):`n  externalObjects: {}`n", [Text.UTF8Encoding]::new($false))
}
$env:ZJCS_BAG_RECYCLE_PROJECT = $project
$env:ZJCS_BAG_RECYCLE_OUTPUT = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'BagRecycleQueriesValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Original recycling query log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^BAG_RECYCLE_QUERIES_PASSED:' -Quiet)) {
    throw ('Original recycling query validation failed: ' + $log)
}
Write-Output 'BAG_RECYCLE_QUERIES_PASSED'

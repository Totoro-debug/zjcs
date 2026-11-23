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
$work = Join-Path $validationBase ('bag-item-queries-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/bag-item-queries-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $receipt.outputs.PSObject.Properties) {
    $source = Join-Path $repository $entry.Name
    if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Selected Bag query input differs: ' + $entry.Name)
    }
    $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$fixture = Join-Path $project 'Assets/Editor/BagItemQueriesValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/bag_item_queries_validation.cs') -Destination (Join-Path $fixture 'BagItemQueriesValidation.cs') -Force
@{ name = 'BagItemQueriesValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagTipDots'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'BagItemQueriesValidation.asmdef') -Encoding utf8
$env:ZJCS_BAG_QUERY_PROJECT = $project
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'BagItemQueriesValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Original Bag query log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^BAG_ITEM_QUERIES_PASSED:' -Quiet)) {
    throw ('Original Bag query validation failed: ' + $log)
}
Write-Output 'BAG_ITEM_QUERIES_PASSED'

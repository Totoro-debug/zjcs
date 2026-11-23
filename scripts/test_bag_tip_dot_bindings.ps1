param([Parameter(Mandatory)][string]$ProjectPath, [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe')
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
$work = Join-Path $validationBase ('bag-tip-dot-bindings-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/bag-tip-dot-bindings-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($group in @($receipt.inputs, $receipt.outputs, $receipt.validation_inputs)) {
    foreach ($entry in $group.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw ('Verified Bag graph binding differs: ' + $entry.Name) }
        if (-not $entry.Name.StartsWith('unity/')) { continue }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture = Join-Path $project 'Assets/Editor/BagTipDotBindingsValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/bag_tip_dot_bindings_validation.cs') -Destination (Join-Path $fixture 'BagTipDotBindingsValidation.cs') -Force
@{ name = 'BagTipDotBindingsValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Offline.BagTipDots', 'Zjcs.Offline.BagItemQueries', 'Zjcs.Official.BagTipDots', 'Zjcs.Official.BagItemQueries',
    'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeUI', 'Zjcs.Offline.PartnersUI', 'Zjcs.Offline.Progression',
    'Zjcs.Offline.Equipment', 'Zjcs.Offline.Home', 'Zjcs.Offline.Cooking', 'Zjcs.Offline.State', 'Zjcs.Offline.CharacterCreation',
    'Zjcs.Presentation.Home', 'Zjcs.Presentation.Inventory', 'Unity.ugui'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'BagTipDotBindingsValidation.asmdef') -Encoding utf8
$metadata = @{ ($fixture + '.meta') = @('folder', 'DefaultImporter');
    (Join-Path $fixture 'BagTipDotBindingsValidation.cs.meta') = @('script', 'MonoImporter');
    (Join-Path $fixture 'BagTipDotBindingsValidation.asmdef.meta') = @('assembly', 'AssemblyDefinitionImporter') }
foreach ($path in $metadata.Keys) {
    if (Test-Path -LiteralPath $path) { continue }
    $entry = $metadata[$path]; $md5 = [Security.Cryptography.MD5]::Create()
    try { $guid = ([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('zjcs:BagTipDotBindingsValidation:' + $entry[0])))).Replace('-', '').ToLowerInvariant() }
    finally { $md5.Dispose() }
    $folder = if ($entry[0] -eq 'folder') { "folderAsset: yes`n" } else { '' }
    [IO.File]::WriteAllText($path, "fileFormatVersion: 2`nguid: $guid`n$folder$($entry[1]):`n  externalObjects: {}`n", [Text.UTF8Encoding]::new($false))
}
$env:ZJCS_BAG_TIP_BINDINGS_PROJECT = $project; $env:ZJCS_BAG_TIP_BINDINGS_OUTPUT = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'BagTipDotBindingsValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Original Bag/Home binding log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^BAG_TIP_DOT_BINDINGS_PASSED:' -Quiet)) {
    throw ('Original Bag/Home binding validation failed: ' + $log)
}
Write-Output 'BAG_TIP_DOT_BINDINGS_PASSED'

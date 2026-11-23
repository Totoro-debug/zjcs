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
if (@(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('bag-tip-dots-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
foreach ($name in @('bag-tip-dots-source-manifest.json', 'home-entity-bindings-source-manifest.json',
    'home-furniture-controller-source-manifest.json', 'home-storage-native-source-manifest.json')) {
    $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $name)) -Raw | ConvertFrom-Json
    foreach ($entry in $receipt.outputs.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Migrated Bag dependency differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
}
$fixture = Join-Path $project 'Assets/Editor/BagTipDotValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/bag_tip_dot_validation.cs') -Destination (Join-Path $fixture 'BagTipDotValidation.cs') -Force
@{ name = 'BagTipDotValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.BagTipDots', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.PartnerRosterUI'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'BagTipDotValidation.asmdef') -Encoding utf8
$env:ZJCS_BAG_TIP_PROJECT = $project
$env:ZJCS_BAG_TIP_OUTPUT = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'BagTipDotValidation.Run',
    '-logFile', ('"' + $log + '"')
)
Write-Output ('Bag tip graph log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^BAG_TIP_DOTS_PASSED:' -Quiet) -or
    -not (Select-String -LiteralPath $log -Pattern '^HOME_STORAGE_NATIVE_ASSET_PASSED:' -Quiet)) {
    throw ('Original Bag tip graph validation failed: ' + $log)
}
Write-Output 'BAG_TIP_DOTS_PASSED'

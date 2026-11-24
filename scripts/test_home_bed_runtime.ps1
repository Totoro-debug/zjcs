param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [switch]$KeepOutput
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = [IO.Path]::GetFullPath($ProjectPath)
$validationBase = [IO.Path]::GetFullPath((Join-Path $repository 'output/validation'))
if (-not $project.StartsWith($validationBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Select an independent Unity project below output/validation; the shared main project is forbidden.'
}
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
})
if ($running.Count -ne 0) { throw 'Do not stage files into a running Unity project.' }

$work = Join-Path $validationBase ('bed-runtime-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{ format = 1; repository = $repository; ownerPid = $PID } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8

$syncPaths = @(
    'Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots/TipDotNodeManager.cs',
    'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotRuntime.cs',
    'Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FastForward.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.HomeTipDots.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.HomeTipDots.cs.meta',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeHangUpHudRuntime.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs'
)
foreach ($relative in $syncPaths) {
    $src = Join-Path $repository ('unity/' + $relative)
    $dst = Join-Path $project $relative
    [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
    Copy-Item -LiteralPath $src -Destination $dst -Force
}
foreach ($dirRelative in @(
    'Assets/Game/Runtime/Official/HomeFurniture',
    'Assets/Game/Runtime/Official/HomeTipDots',
    'Assets/Game/Runtime/Offline/HomeTipDots',
    'Assets/Game/Resources/Home/HomeTipDots'
)) {
    Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $dirRelative)) `
        -Destination (Split-Path (Join-Path $project $dirRelative)) -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $dirRelative + '.meta')) `
        -Destination (Join-Path $project ($dirRelative + '.meta')) -Force
}

foreach ($manifestName in @('home-bed-native-source-manifest.json', 'home-diningtable-native-source-manifest.json', 'home-native-prefab-references-source-manifest.json')) {
    $manifest = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $manifestName)) -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $manifest.outputs.PSObject.Properties) {
        if (-not $entry.Name.StartsWith('unity/')) { continue }
        $src = Join-Path $repository $entry.Name
        if ((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Bound native resource differs (' + $manifestName + '): ' + $entry.Name)
        }
        $dst = Join-Path $project $entry.Name.Substring('unity/'.Length)
        if ((Test-Path -LiteralPath $dst) -and (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
        [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }
}
$bagReceipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/bag-tip-dot-bindings-source-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $bagReceipt.inputs.PSObject.Properties) {
    if (-not $entry.Name.StartsWith('unity/Assets/Official/Home/StorageBox/Native/')) { continue }
    $src = Join-Path $repository $entry.Name
    $dst = Join-Path $project $entry.Name.Substring('unity/'.Length)
    if ((Test-Path -LiteralPath $dst) -and (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
    [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
    Copy-Item -LiteralPath $src -Destination $dst -Force
}

$fixture = Join-Path $project 'Assets/Editor/HomeBedValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_bed_runtime_validation.cs') `
    -Destination (Join-Path $fixture 'HomeBedRuntimeValidation.cs') -Force

@{
    name = 'HomeBedValidation'
    includePlatforms = @('Editor')
    references = @(
        'Launch', 'Zjcs.Editor', 'Zjcs.Presentation.Home', 'Zjcs.Presentation.World',
        'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Offline.CharacterCreation',
        'Zjcs.Offline.Home', 'Zjcs.Offline.HomeSceneInput', 'Zjcs.Offline.HomeHangUpUI',
        'Zjcs.Offline.HomeExperienceUI', 'Zjcs.Offline.HomeTipDots', 'Zjcs.Offline.PartnersUI',
        'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeHangUpTips', 'Zjcs.Official.HomeHangUpWindowsUI',
        'Zjcs.Official.HomeFastHangUpUI', 'Zjcs.Official.HomeFastHangUpProgressUI', 'Zjcs.Official.HomeAwardUI',
        'Zjcs.Official.HomeHudUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput',
        'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.HomeTipDots', 'Zjcs.Official.HomeRelations',
        'Zjcs.Official.HomeExperienceRules', 'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI', 'Unity.TextMeshPro', 'Unity.ugui'
    )
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeBedValidation.asmdef') -Encoding utf8

foreach ($entry in @(
    @($fixture + '.meta', 'folder', 'DefaultImporter'),
    @((Join-Path $fixture 'HomeBedRuntimeValidation.cs.meta'), 'script', 'MonoImporter'),
    @((Join-Path $fixture 'HomeBedValidation.asmdef.meta'), 'asmdef', 'AssemblyDefinitionImporter')
)) {
    if (Test-Path -LiteralPath $entry[0]) { continue }
    $md5 = [Security.Cryptography.MD5]::Create()
    try {
        $guid = ([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('zjcs:HomeBedValidation:' + $entry[1])))).Replace('-', '').ToLowerInvariant()
    } finally { $md5.Dispose() }
    $folder = if ($entry[1] -eq 'folder') { "folderAsset: yes`n" } else { '' }
    [IO.File]::WriteAllText($entry[0], "fileFormatVersion: 2`nguid: $guid`n$folder$($entry[2]):`n  externalObjects: {}`n", [Text.UTF8Encoding]::new($false))
}

. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$env:ZJCS_BED_PROJECT = $project
$env:ZJCS_VALIDATION_DIR = $work
$env:ZJCS_REPOSITORY_ROOT = $repository
$env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'

try {
    $importLog = Join-Path $work 'import.log'
    $importProcess = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode', '-nographics', '-quit', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'HomeBedRuntimeValidation.Import',
        '-logFile', ('"' + $importLog + '"')
    )
    $importProcess.WaitForExit()
    if ($importProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $importLog -SimpleMatch 'HOME_BED_IMPORT_PASSED' -Quiet)) {
        throw ('Bed validation import/compile failed: ' + $importLog)
    }

    $log = Join-Path $work 'unity.log'
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'HomeBedRuntimeValidation.Run',
        '-logFile', ('"' + $log + '"')
    )
    Write-Output ('Bed runtime validation dir: ' + $work)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_BED_RUNTIME_PASSED:' -Quiet)) {
        throw ('Bed runtime validation failed: ' + $log)
    }
    Write-Output ('HOME_BED_RUNTIME_PASSED: ' + $work)
} finally {
    if (-not $KeepOutput) {
        # Leave directory on failure or when explicitly kept for screenshot inspection.
    }
}

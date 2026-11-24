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

$work = Join-Path $validationBase ('diningtable-runtime-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
@{ format = 1; repository = $repository; ownerPid = $PID } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8

# Sync modified/created runtime assets into the independent validation project.
$syncPaths = @(
    'Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots/TipDotNodeManager.cs',
    'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotRuntime.cs',
    'Assets/Game/Runtime/Offline/PartnersUI/PartnerDialoguePlaybackView.cs',
    'Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStories.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStories.cs.meta',
    'Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeHudView.Partners.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeHudView.Repair.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeHudView.Tutorial.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs'
)
foreach ($relative in $syncPaths) {
    $src = Join-Path $repository ('unity/' + $relative)
    $dst = Join-Path $project $relative
    [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
    Copy-Item -LiteralPath $src -Destination $dst -Force
}
Copy-Item -LiteralPath (Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeFurniture') `
    -Destination (Join-Path $project 'Assets/Game/Runtime/Official') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repository 'unity/Assets/Game/Runtime/Official/HomeFurniture.meta') `
    -Destination (Join-Path $project 'Assets/Game/Runtime/Official/HomeFurniture.meta') -Force

foreach ($manifestName in @('home-diningtable-native-source-manifest.json', 'home-native-prefab-references-source-manifest.json', 'partner-dialogue-reward-assets-source-manifest.json')) {
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

$fixture = Join-Path $project 'Assets/Editor/HomeDiningTableValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_diningtable_runtime_validation.cs') `
    -Destination (Join-Path $fixture 'HomeDiningTableRuntimeValidation.cs') -Force

@{
    name = 'HomeDiningTableValidation'
    includePlatforms = @('Editor')
    references = @(
        'Launch', 'Zjcs.Editor', 'Zjcs.Presentation.Home', 'Zjcs.Presentation.World',
        'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Offline.CharacterCreation',
        'Zjcs.Offline.Home', 'Zjcs.Offline.HomeSceneInput', 'Zjcs.Offline.PartnersUI',
        'Zjcs.Official.HomeFurniture', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput',
        'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.HomeStoryDialog',
        'Zjcs.Official.Partners', 'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.PartnerDialoguePlaybackUI', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.StoryMasks', 'Zjcs.Official.WorldUI',
        'spine-unity', 'Unity.TextMeshPro', 'Unity.ugui'
    )
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeDiningTableValidation.asmdef') -Encoding utf8

foreach ($entry in @(
    @($fixture + '.meta', 'folder', 'DefaultImporter'),
    @((Join-Path $fixture 'HomeDiningTableRuntimeValidation.cs.meta'), 'script', 'MonoImporter'),
    @((Join-Path $fixture 'HomeDiningTableValidation.asmdef.meta'), 'asmdef', 'AssemblyDefinitionImporter')
)) {
    if (Test-Path -LiteralPath $entry[0]) { continue }
    $md5 = [Security.Cryptography.MD5]::Create()
    try {
        $guid = ([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('zjcs:HomeDiningTableValidation:' + $entry[1])))).Replace('-', '').ToLowerInvariant()
    } finally { $md5.Dispose() }
    $folder = if ($entry[1] -eq 'folder') { "folderAsset: yes`n" } else { '' }
    [IO.File]::WriteAllText($entry[0], "fileFormatVersion: 2`nguid: $guid`n$folder$($entry[2]):`n  externalObjects: {}`n", [Text.UTF8Encoding]::new($false))
}

. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$env:ZJCS_DINING_PROJECT = $project
$env:ZJCS_VALIDATION_DIR = $work
$env:ZJCS_REPOSITORY_ROOT = $repository
$env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'

try {
    $importLog = Join-Path $work 'import.log'
    $importProcess = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode', '-nographics', '-quit', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'HomeDiningTableRuntimeValidation.Import',
        '-logFile', ('"' + $importLog + '"')
    )
    $importProcess.WaitForExit()
    if ($importProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $importLog -SimpleMatch 'HOME_DININGTABLE_IMPORT_PASSED' -Quiet)) {
        throw ('DiningTable validation import/compile failed: ' + $importLog)
    }

    $log = Join-Path $work 'unity.log'
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'HomeDiningTableRuntimeValidation.Run',
        '-logFile', ('"' + $log + '"')
    )
    Write-Output ('DiningTable runtime validation dir: ' + $work)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_DININGTABLE_RUNTIME_PASSED:' -Quiet)) {
        throw ('DiningTable runtime validation failed: ' + $log)
    }
    Write-Output ('HOME_DININGTABLE_RUNTIME_PASSED: ' + $work)
} finally {
    if (-not $KeepOutput) {
        # Leave directory on failure or when explicitly kept for screenshot inspection.
    }
}

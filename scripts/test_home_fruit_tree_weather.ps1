param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe',
    [switch]$NaturalProduction,
    [switch]$UnlockStory,
    [switch]$PetUnlockStory,
    [switch]$PetGachaControls,
    [ValidateRange(5, 120)][int]$TimeoutMinutes = 45
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = (Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\', '/')
$validationBase = [IO.Path]::GetFullPath((Join-Path $repository 'output/validation'))
if (-not $project.StartsWith($validationBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Use an independent Unity source project under output/validation.'
}
$ancestor = [IO.DirectoryInfo]::new($project)
while ($ancestor.FullName -ne $validationBase) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or
        ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'A worktree or linked source project is forbidden.'
    }
    $ancestor = $ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count) { throw 'Do not stage sources into a running Unity project.' }
$work = Join-Path $validationBase ('fruit-tree-weather-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
# Git archive may apply checkout line endings. Keep the exact verified payload
# bytes when a source project differs solely in line endings. Leave content
# differences alone, so concurrent resource edits do not enter this fixture.
$resourceRoot = Join-Path $repository 'unity/Assets'
$restoredResourceBytes = 0
$resourceUtf8 = [Text.UTF8Encoding]::new($false, $true)
foreach ($source in Get-ChildItem -LiteralPath $resourceRoot -Filter '*.json' -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath((Join-Path $repository 'unity'), $source.FullName)
    $target = Join-Path $project $relative
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $sourceHash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $sourceHash) { continue }
    $sourceText = $resourceUtf8.GetString([IO.File]::ReadAllBytes($source.FullName))
    $targetText = $resourceUtf8.GetString([IO.File]::ReadAllBytes($target))
    if (-not [string]::Equals($sourceText.Replace("`r`n", "`n"), $targetText.Replace("`r`n", "`n"), [StringComparison]::Ordinal)) { continue }
    $manifestPath = Join-Path $source.DirectoryName ($source.BaseName + '-source-manifest.json')
    if (Test-Path -LiteralPath $manifestPath) {
        $resourceManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($resourceManifest.output_sha256 -and $sourceHash.ToLowerInvariant() -ne $resourceManifest.output_sha256) {
            throw ('Original resource payload differs from its source manifest: ' + $relative)
        }
    }
    Copy-Item -LiteralPath $source.FullName -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $sourceHash) {
        throw ('Resource changed while restoring source bytes: ' + $relative)
    }
    $restoredResourceBytes++
}
Write-Output ('Restored original JSON resource bytes: ' + $restoredResourceBytes)
$fruitReceipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-fruit-tree-native-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $fruitReceipt.outputs.PSObject.Properties) {
    $target = Join-Path $project $entry.Name.Substring(6)
    if (-not (Test-Path -LiteralPath $target) -or
        (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Independent official fruit-tree asset differs: ' + $entry.Name)
    }
}
# Reuse the independent source project's decoded official assets; update this
# flow's source and native weather closure without copying concurrent edits.
$hintReceipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-pet-achievement-hint-bindings-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $hintReceipt.inputs.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath (Join-Path $repository $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Original pet hint source differs: ' + $entry.Name)
    }
}
if ((Get-FileHash -LiteralPath (Join-Path $repository $hintReceipt.output.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hintReceipt.output.sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $repository ($hintReceipt.output.path + '.meta')) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hintReceipt.output.meta_sha256) {
    throw 'Original pet hint configuration differs from its receipt.'
}
foreach ($receiptName in @('home-weather-snow-source-manifest.json', 'home-snow-effect-native-source-manifest.json')) {
    $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/' + $receiptName)) -Raw | ConvertFrom-Json
    foreach ($section in @('inputs', 'validation_inputs', 'runtime_bindings', 'outputs')) {
        foreach ($entry in $receipt.$section.PSObject.Properties) {
            $source = Join-Path $repository $entry.Name
            if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
                throw ('Official snow source identity differs: ' + $entry.Name)
            }
            if ($section -eq 'outputs' -and $entry.Name.StartsWith('unity/')) {
                $target = Join-Path $project $entry.Name.Substring(6)
                [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
                Copy-Item -LiteralPath $source -Destination $target -Force
            }
        }
    }
}
$scoped = @(
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetGachaControls.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetGachaControls.cs.meta',
    'Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyImageServices.cs',
    'Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyImageServices.cs.meta',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.FruitTreeSkins.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.FruitTreeUnlock.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.FruitTreeUnlock.cs.meta',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetTreeUnlock.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetTreeUnlock.cs.meta',
    'Assets/Game/Runtime/Presentation/Home/OfflineHomePetTreeTips.cs',
    'Assets/Game/Runtime/Presentation/Home/OfflineHomePetTreeTips.cs.meta',
    'Assets/Game/Runtime/Offline/HomeUI/OfflineHomeFunctionLimits.cs',
    'Assets/Game/Runtime/Offline/HomeUI/OfflineHomeFunctionLimits.cs.meta',
    'Assets/Game/Runtime/Offline/Progression/OfflinePetGachaService.cs',
    'Assets/Game/Resources/Home/pet-achievement-hints.json',
    'Assets/Game/Resources/Home/pet-achievement-hints.json.meta',
    'Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStories.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeHudView.Tutorial.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStoryWaits.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.FurnitureStoryWaits.cs.meta',
    'Assets/Game/Runtime/Offline/HomeStoryDialog/HomeFurnitureStoryDialogNode.cs'
)
$committed = @(
    'Assets/Game/Editor/Home/HomeAcceptanceLauncher.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeNativeSnowEffectView.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeNativeSceneSlots.cs',
    'Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs',
    'Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotRuntime.cs',
    'Assets/Game/Runtime/Official/HomeFurniture/HomeFurnitureServices.cs'
)
foreach ($relative in $scoped) {
    Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $relative)) -Destination (Join-Path $project $relative) -Force
}
$gachaModule = 'Assets/Game/Runtime/Official/GachaUI'
$gachaTarget = Join-Path $project $gachaModule
[void](New-Item -ItemType Directory -Path $gachaTarget -Force)
Get-ChildItem -LiteralPath (Join-Path $repository ('unity/' + $gachaModule)) -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $gachaTarget $_.Name) -Force
}
Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $gachaModule + '.meta')) -Destination ($gachaTarget + '.meta') -Force
$scoreModule = 'Assets/Game/Runtime/Offline/GachaUI'
$scoreTarget = Join-Path $project $scoreModule
[void](New-Item -ItemType Directory -Path $scoreTarget -Force)
Get-ChildItem -LiteralPath (Join-Path $repository ('unity/' + $scoreModule)) -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $scoreTarget $_.Name) -Force
}
Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $scoreModule + '.meta')) -Destination ($scoreTarget + '.meta') -Force
if ($PetGachaControls) {
    $scoreReceipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/pet-score-reward-source-manifest.json') -Raw | ConvertFrom-Json
    foreach ($section in @('inputs', 'outputs')) {
        foreach ($entry in $scoreReceipt.$section.PSObject.Properties) {
            $source = Join-Path $repository $entry.Name
            if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
                throw ('Original score reward source or binding differs: ' + $entry.Name)
            }
            if ($section -eq 'outputs') {
                Copy-Item -LiteralPath $source -Destination (Join-Path $project $entry.Name.Substring(6)) -Force
            }
        }
    }
    $gachaReceiptPath = Join-Path $repository 'assets/reference/pet-gacha-controls-native-source-manifest.json'
    $gachaReceipt = Get-Content -LiteralPath $gachaReceiptPath -Raw | ConvertFrom-Json
    foreach ($entry in $gachaReceipt.inputs.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $repository $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Original gacha control input differs: ' + $entry.Name)
        }
    }
    foreach ($entry in $gachaReceipt.runtime_bindings.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $repository $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Gacha control runtime differs: ' + $entry.Name)
        }
    }
    foreach ($entry in $gachaReceipt.script_bindings.PSObject.Properties) {
        $binding = $entry.Value
        $source = Join-Path $repository $binding.path
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $binding.sha256 -or
            (Get-FileHash -LiteralPath ($source + '.meta') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $binding.meta_sha256) {
            throw ('Native gacha control component differs: ' + $binding.path)
        }
        $target = Join-Path $project $binding.path.Substring(6)
        if ($binding.path.StartsWith('unity/Library/PackageCache/')) {
            # A fresh source project has no package cache yet. Restore the
            # complete verified source package, rather than an isolated class.
            $segments = $binding.path.Substring(6).Split('/')
            $packageRelative = $segments[0..2] -join '/'
            $packageTarget = Join-Path $project $packageRelative
            if (-not (Test-Path -LiteralPath (Join-Path $packageTarget 'package.json'))) {
                [void](New-Item -ItemType Directory -Path (Split-Path $packageTarget) -Force)
                Copy-Item -LiteralPath (Join-Path $repository ('unity/' + $packageRelative)) -Destination $packageTarget -Recurse -Force
            }
        }
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
        Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force
    }
    foreach ($entry in $gachaReceipt.outputs.PSObject.Properties) {
        $source = Join-Path $repository $entry.Name
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw ('Bound gacha control asset differs: ' + $entry.Name)
        }
        $target = Join-Path $project $entry.Name.Substring(6)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
    Copy-Item -LiteralPath $gachaReceiptPath -Destination (Join-Path $work 'control-fixture.json')
}
foreach ($relative in $committed) {
    $source = & git -C $repository show ('HEAD:unity/' + $relative)
    if ($LASTEXITCODE -ne 0) { throw ('Cannot read committed source: ' + $relative) }
    [IO.File]::WriteAllText((Join-Path $project $relative), (($source -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
}
foreach ($fruitService in @('unity/Assets/Game/Runtime/Offline/Progression/OfflineFruitTreeService.cs',
    'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs')) {
    $source = & git -C $repository show (':' + $fruitService)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read staged fruit-tree service.' }
    [IO.File]::WriteAllText((Join-Path $project $fruitService.Substring(6)),
        (($source -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
}
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$names = @('ZJCS_HOME_ACCEPTANCE_ROOT', 'ZJCS_HOME_ACCEPTANCE_OUTPUT', 'ZJCS_PET_GACHA_CONTROL_FIXTURE')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:ZJCS_HOME_ACCEPTANCE_ROOT = Join-Path $work 'profiles'
    $env:ZJCS_HOME_ACCEPTANCE_OUTPUT = $work
    if ($PetGachaControls) { $env:ZJCS_PET_GACHA_CONTROL_FIXTURE = Join-Path $work 'control-fixture.json' }
    $modeArguments = if ($PetGachaControls) { @('-petGachaControlsAcceptance') } elseif ($PetUnlockStory) { @('-petTreeUnlockAcceptance') } elseif ($UnlockStory) { @('-fruitTreeUnlockAcceptance') } elseif ($NaturalProduction) { @('-fruitTreeNaturalProductionAcceptance') } else { @('-fruitTreeNativeSkinsAcceptance') }
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList (@(
        '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'Zjcs.Editor.Home.HomeAcceptanceLauncher.Run',
        '-homeAcceptance', '-fruitTreeNativeAcceptance',
        '-logFile', ('"' + $log + '"')
    ) + $modeArguments)
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
Write-Output ('Fruit-tree weather PID ' + $process.Id + ', log: ' + $log)
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(30000)) {
    if ($timer.Elapsed.TotalMinutes -gt $TimeoutMinutes -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Fruit-tree weather validation stopped: ' + $log)
    }
}
$process.Refresh()
$reportPath = Join-Path $work 'report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) {
    throw ('Fruit-tree weather validation failed: ' + $log)
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$requiredPattern = if ($PetGachaControls) { 'PASS pet-gacha-controls-*' } elseif ($PetUnlockStory) { 'PASS pet-unlock-*' } elseif ($UnlockStory) { 'PASS fruit-unlock-completed*' } elseif ($NaturalProduction) { 'PASS native-fruit-tree-natural-*' } else { 'PASS native-fruit-tree-weather-*' }
$requiredCount = if ($PetGachaControls) { 10 } elseif ($PetUnlockStory) { 16 } elseif ($UnlockStory) { 1 } elseif ($NaturalProduction) { 2 } else { 12 }
if (-not $report.success -or @($report.lines | Where-Object { $_ -like $requiredPattern }).Count -ne $requiredCount) {
    throw ('The selected fruit-tree flow must pass: ' + $reportPath)
}
Write-Output ('FRUIT_TREE_WEATHER_PASSED: ' + $work)

$ErrorActionPreference = 'Stop'
$incomeSegmentRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $incomeSegmentRoot 'unity/Assets/Game/Runtime/Offline/State/OfflineIncomeTimeSegments.cs'),
    (Join-Path $incomeSegmentRoot 'unity/Assets/Game/Runtime/Offline/Progression/OfflineFruitTreeService.cs'),
    (Join-Path $incomeSegmentRoot 'unity/Assets/Game/Runtime/Offline/Progression/OfflineFruitTreeProgress.cs'),
    (Join-Path $incomeSegmentRoot 'scripts/fixtures/home_income_time_segments_harness.cs')
)
Write-Output ('HOME_INCOME_TIME_SEGMENTS_PASSED checks=' + [IncomeTimeSegmentsChecks]::Run())

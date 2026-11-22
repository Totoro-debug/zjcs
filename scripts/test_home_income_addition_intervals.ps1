$ErrorActionPreference = 'Stop'
$incomeIntervalRoot = Split-Path -Parent $PSScriptRoot
$incomeIntervalFiles = @([IO.Directory]::GetFiles((Join-Path $incomeIntervalRoot 'unity/Assets/Game/Runtime/Official/HomeIncomeRules'), '*.cs'))
$incomeIntervalFiles += @(
    (Join-Path $incomeIntervalRoot 'unity/Assets/Game/Runtime/Offline/State/OfflineIncomeTimeSegments.cs'),
    (Join-Path $incomeIntervalRoot 'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs'),
    (Join-Path $incomeIntervalRoot 'unity/Assets/Game/Runtime/Offline/Home/IHomeIncomeAdditionSnapshot.cs'),
    (Join-Path $incomeIntervalRoot 'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncomeAddition.cs'),
    (Join-Path $incomeIntervalRoot 'scripts/fixtures/home_income_addition_intervals_harness.cs')
)
Add-Type -Path $incomeIntervalFiles
Write-Output ('HOME_INCOME_ADDITION_INTERVALS_PASSED checks=' + [HomeIncomeAdditionIntervalChecks]::Run())

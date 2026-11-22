$ErrorActionPreference = 'Stop'
$incomeLedgerRoot = Split-Path -Parent $PSScriptRoot
$incomeLedgerFiles = @([IO.Directory]::GetFiles((Join-Path $incomeLedgerRoot 'unity/Assets/Game/Runtime/Official/HomeIncomeRules'), '*.cs'))
$incomeLedgerFiles += @(
    (Join-Path $incomeLedgerRoot 'unity/Assets/Game/Runtime/Offline/State/OfflineIncomeTimeSegments.cs'),
    (Join-Path $incomeLedgerRoot 'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs'),
    (Join-Path $incomeLedgerRoot 'unity/Assets/Game/Runtime/Offline/Home/IHomeIncomeAdditionSnapshot.cs'),
    (Join-Path $incomeLedgerRoot 'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncomeAddition.cs'),
    (Join-Path $incomeLedgerRoot 'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeActivityLedger.cs'),
    (Join-Path $incomeLedgerRoot 'scripts/fixtures/home_income_activity_ledger_harness.cs')
)
Add-Type -Path $incomeLedgerFiles -ReferencedAssemblies @('System.Collections','System.Runtime','System.Text.Json','System.Memory')
Write-Output ('HOME_INCOME_ACTIVITY_LEDGER_PASSED checks=' + [HomeIncomeActivityLedgerChecks]::Run())

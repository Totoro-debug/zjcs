$ErrorActionPreference = 'Stop'
$additionRoot = Split-Path -Parent $PSScriptRoot
$additionFiles = @([IO.Directory]::GetFiles((Join-Path $additionRoot 'unity/Assets/Game/Runtime/Official/HomeOutputAddition'), '*.cs'))
$additionFiles += @(
    (Join-Path $additionRoot 'unity/Assets/Game/Runtime/Offline/Home/IHomeIncomeAdditionSnapshot.cs'),
    (Join-Path $additionRoot 'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeIncomeAdditionSnapshot.cs'),
    (Join-Path $additionRoot 'scripts/fixtures/home_income_addition_snapshot_harness.cs')
)
Add-Type -Path $additionFiles
$additionConfig = Get-Content -LiteralPath (Join-Path $additionRoot 'unity/Assets/Game/Resources/Home/output-addition-rules.json') -Raw | ConvertFrom-Json -AsHashtable
$additionRows = [Collections.Generic.List[Zjcs.Official.HomeOutputAddition.OutputAdditionInfo]]::new()
foreach ($additionRow in $additionConfig.rows) {
    $additionValue = [Zjcs.Official.HomeOutputAddition.OutputAdditionInfo]::new()
    $additionValue.ClassId = $additionRow.ClassId
    $additionValue.OutputAdditionPropVal = $additionRow.OutputAdditionPropVal
    $additionValue.OutputActionType = [Zjcs.Official.HomeOutputAddition.OutputActionType]$additionRow.OutputActionType
    $additionValue.OutputGroupType = [Zjcs.Official.HomeOutputAddition.OutputGroupType]$additionRow.OutputGroupType
    $additionValue.OutputAdditionPropType = [Zjcs.Official.HomeOutputAddition.OutputAdditionPropType]$additionRow.OutputAdditionPropType
    $additionValue.ShowGroupKey = $additionRow.ShowGroupKey
    $additionValue.ShowKey = $additionRow.ShowKey
    $additionRows.Add($additionValue)
}
$additionSettings = [Zjcs.Official.HomeOutputAddition.HomeOutputAdditionSettings]::new()
$additionSettings.HomeHangUpAddPercentTypeDict = [Collections.Generic.Dictionary[int,Zjcs.Official.HomeOutputAddition.OutputAdditionPropType]]::new()
$additionSettings.HomeHangUpMaxStoreTypeDict = [Collections.Generic.Dictionary[int,Zjcs.Official.HomeOutputAddition.OutputAdditionPropType]]::new()
foreach ($additionPair in $additionConfig.settings.HomeHangUpAddPercentTypeDict.GetEnumerator()) {
    $additionSettings.HomeHangUpAddPercentTypeDict[[int]$additionPair.Key] = [Zjcs.Official.HomeOutputAddition.OutputAdditionPropType]$additionPair.Value
}
foreach ($additionPair in $additionConfig.settings.HomeHangUpMaxStoreTypeDict.GetEnumerator()) {
    $additionSettings.HomeHangUpMaxStoreTypeDict[[int]$additionPair.Key] = [Zjcs.Official.HomeOutputAddition.OutputAdditionPropType]$additionPair.Value
}
Write-Output ('HOME_INCOME_ADDITION_SNAPSHOT_PASSED checks=' + [HomeIncomeAdditionSnapshotChecks]::Run($additionRows.ToArray(), $additionSettings))

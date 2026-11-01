$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root = Split-Path -Parent $PSScriptRoot
Invoke-ZjcsValidation -Name 'skills-equipment-selection-rules' -ScriptBlock {
    param($workspace)
    $reference = Join-Path $workspace 'equipment-selection-reference.cs'
    & D:/miniconda3/envs/comm/python.exe -B (Join-Path $PSScriptRoot 'prepare_battle_equipment_selection_reference.py') --output $reference
    if ($LASTEXITCODE -ne 0) { throw 'Original equipment selection reference preparation failed.' }
    $runtime = Join-Path $root 'unity/Assets/Game/Runtime'
    Add-Type -Path @(
        (Join-Path $runtime 'Offline/Equipment/OfficialEquipmentJson.cs'),
        (Join-Path $runtime 'Offline/Equipment/OfficialEquipmentCatalog.cs'),
        (Join-Path $runtime 'Offline/Equipment/OfficialEquipmentCatalog.TermReplacement.cs'),
        (Join-Path $runtime 'Offline/Equipment/OfflineEquipmentProfile.cs'),
        (Join-Path $runtime 'Offline/Progression/OfflineEquipmentPropSource.cs'),
        (Join-Path $runtime 'Offline/Progression/OfflineEquipmentPropSource.BattleTerms.cs'),
        $reference, (Join-Path $PSScriptRoot 'fixtures/battle_equipment_selection.cs'))
    [EquipmentSelectionChecks]::Run([IO.File]::ReadAllText(
        (Join-Path $root 'unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json')))
}

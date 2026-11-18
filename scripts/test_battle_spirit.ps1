$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputPath = Join-Path $root 'unity/Assets/Official/Combat'
function Read-SpiritInput($name) { [IO.File]::ReadAllText((Join-Path $inputPath "$name.json")) }
function Spirit-Proof($name) { Get-Content (Join-Path $inputPath "$name-source-manifest.json") -Raw | ConvertFrom-Json }
Invoke-ZjcsValidation -Name 'battle-spirit-rules' -ScriptBlock {
    param($workspace)
    & python -B (Join-Path $PSScriptRoot 'prepare_battle_status_property_reference.py') --output $workspace
    if ($LASTEXITCODE -ne 0) { throw 'Original status property reference preparation failed.' }
    $sources = @(Get-FightCombatSources) + @(
        (Join-Path $PSScriptRoot 'fixtures/battle_spirit_harness.cs'),
        (Join-Path $workspace 'status-property-reference.cs'))
    Add-Type -Path $sources
    $sp = Spirit-Proof 'skill-rules'; $bp = Spirit-Proof 'battle-rules'; $rp = Spirit-Proof 'rules'
    $catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        (Read-SpiritInput 'skill-rules'), $sp.output_sha256, $sp.manifest,
        (Read-SpiritInput 'battle-rules'), $bp.output_sha256, $bp.manifest)
    $formula = $catalog.CreateDamageFormula((Read-SpiritInput 'rules'), $rp.output_sha256, $rp.manifest)
    $items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
        (Read-SpiritInput 'rules'), $rp.output_sha256, $rp.manifest)
    $rules = Read-SpiritInput 'rules' | ConvertFrom-Json
    [SourceStatusPropertyReference.ReferenceState]::Configure(
        [int[]]@($rules.prop_cfg.PropType),
        [int[]]@($rules.prop_cfg | Where-Object Float | ForEach-Object PropType))
    [BattleSpiritHarness]::Run($catalog, $formula, $items)
}

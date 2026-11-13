$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputPath = Join-Path $root 'unity/Assets/Official/Combat'
function Read-ArcInput($name) { [IO.File]::ReadAllText((Join-Path $inputPath "$name.json")) }
function Arc-Proof($name) { Get-Content (Join-Path $inputPath "$name-source-manifest.json") -Raw | ConvertFrom-Json }
Invoke-ZjcsValidation -Name 'arc-shield-rules' -ScriptBlock {
    param($workspace)
    & python -B (Join-Path $PSScriptRoot 'prepare_battle_status_property_reference.py') --output $workspace
    if ($LASTEXITCODE -ne 0) { throw 'Original status property reference preparation failed.' }
    $sources = @(Get-FightCombatSources) + @(
        (Join-Path $PSScriptRoot 'fixtures/battle_arc_shield_harness.cs'),
        (Join-Path $workspace 'status-property-reference.cs'))
    Add-Type -Path $sources
    $sp = Arc-Proof 'skill-rules'; $bp = Arc-Proof 'battle-rules'; $rp = Arc-Proof 'rules'
    $catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        (Read-ArcInput 'skill-rules'), $sp.output_sha256, $sp.manifest,
        (Read-ArcInput 'battle-rules'), $bp.output_sha256, $bp.manifest)
    $formula = $catalog.CreateDamageFormula((Read-ArcInput 'rules'), $rp.output_sha256, $rp.manifest)
    $items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
        (Read-ArcInput 'rules'), $rp.output_sha256, $rp.manifest)
    $rules = Read-ArcInput 'rules' | ConvertFrom-Json
    [SourceStatusPropertyReference.ReferenceState]::Configure(
        [int[]]@($rules.prop_cfg.PropType),
        [int[]]@($rules.prop_cfg | Where-Object Float | ForEach-Object PropType))
    [BattleArcShieldHarness]::Run($catalog, $formula, $items)
    Write-Output 'Arc shield passed: original 10760/10761/24030, original-source property comparison, owner-round expiry, view timing, receipt replay and checkpoint continuation at every boundary.'
}

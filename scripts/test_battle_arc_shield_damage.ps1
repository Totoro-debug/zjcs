$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputPath = Join-Path $root 'unity/Assets/Official/Combat'
function Read-ArcDamageInput($name) { [IO.File]::ReadAllText((Join-Path $inputPath "$name.json")) }
function Arc-DamageProof($name) { Get-Content (Join-Path $inputPath "$name-source-manifest.json") -Raw | ConvertFrom-Json }
Invoke-ZjcsValidation -Name 'arc-shield-damage' -ScriptBlock {
    param($workspace)
    Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_arc_shield_damage_harness.cs'))
    $sp=Arc-DamageProof 'skill-rules'; $bp=Arc-DamageProof 'battle-rules'; $rp=Arc-DamageProof 'rules'
    $catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        (Read-ArcDamageInput 'skill-rules'),$sp.output_sha256,$sp.manifest,
        (Read-ArcDamageInput 'battle-rules'),$bp.output_sha256,$bp.manifest)
    $formula=$catalog.CreateDamageFormula((Read-ArcDamageInput 'rules'),$rp.output_sha256,$rp.manifest)
    $items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
        (Read-ArcDamageInput 'rules'),$rp.output_sha256,$rp.manifest)
    $cases=[BattleArcShieldDamageHarness]::Run($catalog,$formula,$items)
    [IO.File]::WriteAllText((Join-Path $workspace 'arc-damage-cases.json'),(ConvertTo-Json -InputObject $cases -Depth 30))
    & python -B (Join-Path $PSScriptRoot 'prepare_battle_arc_damage_reference.py')
    if($LASTEXITCODE -ne 0) { throw 'Original arc incoming-damage comparison failed.' }
}

$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
$proof=Get-Content (Join-Path $root 'assets/reference/battle-rules/ripple-impact-source-manifest.json') -Raw|ConvertFrom-Json
foreach($entry in @($proof.original_code)+@($proof.original_assemblies)+@($proof.native_inputs)+@($proof.runtime_inputs)+@($proof.verifier)) {
    if((Get-FileHash -LiteralPath (Join-Path $root $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Ripple impact source identity changed: $($entry.path)"
    }
}
$inputPath=Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $PSScriptRoot 'fixtures/battle_ripple_impact_harness.cs'))
$sp=Get-Content (Join-Path $inputPath 'skill-rules-source-manifest.json') -Raw|ConvertFrom-Json
$bp=Get-Content (Join-Path $inputPath 'battle-rules-source-manifest.json') -Raw|ConvertFrom-Json
$rp=Get-Content (Join-Path $inputPath 'rules-source-manifest.json') -Raw|ConvertFrom-Json
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputPath 'skill-rules.json')),$sp.output_sha256,$sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputPath 'battle-rules.json')),$bp.output_sha256,$bp.manifest)
$rules=[IO.File]::ReadAllText((Join-Path $inputPath 'rules.json'))
$formula=$catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
[BattleRippleImpactHarness]::Run($catalog,$formula,$items)

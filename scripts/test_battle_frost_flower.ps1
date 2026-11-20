$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
$proof=Get-Content (Join-Path $root 'assets/reference/battle-rules/first-promotion-source-manifest.json') -Raw|ConvertFrom-Json
foreach($entry in @($proof.original_code)+@($proof.verifier)+@($proof.configuration.runtime_source_manifests)+@($proof.configuration.formatters)+@($proof.configuration.verifier)) {
    if((Get-FileHash -LiteralPath (Join-Path $root $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Frost flower source identity changed: $($entry.path)"
    }
}
Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $PSScriptRoot 'fixtures/battle_frost_flower_harness.cs'))
$inputs=Join-Path $root 'unity/Assets/Official/Combat'
$sp=Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw|ConvertFrom-Json
$bp=Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw|ConvertFrom-Json
$rp=Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw|ConvertFrom-Json
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')),$sp.output_sha256,$sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')),$bp.output_sha256,$bp.manifest)
$rules=[IO.File]::ReadAllText((Join-Path $inputs 'rules.json'))
$formula=$catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
[BattleFrostFlowerHarness]::Run($catalog,$formula,$items)

$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
$proof=Get-Content (Join-Path $root 'assets/reference/battle-rules/sword-counterattack-source-manifest.json') -Raw|ConvertFrom-Json
foreach($entry in @($proof.verified_scope)+@($proof.original_code)+@($proof.runtime_inputs)+@($proof.adapter)) {
    if((Get-FileHash -LiteralPath (Join-Path $root $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Sword counterattack source identity changed: $($entry.path)"
    }
}
$inputPath=Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $PSScriptRoot 'fixtures/battle_sword_counterattack_harness.cs'))
$sp=Get-Content (Join-Path $inputPath 'skill-rules-source-manifest.json') -Raw|ConvertFrom-Json
$bp=Get-Content (Join-Path $inputPath 'battle-rules-source-manifest.json') -Raw|ConvertFrom-Json
$rp=Get-Content (Join-Path $inputPath 'rules-source-manifest.json') -Raw|ConvertFrom-Json
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputPath 'skill-rules.json')),$sp.output_sha256,$sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputPath 'battle-rules.json')),$bp.output_sha256,$bp.manifest)
$rules=[IO.File]::ReadAllText((Join-Path $inputPath 'rules.json'))
$formula=$catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
[BattleSwordCounterattackHarness]::Run($catalog,$formula,$items)

param([switch]$BoundaryOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
if (-not $env:ZJCS_VALIDATION_DIR) { throw 'Set ZJCS_VALIDATION_DIR to this session workspace.' }
$root = Split-Path -Parent $PSScriptRoot
$proof = Get-Content (Join-Path $root 'assets/reference/battle-rules/void-blessing-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in @($proof.original_code) + @($proof.runtime_inputs)) {
    if ((Get-FileHash -LiteralPath (Join-Path $root $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Void blessing source identity changed: $($entry.path)"
    }
}
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_void_blessing_harness.cs'))
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
$skillProof = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$battleProof = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rulesProof = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $skillProof.output_sha256, $skillProof.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $battleProof.output_sha256, $battleProof.manifest)
$rules = [IO.File]::ReadAllText((Join-Path $inputs 'rules.json'))
$formula = $catalog.CreateDamageFormula($rules, $rulesProof.output_sha256, $rulesProof.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, $rulesProof.output_sha256, $rulesProof.manifest)
[BattleVoidBlessingHarness]::RunBoundary($catalog, $formula, $items)
if (-not $BoundaryOnly) {
    [BattleVoidBlessingHarness]::Run($catalog, $formula, $items)
    [BattleVoidBlessingHarness]::RunUnique($catalog, $formula, $items)
    [BattleVoidBlessingHarness]::RunFortress($catalog, $formula, $items)
}

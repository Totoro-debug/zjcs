$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
if (-not $env:ZJCS_VALIDATION_DIR) { throw 'Set ZJCS_VALIDATION_DIR to this session workspace.' }
$root = Split-Path -Parent $PSScriptRoot
$proof = Get-Content (Join-Path $root 'assets/reference/battle-rules/skill-start-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in @($proof.original_code) + @($proof.runtime_inputs)) {
    if ((Get-FileHash -LiteralPath (Join-Path $root $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Skill startup source identity changed: $($entry.path)"
    }
}
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_wind_shield_harness.cs'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$formula = $catalog.CreateDamageFormula([IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load([IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
[BattleWindShieldHarness]::Run($catalog, $formula, $items)

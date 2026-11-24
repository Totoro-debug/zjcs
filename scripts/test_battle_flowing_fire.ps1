param([string]$SourceRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$repository = Split-Path -Parent $PSScriptRoot
if (-not $SourceRoot) { $SourceRoot = $repository }
$root = (Get-Item -LiteralPath $SourceRoot).FullName.TrimEnd('\', '/')
$sources = @(Get-FightCombatSources | ForEach-Object {
    if (-not $_.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The common combat source list refers outside the repository.'
    }
    Join-Path $root $_.Substring($repository.Length + 1)
})
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path ($sources +
    (Join-Path $PSScriptRoot 'fixtures/skill_rule_pack_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_flowing_fire_harness.cs'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$index = Get-Content (Join-Path $root 'unity/Assets/Game/Resources/Combat/skill-rule-packs.json') -Raw | ConvertFrom-Json
foreach ($entry in $index.resources) {
    $pack = Join-Path $root 'unity/Assets/Game/Resources' ($entry + '.json')
    $source = Get-Content ($pack -replace '\.json$', '-source-manifest.json') -Raw | ConvertFrom-Json
    [SkillRulePackHarness]::Apply($catalog, [IO.File]::ReadAllText($pack), $source.output_sha256, $source.manifest)
}
$rules = [IO.File]::ReadAllText((Join-Path $inputs 'rules.json'))
$formula = $catalog.CreateDamageFormula($rules, $rp.output_sha256, $rp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, $rp.output_sha256, $rp.manifest)
[BattleFlowingFireHarness]::Run($catalog, $formula, $items)

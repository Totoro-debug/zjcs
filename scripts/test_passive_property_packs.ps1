$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) +
    (Join-Path $PSScriptRoot 'fixtures/passive_property_pack_harness.cs'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$rules = [IO.File]::ReadAllText((Join-Path $inputs 'rules.json'))
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, $rp.output_sha256, $rp.manifest)
$formula = $catalog.CreateDamageFormula($rules, $rp.output_sha256, $rp.manifest)
$pack = Join-Path $root 'unity/Assets/Game/Resources/Combat/SkillRules/10604-passive.json'
$source = Get-Content ($pack -replace '\.json$', '-source-manifest.json') -Raw | ConvertFrom-Json
[PassivePropertyPackHarness]::Run($catalog, $items, $formula, [IO.File]::ReadAllText($pack), $source.output_sha256, $source.manifest)
$pack = Join-Path $root 'unity/Assets/Game/Resources/Combat/SkillRules/10528-passive.json'
$source = Get-Content ($pack -replace '\.json$', '-source-manifest.json') -Raw | ConvertFrom-Json
[PassivePropertyPackHarness]::RunDarkAffinity($catalog, $items, $formula, [IO.File]::ReadAllText($pack), $source.output_sha256, $source.manifest)

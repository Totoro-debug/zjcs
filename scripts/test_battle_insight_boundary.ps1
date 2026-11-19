$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
& python (Join-Path $PSScriptRoot 'prepare_skill_passive_property_bindings.py')
if ($LASTEXITCODE -ne 0) { throw 'Official passive property inputs failed verification.' }
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_insight_boundary_harness.cs'))
$skillProof = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$battleProof = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rulesProof = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $skillProof.output_sha256, $skillProof.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $battleProof.output_sha256, $battleProof.manifest)
$rules = [IO.File]::ReadAllText((Join-Path $inputs 'rules.json'))
$formula = $catalog.CreateDamageFormula($rules, $rulesProof.output_sha256, $rulesProof.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, $rulesProof.output_sha256, $rulesProof.manifest)
[BattleInsightBoundaryHarness]::Run($catalog, $formula, $items)

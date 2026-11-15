$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_fighter_hp_stacks_harness.cs'))
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
$skillJson = Get-Content (Join-Path $inputs 'skill-rules.json') -Raw
$battleJson = Get-Content (Join-Path $inputs 'battle-rules.json') -Raw
$rulesJson = Get-Content (Join-Path $inputs 'rules.json') -Raw
$skillProof = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$battleProof = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rulesProof = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skillJson,
    $skillProof.output_sha256, $skillProof.manifest, $battleJson,
    $battleProof.output_sha256, $battleProof.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    $rulesJson, $rulesProof.output_sha256, $rulesProof.manifest)
$formula = $catalog.CreateDamageFormula($rulesJson, $rulesProof.output_sha256, $rulesProof.manifest)
[BattleFighterHpStacksHarness]::Run($catalog, $items, $formula)
Write-Output 'Fighter current-HP stack checks passed: entry, damage, healing, boundaries, properties, cultivation and checkpoint restore.'

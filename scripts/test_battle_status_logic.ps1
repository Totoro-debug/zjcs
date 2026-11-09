$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_status_logic_harness.cs') + (Join-Path $PSScriptRoot 'fixtures/battle_status_sequence_harness.cs'))
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
[BattleStatusLogicHarness]::Run($catalog, $items, $formula)
Write-Output 'Logic checks passed: status self-init, persistent blackboard/checkpoint restore, official filter operands, stack changes/multipliers/identity/zero-layer end/consumption/source creation/caps, original 24745/24774 startup, 24774 absorption snapshots/no-match/actual skill condition/production 64280 dispatch, unsupported ports/conditions fail closed; all 15 skill graphs retained and restored for legacy saves.'

$fixture = Get-Content (Join-Path $root 'assets/reference/battle-rules/status-sequence-multiplier.json') -Raw | ConvertFrom-Json
foreach ($entity in $fixture.entities) {
    $graph = $entity.logic | ConvertTo-Json -Depth 100 -Compress
    $skill = if ($entity.entity_id -eq 24679) { 13625 } else { 13635 }
    [BattleStatusLogicRegression]::CheckOfficial($graph, $skill)
    [BattleStatusLogicRegression]::CheckEdges($graph)
}
[BattleStatusLogicRegression]::CheckEventScope()
Write-Output 'Official status sequence checks passed: both eight-round graphs, counter/copy/checkpoint continuity, event Runtime sharing/reset and persistent isolation, legacy Runtime migration, captured multipliers, missing keys, empty/zero/fractional values and out-of-range indices.'

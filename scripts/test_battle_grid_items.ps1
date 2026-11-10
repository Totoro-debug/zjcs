param([string]$StageCheckpoint, [string]$StaticCheckpoint)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_grid_item_harness.cs'))
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
$state = [BattleGridItemHarness]::Run($catalog,$items,$formula)
$json = [System.Text.Json.JsonSerializer]::Serialize($state,
    [System.Text.Json.JsonSerializerOptions]@{IncludeFields=$true;MaxDepth=128})
$restored = [System.Text.Json.JsonSerializer]::Deserialize($json,
    [Zjcs.Combat.Official.FightBattleCheckpoint],
    [System.Text.Json.JsonSerializerOptions]@{IncludeFields=$true;MaxDepth=128})
[BattleGridItemHarness]::Restore($restored,$catalog,$items,$formula)
$restored = [System.Text.Json.JsonSerializer]::Deserialize($json,
    [Zjcs.Combat.Official.FightBattleCheckpoint],
    [System.Text.Json.JsonSerializerOptions]@{IncludeFields=$true;MaxDepth=128})
[BattleGridItemHarness]::CreatorDeath($restored,$catalog,$items,$formula)
if ($StageCheckpoint) {
    $stageJson = [System.Text.Json.JsonDocument]::Parse([System.IO.File]::ReadAllText($StageCheckpoint))
    try {
        $stage = [System.Text.Json.JsonSerializer]::Deserialize(
            $stageJson.RootElement.GetProperty('checkpoint').GetRawText(),
            [Zjcs.Combat.Official.FightBattleCheckpoint],
            [System.Text.Json.JsonSerializerOptions]@{IncludeFields=$true;MaxDepth=128})
        [BattleGridItemHarness]::OfficialStage($stage,$catalog,$items,$formula)
    } finally { $stageJson.Dispose() }
}
if ($StaticCheckpoint) {
    [BattleGridItemHarness]::StaticCheckpoint([IO.File]::ReadAllText($StaticCheckpoint),$catalog,$items,$formula)
}
Write-Output 'Grid checks passed: original burning/static cells, terrain and battlefield bounds, opposing overwrite, independent snapshot, creator/applicator, actual damage and delays, saved expiry/death, and nine independent hits on one large body.'

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
if (-not $env:ZJCS_VALIDATION_DIR) { throw 'Set ZJCS_VALIDATION_DIR to this session workspace.' }
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_basic_hemophagia_harness.cs'))
$inputs = Join-Path (Split-Path -Parent $PSScriptRoot) 'unity/Assets/Official/Combat'
function Read-Input($name) { [IO.File]::ReadAllText((Join-Path $inputs $name)) }
$skill = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'skill-rules-source-manifest.json'))
$battle = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'battle-rules-source-manifest.json'))
$rules = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Input 'skill-rules.json'), $skill.OutputSha256, $skill.Manifest,
    (Read-Input 'battle-rules.json'), $battle.OutputSha256, $battle.Manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Read-Input 'rules.json'), $rules.OutputSha256, $rules.Manifest)
$formula = $catalog.CreateDamageFormula((Read-Input 'rules.json'), $rules.OutputSha256, $rules.Manifest)
[BattleBasicHemophagiaHarness]::Run($catalog, $items, $formula)

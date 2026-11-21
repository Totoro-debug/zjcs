$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'verify_fourth_profession_skills.py')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The optional C# catalog harness uses the same official runtime sources as
# the existing battle fixtures.  Keep it opt-in because the repository's
# PowerShell Add-Type fallback is an older compiler on some machines.
if ($env:ZJCS_FOURTH_PROFESSION_RUNTIME -ne '1') { exit 0 }

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) +
    (Join-Path $PSScriptRoot 'fixtures/fourth_profession_skill_catalog_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/fourth_profession_battle_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_surging_tide_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_water_blade_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_light_sword_array_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_diversion_harassment_harness.cs'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
[FourthProfessionSkillCatalogHarness]::Run($catalog, $items)
$formula = $catalog.CreateDamageFormula(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
[FourthProfessionBattleHarness]::Run($catalog, $formula, $items)
[BattleSurgingTideHarness]::Run($catalog, $formula, $items)
[BattleWaterBladeHarness]::Run($catalog, $formula, $items)
[BattleLightSwordArrayHarness]::Run($catalog, $formula, $items)
[BattleDiversionHarassmentHarness]::Run($catalog, $formula, $items)

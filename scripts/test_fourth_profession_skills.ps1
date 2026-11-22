param([int]$SkillId = 0)
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
    (Join-Path $PSScriptRoot 'fixtures/skill_rule_pack_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/fourth_profession_skill_catalog_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/fourth_profession_battle_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_surging_tide_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_water_blade_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_light_sword_array_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_diversion_harassment_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_heavenly_light_blades_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_vortex_surge_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_steel_faith_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_cold_glint_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_frigid_aura_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_hellfire_resurgence_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_sword_wind_dance_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_soul_piercer_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_fear_targeting_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_dread_shadow_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_standfast_soul_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_round_forbidden_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_tactical_adaptation_harness.cs'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
$packRoot = Join-Path $root 'unity/Assets/Game/Resources'
$packIndex = Get-Content (Join-Path $packRoot 'Combat/skill-rule-packs.json') -Raw | ConvertFrom-Json
foreach ($resource in $packIndex.resources) {
    $pack = Join-Path $packRoot ($resource + '.json')
    $source = Get-Content (Join-Path $packRoot ($resource + '-source-manifest.json')) -Raw | ConvertFrom-Json
    [SkillRulePackHarness]::Apply($catalog, [IO.File]::ReadAllText($pack), $source.output_sha256, $source.manifest)
}
[FourthProfessionSkillCatalogHarness]::Run($catalog, $items)
$formula = $catalog.CreateDamageFormula(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
if ($SkillId -ne 0) {
    switch ($SkillId) {
        10068 { [BattleHellfireResurgenceHarness]::Run($catalog, $formula, $items); exit 0 }
        10088 { [BattleSwordWindDanceHarness]::Run($catalog, $formula, $items); exit 0 }
        10089 { [BattleSoulPiercerHarness]::Run($catalog, $formula, $items); exit 0 }
        10104 { [BattleFearTargetingHarness]::Run($catalog); [BattleFearTargetingHarness]::RunBattle($catalog, $formula, $items); [BattleDreadShadowHarness]::Run($catalog, $formula, $items); exit 0 }
        10608 { [BattleStandfastSoulHarness]::Run($catalog, $formula, $items); [BattleRoundForbiddenRegression]::Run($catalog, $formula, $items); exit 0 }
        10609 { [BattleTacticalAdaptationHarness]::Run($catalog, $formula, $items); exit 0 }
        10607 { [BattleFrigidAuraHarness]::Run($catalog, $formula, $items); exit 0 }
        default { throw "No focused fourth-profession fixture: $SkillId" }
    }
}
[FourthProfessionBattleHarness]::Run($catalog, $formula, $items)
[BattleSurgingTideHarness]::Run($catalog, $formula, $items)
[BattleWaterBladeHarness]::Run($catalog, $formula, $items)
[BattleLightSwordArrayHarness]::Run($catalog, $formula, $items)
[BattleDiversionHarassmentHarness]::Run($catalog, $formula, $items)
[BattleHeavenlyLightBladesHarness]::Run($catalog, $formula, $items)
[BattleVortexSurgeHarness]::Run($catalog, $formula, $items)
[BattleSteelFaithHarness]::Run($catalog, $formula, $items)
[BattleColdGlintHarness]::Run($catalog, $formula, $items)
[BattleFrigidAuraHarness]::Run($catalog, $formula, $items)
[BattleHellfireResurgenceHarness]::Run($catalog, $formula, $items)

[BattleSwordWindDanceHarness]::Run($catalog, $formula, $items)
[BattleSoulPiercerHarness]::Run($catalog, $formula, $items)
[BattleFearTargetingHarness]::Run($catalog)
[BattleFearTargetingHarness]::RunBattle($catalog, $formula, $items)
[BattleDreadShadowHarness]::Run($catalog, $formula, $items)
[BattleStandfastSoulHarness]::Run($catalog, $formula, $items)
[BattleRoundForbiddenRegression]::Run($catalog, $formula, $items)
[BattleTacticalAdaptationHarness]::Run($catalog, $formula, $items)

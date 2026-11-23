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
    (Join-Path $PSScriptRoot 'fixtures/battle_tactical_adaptation_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_assault_piercing_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_soul_breaker_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_fixed_percent_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_wind_orb_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_blade_feather_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_flame_momentum_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_star_burst_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_doom_torrent_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_spiral_wind_blade_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_battlecry_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_judgment_thunder_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_protective_runes_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_fire_core_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_ice_crystal_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_overload_protection_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_chaos_rune_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_heavenly_rain_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_sage_psychic_ward_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_sage_overflow_shield_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_sage_dark_blast_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_flame_soul_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_saint_guard_harness.cs'))
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
        10002 { [BattleStarBurstHarness]::Run($catalog, $formula, $items); exit 0 }
        10056 { [BattleDoomTorrentHarness]::Run($catalog, $formula, $items); exit 0 }
        10091 { [BattleSpiralWindBladeHarness]::Run($catalog, $formula, $items); exit 0 }
        10092 { [BattleBattlecryHarness]::Run($catalog, $formula, $items); exit 0 }
        10093 { [BattleJudgmentThunderHarness]::Run($catalog, $formula, $items); exit 0 }
        10102 { [BattleProtectiveRunesHarness]::Run($catalog, $formula, $items); exit 0 }
        10503 { [BattleFireCoreHarness]::Run($catalog, $formula, $items); exit 0 }
        10557 { [BattleIceCrystalHarness]::Run($catalog, $formula, $items); exit 0 }
        10573 { [BattleOverloadProtectionHarness]::Run($catalog, $formula); exit 0 }
        10059 { [BattleChaosRuneHarness]::Run($catalog, $formula, $items); exit 0 }
        10096 { [BattleHeavenlyRainHarness]::Run($catalog, $formula, $items); exit 0 }
        10077 { [BattleSagePsychicWardHarness]::Run($catalog, $formula, $items); exit 0 }
        10601 { [BattleSageOverflowShieldHarness]::Run($catalog, $formula, $items); exit 0 }
        10103 { [BattleSageDarkBlastHarness]::Run($catalog, $formula, $items); exit 0 }
        10599 { [BattleFlameSoulHarness]::Run($catalog, $formula, $items); exit 0 }
        10593 { [BattleSaintGuardHarness]::Run($catalog, $formula, $items); exit 0 }
        10543 { [BattleFlameMomentumHarness]::Run($catalog, $formula, $items); exit 0 }
        10595 { [BattleWindOrbHarness]::Run($catalog, $formula, $items); [BattleBladeFeatherHarness]::Run($catalog, $formula, $items); exit 0 }
        10592 { [BattleFixedPercentHarness]::Run($formula); [BattleSteelFaithHarness]::Run($catalog, $formula, $items); exit 0 }
        10594 { [BattleFixedPercentHarness]::Run($formula); [BattleSoulBreakerHarness]::Run($catalog, $formula, $items); exit 0 }
        10568 { [BattleAssaultPiercingHarness]::Run($catalog, $formula, $items); exit 0 }
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
[BattleAssaultPiercingHarness]::Run($catalog, $formula, $items)
[BattleFixedPercentHarness]::Run($formula)
[BattleSoulBreakerHarness]::Run($catalog, $formula, $items)

[BattleWindOrbHarness]::Run($catalog, $formula, $items)
[BattleBladeFeatherHarness]::Run($catalog, $formula, $items)

[BattleFlameMomentumHarness]::Run($catalog, $formula, $items)
[BattleStarBurstHarness]::Run($catalog, $formula, $items)
[BattleDoomTorrentHarness]::Run($catalog, $formula, $items)
[BattleSpiralWindBladeHarness]::Run($catalog, $formula, $items)
[BattleBattlecryHarness]::Run($catalog, $formula, $items)
[BattleJudgmentThunderHarness]::Run($catalog, $formula, $items)
[BattleProtectiveRunesHarness]::Run($catalog, $formula, $items)
[BattleFireCoreHarness]::Run($catalog, $formula, $items)
[BattleIceCrystalHarness]::Run($catalog, $formula, $items)
[BattleOverloadProtectionHarness]::Run($catalog, $formula)
[BattleChaosRuneHarness]::Run($catalog, $formula, $items)
[BattleHeavenlyRainHarness]::Run($catalog, $formula, $items)
[BattleSagePsychicWardHarness]::Run($catalog, $formula, $items)
[BattleSageOverflowShieldHarness]::Run($catalog, $formula, $items)
[BattleSageDarkBlastHarness]::Run($catalog, $formula, $items)
[BattleFlameSoulHarness]::Run($catalog, $formula, $items)
[BattleSaintGuardHarness]::Run($catalog, $formula, $items)

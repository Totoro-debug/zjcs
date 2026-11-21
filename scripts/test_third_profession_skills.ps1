$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'verify_third_profession_skills.py')

# The optional C# catalog harness uses the same official runtime sources as the
# existing battle fixtures. The repository's PowerShell Add-Type fallback is
# an older compiler and cannot parse the current Unity C# sources; keep this
# extension opt-in so the default source verification remains deterministic.
if ($env:ZJCS_THIRD_PROFESSION_RUNTIME -ne '1') { exit 0 }

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) +
    (Join-Path $PSScriptRoot 'fixtures/third_profession_skill_catalog_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_crisis_guard_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_star_shattering_slash_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_shield_charge_harness.cs') +
    (Join-Path $PSScriptRoot 'fixtures/battle_holy_purification_harness.cs'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
[ThirdProfessionSkillCatalogHarness]::Run($catalog, $items)
$formula = $catalog.CreateDamageFormula(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
[BattleCrisisGuardHarness]::Run($catalog, $formula, $items)
[BattleStarShatteringSlashHarness]::Run($catalog, $formula, $items)
[BattleShieldChargeHarness]::Run($catalog, $formula, $items)
[BattleHolyPurificationHarness]::Run($catalog, $formula, $items)

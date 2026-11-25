param([string]$ExecutorSource = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
$sources = @(Get-FightCombatSources | ForEach-Object {
    if ($ExecutorSource -and $_ -like '*\Official\Combat\FightBattleExecutor.cs') { $ExecutorSource } else { $_ }
})
Add-Type -Path ($sources + @(
    (Join-Path $PSScriptRoot 'fixtures/battle_stone_skin_harness.cs'),
    (Join-Path $PSScriptRoot 'fixtures/battle_return_strike_harness.cs'),
    (Join-Path $PSScriptRoot 'fixtures/battle_reflection_armor_harness.cs')))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
$formula = $catalog.CreateDamageFormula(
    [IO.File]::ReadAllText((Join-Path $inputs 'rules.json')), $rp.output_sha256, $rp.manifest)
[BattleStoneSkinHarness]::Run($catalog, $formula, $items)
[BattleReturnStrikeHarness]::Run($catalog, $formula, $items)
[BattleReflectionArmorHarness]::Run($catalog, $formula, $items)

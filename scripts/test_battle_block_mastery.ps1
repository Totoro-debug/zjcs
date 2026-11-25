param([string]$ExecutorSource = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
$sources = @(Get-FightCombatSources | ForEach-Object {
    if ($ExecutorSource -and $_ -like '*\Official\Combat\FightBattleExecutor.cs') { $ExecutorSource } else { $_ }
})
Add-Type -Path ($sources + (Join-Path $PSScriptRoot 'fixtures/battle_block_mastery_harness.cs'))
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
$pack = Join-Path $root 'unity/Assets/Game/Resources/Combat/SkillRules/10587-passive.json'
$receipt = Get-Content (Join-Path $root 'unity/Assets/Game/Resources/Combat/SkillRules/10587-passive-source-manifest.json') -Raw | ConvertFrom-Json
[BattleBlockMasteryHarness]::Run($catalog, $formula, $items, [IO.File]::ReadAllText($pack), $receipt.output_sha256, $receipt.manifest)

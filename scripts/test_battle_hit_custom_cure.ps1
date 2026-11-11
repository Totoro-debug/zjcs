$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$sources = @(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_hit_custom_cure_harness.cs')
Add-Type -Path $sources
$root = Split-Path -Parent $PSScriptRoot
$inputPath = Join-Path $root 'unity/Assets/Official/Combat'
$skill = [IO.File]::ReadAllText((Join-Path $inputPath 'skill-rules.json'))
$battle = [IO.File]::ReadAllText((Join-Path $inputPath 'battle-rules.json'))
$sp = Get-Content (Join-Path $inputPath 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputPath 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skill,$sp.output_sha256,$sp.manifest,$battle,$bp.output_sha256,$bp.manifest)
$rules = [IO.File]::ReadAllText((Join-Path $inputPath 'rules.json'))
$rp = Get-Content (Join-Path $inputPath 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$formula = $catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
[BattleHitCustomCureHarness]::Run($catalog,$formula,$items)
Write-Output 'Hit custom cure passed: original passive properties, overkill, full shield, filtering, whitelist, counters, checkpoint, original self-cure and cure interception.'

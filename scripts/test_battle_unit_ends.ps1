$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + @(
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleUnitEndPlayback.cs'),
    (Join-Path $PSScriptRoot 'fixtures/battle_unit_end_harness.cs')))
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
$skill = [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json'))
$battle = [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json'))
$rules = [IO.File]::ReadAllText((Join-Path $inputs 'rules.json'))
$sp = Get-Content -LiteralPath (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content -LiteralPath (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$rp = Get-Content -LiteralPath (Join-Path $inputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skill,$sp.output_sha256,$sp.manifest,$battle,$bp.output_sha256,$bp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
$formula = $catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
[BattleUnitEndHarness]::Run($catalog,$items,$formula)
'Unit removal passed: original summon final-turn boundaries, reduced lifetime, replay/JSON receipts, retained meshes clock, ordering and no cold resurrection.'

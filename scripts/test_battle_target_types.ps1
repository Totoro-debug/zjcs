$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_target_types_harness.cs'))
$inputs = Join-Path (Split-Path -Parent $PSScriptRoot) 'unity/Assets/Official/Combat'
$skill = [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json'))
$battle = [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json'))
$sp = Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp = Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skill,$sp.output_sha256,$sp.manifest,$battle,$bp.output_sha256,$bp.manifest)
[BattleTargetTypesHarness]::Run($catalog)
Write-Output 'Public target types passed: all eleven official relations, selection/range/explicit/area interfaces, original 12980 MySummon, ownership and lifecycle exclusions.'

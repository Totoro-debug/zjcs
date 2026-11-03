$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_death_forbidden_harness.cs'))
function Read-DeathRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-DeathRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-DeathRule 'skill-rules'; $battle = Read-DeathRule 'battle-rules'; $rules = Read-DeathRule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-DeathRule $skills),'854_156383',$battle,(Hash-DeathRule $battle),'854_156383')
$formula = $catalog.CreateDamageFormula($rules,(Hash-DeathRule $rules),'854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-DeathRule $rules),'854_156383')
[BattleDeathForbiddenRegression]::Run($catalog, $formula, $items)
Write-Output 'Settled death checks passed: actual fatal hits, HP listeners, same-hit official BackToLife, independent reasons, exempt states, registration on a corpse, later revival, preserved stacks, receipt replay and exact checkpoint continuation.'

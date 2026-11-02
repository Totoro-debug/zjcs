$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_round_forbidden_harness.cs'))
function Read-ForbiddenRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-ForbiddenRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-ForbiddenRule 'skill-rules'; $battle = Read-ForbiddenRule 'battle-rules'; $rules = Read-ForbiddenRule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-ForbiddenRule $skills),'854_156383',$battle,(Hash-ForbiddenRule $battle),'854_156383')
$formula = $catalog.CreateDamageFormula($rules,(Hash-ForbiddenRule $rules),'854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-ForbiddenRule $rules),'854_156383')
[BattleRoundForbiddenRegression]::Run($catalog, $formula, $items)
Write-Output 'Round forbidden checks passed: original 25117, complete release only, normal plus one extra attempt, failed probability, listener ownership, new rounds, receipt replay and exact current/legacy checkpoint continuation.'

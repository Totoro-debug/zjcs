$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Get-FightCombatSources) + (Join-Path $root 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleStatusActionTimingCases.cs'))
function Read-ActionRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-ActionRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-ActionRule 'skill-rules'; $battle = Read-ActionRule 'battle-rules'; $rules = Read-ActionRule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-ActionRule $skills),'854_156383',$battle,(Hash-ActionRule $battle),'854_156383')
$formula = $catalog.CreateDamageFormula($rules,(Hash-ActionRule $rules),'854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-ActionRule $rules),'854_156383')
[OfficialBattleStatusActionTimingCases]::Run($catalog, $formula, $items)

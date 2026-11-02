$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_voodoo_doll_harness.cs'))
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$original = Read-Rule 'skill-rules'; $battle = Read-Rule 'battle-rules'; $rules = Read-Rule 'rules'
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-Rule $rules),'854_156383')
foreach ($mode in 0,1,2) {
    $skills = $original
    $modeBattle = $battle
    if ($mode -gt 0) {
        # Only the in-memory copy changes. Real 10140 blocks children and allows status 24203.
        $data = $original | ConvertFrom-Json -AsHashtable
        $cfg = $data.entities['10140'].FightSkillComponent
        if ($mode -eq 1) { $cfg.CanTriggerChild = $true }
        else { $cfg.CanTriggerStatusEntityIds += 24202 }
        $skills = $data | ConvertTo-Json -Depth 100 -Compress
        $battleData = $battle | ConvertFrom-Json -AsHashtable
        $battleData.entities['10140'].FightSkillComponent = $cfg
        $modeBattle = $battleData | ConvertTo-Json -Depth 100 -Compress
    }
    $catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        $skills,(Hash-Rule $skills),'854_156383',$modeBattle,(Hash-Rule $modeBattle),'854_156383')
    $formula = $catalog.CreateDamageFormula($rules,(Hash-Rule $rules),'854_156383')
    $direct = [BattleVoodooDollHarness]::RunHpTriggers($catalog,$items,$formula,$mode,$false)
    $restored = [BattleVoodooDollHarness]::RunHpTriggers($catalog,$items,$formula,$mode,$true)
    if ($direct -ne $restored) { throw "Substitute HP behavior changed after mid-battle restore: mode=$mode" }
}
Write-Output 'Substitute HP passed: original 10140 trigger gate, permitted/whitelisted HP skills, owner, same-hit time, one-shot removal, full transmission and deterministic restore.'

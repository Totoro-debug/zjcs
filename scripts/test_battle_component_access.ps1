$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Component($condition, $message) {
    if (-not $condition) { throw $message }
}

function Read-Text($path) {
    return [System.IO.File]::ReadAllText((Join-Path $root $path))
}

function Get-Sha($path) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $path))
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/skill-rules.json'), '854_156383',
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    (Get-Sha 'unity/Assets/Official/Combat/battle-rules.json'), '854_156383')

# The battle catalog already carries the status/skill sub-components.
Assert-Component ($catalog.HasComponent(26004, 'FightStatusDamageReducePerComponent')) 'Damage reduction component is not exposed by the combat catalog'
Assert-Component ($catalog.HasComponent(26004, 'FightStatusPropComponent')) 'Status prop component is not exposed by the combat catalog'
Assert-Component ($catalog.HasComponent(60031, 'FightStatusShieldPersistentComponent')) 'Shield component is not exposed by the combat catalog'
Assert-Component ($catalog.HasComponent(12860, 'FightSkillComponent')) 'Skill component is not exposed by the combat catalog'
Assert-Component (-not $catalog.HasComponent(26004, 'FightStatusShieldPersistentComponent')) 'Component lookup invented a component for an unrelated status'
Assert-Component (-not $catalog.HasComponent(999999, 'FightStatusComponent')) 'Component lookup found an unknown entity'

$reduce = [System.Collections.Generic.IReadOnlyDictionary[string, object]]$null
Assert-Component ($catalog.TryGetComponent(26004, 'FightStatusDamageReducePerComponent', [ref]$reduce)) 'Damage reduction component lookup failed'
Assert-Component ($reduce.ContainsKey('Rate') -and $reduce.ContainsKey('Damage') -and $reduce.ContainsKey('ConditionCount')) 'Damage reduction component fields are missing'
Assert-Component ([Math]::Abs([double]$reduce['Rate'] - 1.0) -lt 0.0001) 'Damage reduction rate mismatch'

$shield = [System.Collections.Generic.IReadOnlyDictionary[string, object]]$null
Assert-Component ($catalog.TryGetComponent(60031, 'FightStatusShieldPersistentComponent', [ref]$shield)) 'Shield component lookup failed'
Assert-Component ($shield.ContainsKey('Rate') -and $shield.ContainsKey('CustomShield')) 'Shield component fields are missing'

# Pinned skill payload components stay readable through the same accessors.
$status = [System.Collections.Generic.IReadOnlyDictionary[string, object]]$null
Assert-Component ($catalog.TryGetComponent(26004, 'FightStatusComponent', [ref]$status)) 'Pinned status component lookup failed'
Assert-Component ($status['DurationRound'] -eq -1 -and $status['RoundTarget'] -eq 2) 'Pinned status component values changed'

Write-Output 'Combat component access checks passed: status damage reduce, status props, shields and pinned skill components readable from the loaded catalog.'

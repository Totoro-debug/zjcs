. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)

function Assert-PropertyHp($condition, $message) {
    if (-not $condition) { throw $message }
}

function Read-Text($path) {
    return [System.IO.File]::ReadAllText((Join-Path $root $path))
}

$skillSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'))
$battleSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/battle-rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'), $skillSource.OutputSha256,
    $skillSource.Manifest, (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    $battleSource.OutputSha256, $battleSource.Manifest)
$rules = Read-Text 'unity/Assets/Official/Combat/rules.json'
$rulesHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($rules))).ToLowerInvariant()
$formula = $catalog.CreateDamageFormula($rules, $rulesHash, '854_156383')
$maxHpId = $formula.PropertyId('MaxHp')

$unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$unit.Id = 1; $unit.Hp = 1; $unit.MaxHp = 1000; $unit.LastMaxHp = 1000
$unit.BaseProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$unit.Properties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$unit.BaseProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=$maxHpId; Value=1000 })
$unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=$maxHpId; Value=1000 })
$unit.Statuses.Add($catalog.CreateStatus(60680, 1, 1))

$maxHpStatus = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$maxHpStatus.InstanceId = 70001; $maxHpStatus.ClassId = 999999
$maxHpStatus.OwnerId = 1; $maxHpStatus.CreatorId = 1
$maxHpStatus.HasPropertyComponent = $true
$maxHpStatus.StatusConfigProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$maxHpStatus.StatusProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$maxHpStatus.ResolvedProperties = [System.Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
$maxHpStatus.ResolvedProperties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id=$maxHpId; Value=1000 })
$unit.Statuses.Add($maxHpStatus)
$units = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($unit)

$updates = [System.Collections.Generic.List[string]]::new()
$hpField = [Zjcs.Combat.Official.FightBattleUnitState].GetField('HpUpdated',
    [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic)
$hpField.SetValue($unit, [System.Action[Zjcs.Combat.Official.FightBattleUnitState]]{
    param($changed)
    $property = @($changed.Properties | Where-Object { $_.Id -eq $maxHpId } | Select-Object -First 1)
    $updates.Add("$($changed.Hp):$($changed.MaxHp):$($property[0].Value)")
    [void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($changed, $catalog, $false)
})

[Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($units, $catalog, $formula)
Assert-PropertyHp ($unit.Hp -eq 2 -and $unit.MaxHp -eq 2000) 'Maximum HP increase did not preserve the current HP ratio.'
Assert-PropertyHp ($updates.Count -eq 1 -and $updates[0] -eq '2:2000:2000') 'HP listeners ran before the rebuilt maximum HP properties were visible.'
Assert-PropertyHp (($unit.Statuses | Where-Object ClassId -eq 60681).Count -eq 1) 'Maximum HP update did not synchronously run the configured 60680 -> 60681 listener.'

$unit.Statuses.Clear()
[Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($units, $catalog, $formula)
Assert-PropertyHp ($unit.Hp -eq 1 -and $unit.MaxHp -eq 1000 -and $updates.Count -eq 2) 'Maximum HP removal did not publish its adjusted HP.'
[Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($units, $catalog, $formula)
Assert-PropertyHp ($updates.Count -eq 2) 'An unchanged property rebuild emitted a duplicate HP update.'
Assert-PropertyHp ($hpField.IsNotSerialized) 'The runtime HP callback can be serialized into a checkpoint.'

Write-Output 'Property HP update checks passed: max-HP ratio changes notify after property rebuild, trigger the configured listener once, and ignore unchanged rebuilds.'

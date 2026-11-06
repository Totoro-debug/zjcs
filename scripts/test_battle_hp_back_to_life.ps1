. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-BackToLife($condition, $message) {
    if (-not $condition) { throw $message }
}

function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
$skillSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'))
$battleSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/battle-rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'), $skillSource.OutputSha256,
    $skillSource.Manifest, (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    $battleSource.OutputSha256, $battleSource.Manifest)
$unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$unit.Id = 1; $unit.Hp = 0; $unit.MaxHp = 1000
$listener = $catalog.CreateStatus(60680, 1, 1)
$unit.Statuses.Add($listener)
$events = [System.Collections.Generic.List[string]]::new()
$hpField = [Zjcs.Combat.Official.FightBattleUnitState].GetField('HpUpdated',
    [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic)
$hpField.SetValue($unit, [System.Action[Zjcs.Combat.Official.FightBattleUnitState]]{
    param($changed)
    $events.Add('hp')
    [void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::Apply($changed, $catalog, $false)
})
$statusStartedField = [Zjcs.Combat.Official.FightBattleUnitState].GetField('StatusStarted',
    [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic)
$statusStartedField.SetValue($unit, [System.Action[Zjcs.Combat.Official.FightBattleUnitState, Zjcs.Combat.Official.FightBattleStatusState]]{
    param($owner, $status)
    $events.Add([string]$status.ClassId)
})
$revive = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$revive.InstanceId = 70001; $revive.ClassId = 70001
$revive.OwnerId = 1; $revive.CreatorId = 2; $revive.RootSkillId = 70002
$revive.DurationRound = -1; $revive.CurDurationRound = -1
$revive.HasBackToLifeComponent = $true
[void][Zjcs.Combat.Official.FightStatusLifecycle]::Apply($unit, $revive, 0, $revive.InstanceId)

Assert-BackToLife ($unit.Hp -eq 1) 'BackToLife did not restore HP before publishing its update'
Assert-BackToLife (($events -join ',') -eq 'hp,60681,70001') 'BackToLife HP notification did not precede nested and outer status start notifications'
Assert-BackToLife (($unit.Statuses | Where-Object { $_.ClassId -eq 60681 }).Count -eq 1) 'BackToLife update did not synchronously run the configured HP listener'
Assert-BackToLife ($hpField.IsNotSerialized) 'HP update callback can be serialized into a checkpoint'

Write-Output 'BackToLife HP notification checks passed: threshold listeners run synchronously before status start, and the runtime callback is nonserialized.'

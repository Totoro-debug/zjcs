. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
Add-Type -Path (Get-FightCombatSources)
$root=Split-Path -Parent $PSScriptRoot
function Read-Rules($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name")) }
function Sha-Rules($name) { (Get-FileHash (Join-Path $root "unity/Assets/Official/Combat/$name") -Algorithm SHA256).Hash.ToLowerInvariant() }
function Check($condition,$message) { if(-not $condition) { throw $message } }
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Read-Rules 'skill-rules.json'),(Sha-Rules 'skill-rules.json'),'854_156383',(Read-Rules 'battle-rules.json'),(Sha-Rules 'battle-rules.json'),'854_156383')
$formula=$catalog.CreateDamageFormula((Read-Rules 'rules.json'),(Sha-Rules 'rules.json'),'854_156383')
function Unit($id,$side,$hp,$score=0) {
    $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id=$id; $u.Side=$side; $u.CharacterType=1; $u.Level=20; $u.SubRank=1
    $u.Hp=$hp; $u.MaxHp=10000; $u.EntryCombatRating=$score; $u.X=$id; $u.Y=1
    $u.Turn.Id=$id; $u.Turn.Hp=$hp; $u.Turn.Speed=100
    foreach($p in @(@(1,100),@(2,0),@(3,10000),@(32,100),@(33,100),@(203,0),@(190,0))) {
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$p[0];Value=$p[1]})
    }
    return $u
}
function Passive($u) {
    $props=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
    foreach($p in $catalog.CalculateSkillProps(25014,2,20,1).GetEnumerator()) {
        $props.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$p.Key;Value=$p.Value})
    }
    $status=$catalog.CreateStatus(25014,$u.Id,$u.Id,1,25014,10531,2,20,1,$props)
    $u.Statuses.Add($status)
    return $status
}
function Battle($roster) {
    $c=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $c.StageId=1; $c.MaxRound=10; $c.RandomSeed=73
    $c.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
    foreach($u in $roster) { $c.Units.Add($u) }
    return $c
}
# A dead holder must survive the initial outcome check, execute both originals, then heal.
$p=Unit 1 1 0 500; $e=Unit 2 2 10000
$s=Passive $p
Check ($s.RoleDieRulesSupported -and $s.RoleDieSkills.Count -eq 2) 'Original revival definition remains unsupported'
$c=Battle @($p,$e)
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($c,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
Check (-not $c.Finished -and $c.RoleDiePlayPending) 'Initial death ended battle before revival'
$result=$runner.Step(0,1)
Check ($p.Hp -gt 1 -and -not $c.Finished -and $s.RoleDieTriggeredCount -eq 1) "Dead holder did not revive/heal: hp=$($p.Hp)/$($p.MaxHp), targetable=$($p.Targetable), ended=$($p.Ended), finished=$($c.Finished), count=$($s.RoleDieTriggeredCount), casts=$($result.Event.SkillCasts.SkillEntityId -join ','), hits=$($result.Event.DamageEvents.HitEntityId -join ','), statuses=$($p.Statuses.ClassId -join ',')"
Check (($result.Event.SkillCasts.SkillEntityId -join ',') -eq '11470,11490') 'Original revival/healing order differs'
Check ($result.Event.SkillCasts[1].Delay -ge 1.2 -and $result.Event.SkillCasts[0].Slot -eq -1) 'Revival lost sequential timing or consumed a slot'
Check ($p.Statuses.ClassId -contains 24233 -and $p.Statuses.ClassId -contains 24083) 'Original revival/protection statuses missing'
Check ($p.Statuses | Where-Object { $_.ClassId -eq 24083 -and $_.ApplyingSkillItemId -eq 10531 -and $_.SourceSkillRank -eq 2 -and $_.RootSkillId -eq 25014 }) 'Revival status lost passive cultivation/source'
# Receipt replay and restore may not spend or repeat the one permitted activation.
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($c))
$restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($saved,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
Check ($restored.Step(0,1).Replayed -and -not $saved.RoleDiePlayPending) 'Revival checkpoint replay changed startup boundary'
$saved.Units[0].Hp=0
Check ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRevivalSkills($saved.Units,[Zjcs.Combat.Official.FightRandomStream]::new(73)).Count -eq 0) 'Revival fired twice after restore'
# Highest entry score, then lower id; reject enemies and non-player entities.
$p=Unit 1 1 10000 100; $s=Passive $p
$a=Unit 3 1 0 900; $b=Unit 4 1 0 900; $enemy=Unit 2 2 0 9000
$other=Unit 5 1 0 9900; $other.CharacterType=2
$c=Battle @($p,$b,$enemy,$other,$a)
$inv=[Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRevivalSkills($c.Units,[Zjcs.Combat.Official.FightRandomStream]::new(73))
Check ($inv.Count -eq 2 -and $inv[0].TargetUnitId -eq 3 -and $inv[1].TargetUnitId -eq 3) 'Revival score/type/faction/tie ordering differs'
$s.RoleDieTriggeredCount=0; $s.ForbiddenType=1
Check ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRevivalSkills($c.Units,[Zjcs.Combat.Official.FightRandomStream]::new(73)).Count -eq 0) 'Forbidden revival activated'
# A real lethal enemy hit must run revival before the last friendly unit loses the battle.
$p=Unit 1 1 50 100; $s=Passive $p; $enemy=Unit 2 2 10000
$enemy.Turn.Speed=1000
$attack=[Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$attack.EntityId=90000; $attack.ItemId=90001; $attack.Slot=0; $attack.ActionType=2; $attack.TargetType=1
$attack.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(-1,0))
$hit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.EntityId=90002; $hit.ActionType=4; $hit.DamageType=8; $hit.HasCustomDamage=$true; $hit.CustomDamage=1000
$attack.Hits.Add($hit); $enemy.Skills.Add($attack)
$enemy.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$c=Battle @($p,$enemy)
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($c,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$result=$runner.Step(0,1)
Check (-not $c.Finished -and $p.Hp -gt 1 -and $s.RoleDieTriggeredCount -eq 1 -and
    $result.Event.SkillCasts.SkillEntityId -contains 90000 -and
    (($result.Event.SkillCasts | Where-Object { $_.SkillEntityId -in @(11470,11490) }).SkillEntityId -join ',') -eq '11470,11490') "Fatal hit revival differs: hp=$($p.Hp), finished=$($c.Finished), count=$($s.RoleDieTriggeredCount), casts=$($result.Event.SkillCasts.SkillEntityId -join ','), hits=$($result.Event.DamageEvents.HitEntityId -join ','), statuses=$($p.Statuses.ClassId -join ',')"
Check ($enemy.SkillState.Slots[0].ReleasedTimes -eq 1 -and $p.SkillState.Slots.Count -eq 0) 'Revival spent an equipped release'
# A dead holder may remain dead while its original skills revive and heal another character.
$p=Unit 1 1 0 100; $s=Passive $p; $ally=Unit 3 1 0 900; $enemy=Unit 2 2 10000
$c=Battle @($p,$ally,$enemy)
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($c,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$result=$runner.Step(0,1)
Check ($p.Hp -eq 0 -and $ally.Hp -gt 1 -and $s.RoleDieTriggeredCount -eq 1 -and
    @($result.Event.SkillCasts | Where-Object { $_.ActorId -eq 1 -and $_.TargetIds[0] -eq 3 }).Count -eq 2) 'Dead holder could not revive and heal the higher-score ally'
# Multiple holders observe completed revivals, so they cannot spend both uses on one corpse.
$p=Unit 1 1 10000 100; $s=Passive $p; $ally=Unit 3 1 10000 200; $second=Passive $ally
$a=Unit 4 1 0 900; $b=Unit 5 1 0 800; $enemy=Unit 2 2 10000
$c=Battle @($p,$ally,$b,$a,$enemy)
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($c,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
$result=$runner.Step(0,1)
Check ($a.Hp -gt 1 -and $b.Hp -gt 1 -and $s.RoleDieTriggeredCount -eq 1 -and $second.RoleDieTriggeredCount -eq 1) 'Multiple holders selected an already revived target'
$revives=@($result.Event.SkillCasts | Where-Object SkillEntityId -eq 11470)
Check ($revives.Count -eq 2 -and $revives[0].TargetIds[0] -eq 4 -and $revives[1].TargetIds[0] -eq 5) 'Sequential holders lost highest remaining corpse ordering'
# Older status snapshots carried the definition but marked its server-only dispatch unsupported.
$legacy=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($c))
$legacyStatus=$legacy.Units[0].Statuses | Where-Object ClassId -eq 25014
$legacyStatus.RoleDieRulesSupported=$false; $legacyStatus.RoleDieSkills.Clear()
$restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($legacy,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog)
Check ($legacyStatus.RoleDieRulesSupported -and $legacyStatus.RoleDieSkills.Count -eq 2 -and $legacyStatus.RoleDieTriggeredCount -eq 1 -and $legacy.Units[0].EntryCombatRating -eq 100) 'Definition recovery lost the spent use or entry score'
Write-Output 'Passive 10531 passed initial dead-holder and fatal-hit revival, original healing/protection, sequential timing, cultivation/source, score/tie targeting, forbidden state and checkpoint replay.'

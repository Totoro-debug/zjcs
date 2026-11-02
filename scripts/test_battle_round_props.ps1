$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Read-PropRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-PropRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function Assert-RoundProp($condition,$message) { if (-not $condition) { throw $message } }
$skills=Read-PropRule 'skill-rules'; $battle=Read-PropRule 'battle-rules'; $rules=Read-PropRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-PropRule $skills),'854_156383',$battle,(Hash-PropRule $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-PropRule $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-PropRule $rules),'854_156383')
foreach($statusId in 25119,25206) {
    $definition=$catalog.CreateStatus($statusId,1,1,99,0,0,5,20,1,$null)
    Assert-RoundProp ($definition.RoundStartRulesSupported) "Original $statusId property-target rules are unsupported"
    $prop=$definition.RoundStartSkills[0].PropTarget
    Assert-RoundProp ($prop.PropertyId -eq 1 -and $prop.Highest -and $prop.SupportCustomFilter -and $prop.IsEnemy -eq ($statusId -eq 25206)) "Original $statusId lost its official selector"
}
function New-PropCheckpoint($statusId) {
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId=1; $cp.MaxRound=10; $cp.RandomSeed=23
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=24;Height=8;DefaultGroundType=2}
    foreach($id in 1..9) {
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id; $u.Side=$(if($id -ge 7){2}else{1}); $u.CharacterType=1; $u.X=$id+1; $u.Y=2
        $u.MaxHp=$u.LastMaxHp=$u.Hp=10000000; $u.Level=$u.SubRank=1
        if($id -eq 3){$u.CharacterType=4}
        if($id -eq 4){$u.CharacterType=3; $u.MasterId=1; $u.Turn.MasterId=1}
        if($id -eq 5){$u.Hp=0}
        if($id -eq 6){$u.Targetable=$false}
        foreach($pair in @(@(1,($id*1000)),@(2,1000),@(3,$u.MaxHp),@(4,$(if($id -eq 1){100}else{1})))) {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
        }
        $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=$(if($id -eq 1){100}else{1})
        $cp.Units.Add($u)
    }
    # Excluded state identity is fixture input, independent of its unrelated lifecycle.
    $cp.Units[2].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=24395;OwnerId=3;CreatorId=3;InstanceId=9924395;DurationRound=-1;CurDurationRound=-1})
    $cp.Units[0].Statuses.Add($catalog.CreateStatus($statusId,1,1,990000+$statusId,0,0,5,20,1,$null))
    return $cp
}
function Invoke-PropBattle($statusId,$legacy) {
    $cp=New-PropCheckpoint $statusId
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    if($legacy) {
        $json=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        $json=$json -replace '"RoundStartPropTargetRulesResolved":true,',''
        $json=$json -replace '"PropTarget":\{[^{}]*\},',''
        $json=$json -replace '"RoundStartRulesSupported":true','"RoundStartRulesSupported":false'
        $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($json)
        $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    $source=$cp.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq $statusId})
    $skillId=if($statusId -eq 25119){12180}else{68020}
    $effectId=if($statusId -eq 25119){24395}else{24673}
    $expected=if($statusId -eq 25119){2}else{9}
    $result=$runner.Step(0,1)
    $casts=@($result.Event.SkillCasts | Where-Object SkillEntityId -eq $skillId)
    Assert-RoundProp ($casts.Count -eq 1 -and $casts[0].ActorId -eq 1 -and $casts[0].TargetIds.Count -eq 1 -and $casts[0].TargetIds[0] -eq $expected) "Original $statusId selected the wrong highest eligible property target"
    Assert-RoundProp ($cp.Units[$expected-1].Statuses.ClassId -contains $effectId -and $source.RoundStartTriggeredCount -eq 1 -and $cp.Units[0].SkillState.Round -eq 1) "Original $statusId lost its StartEnd invocation or actual effect"
    $saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    Assert-RoundProp ($runner.Step(0,1).Replayed -and [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq $saved) 'Property target replay mutated the checkpoint'
    $cp=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
    $runner=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $runner.Step(0,2) | Out-Null
    Assert-RoundProp ($cp.Units[0].Statuses.Find([Predicate[Zjcs.Combat.Official.FightBattleStatusState]]{param($s) $s.ClassId -eq $statusId}).RoundStartTriggeredCount -eq 2) 'Property selector lost its counter on continuation'
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
foreach($statusId in 25119,25206) {
    Assert-RoundProp ((Invoke-PropBattle $statusId $false) -eq (Invoke-PropBattle $statusId $true)) "Legacy property-target restore diverged for $statusId"
}
# Collector boundary: final properties, include self, role types, lowest, custom
# exclusions, skill factions and action restrictions, no eligible retry and copies.
$cp=New-PropCheckpoint 25119
$source=$cp.Units[0].Statuses[0]; $trigger=$source.RoundStartSkills[0]
$units=$cp.Units; $random=[Zjcs.Combat.Official.FightRandomStream]::new(23)
function Pick-Prop { return [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectRoundStartInvocations($units[0],$units[0],$units,$random,$catalog,$source,$false,5) }
$attackScale=$catalog.PropertyType('AttackScale')
$units[1].Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$attackScale;Value=10000})
[Zjcs.Combat.Official.FightStatusPropertyRuntime]::RebuildAll($units,$catalog,$formula)
Assert-RoundProp ($units[1].BaseProperties[0].Value -eq 2000 -and $units[1].Properties[0].Value -eq 4000 -and (Pick-Prop)[0].TargetUnitId -eq 2) 'Property selector ignored the rebuilt attack scale'
$units[1].Properties[0].Value=2000
$units[0].Properties[0].Value=10000
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 1) 'IncludeApplicator or final-property ordering ignored the owner'
$trigger.PropTarget.IncludeApplicator=$false
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 2) 'IncludeApplicator=false selected the owner'
$units[2].Statuses.Clear()
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 3) 'Official NPC role filter excluded the highest eligible NPC'
$trigger.PropTarget.Highest=$false
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 2) 'Lowest property ordering failed'
$trigger.TargetWithoutStatusIds=[int[]]@(77777)
$units[1].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=77777})
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 3) 'Trigger status exclusion ran after property selection'
$units[2].Targetable=$false
$before=$source.RoundStartTriggeredCount
Assert-RoundProp (@(Pick-Prop).Count -eq 0 -and $source.RoundStartTriggeredCount -eq $before) 'No eligible property target consumed a trigger'
$units[2].Targetable=$true
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 3) 'Property selector failed to retry after a target became eligible'
# Use the existing action table against a fixture skill's target action, retaining
# official action-blocker calculation rather than hand-writing blocker identities.
$required=$catalog.GetType().GetMethod('RequiredEntity',[Reflection.BindingFlags]'Instance,NonPublic')
$component=$required.Invoke($catalog,[object[]]@([int]12180))['FightSkillComponent']
$savedAction=$component['HitTargetActionType']; $savedIgnore=$component['Ai']['IgnoreStatusArray']
$component['HitTargetActionType']=[long]50
$component['Ai']['IgnoreStatusArray']=[System.Collections.Generic.List[object]]::new()
$component['Ai']['IgnoreStatusArray'].Add([long]88888)
try {
$restricted=$catalog.CreateSkill(12180,0,0,5,20,1,$null)
Assert-RoundProp ($restricted.BlockingTargetActionTypes.Count -gt 0) 'Action restriction fixture has no official blockers'
$trigger.PropTarget.Highest=$true
$units[1].Statuses.Clear(); $units[1].Properties[0].Value=8000
$units[1].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ActionType=$restricted.BlockingTargetActionTypes[0]})
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 3) 'Target action restriction ran after property ranking'
$units[1].Statuses.Clear(); $units[1].Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{ClassId=88888})
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 3) 'Skill ignored-status filter ran after property ranking'
} finally { $component['HitTargetActionType']=$savedAction; $component['Ai']['IgnoreStatusArray']=$savedIgnore }
$trigger.PropTarget.Highest=$false
$trigger.PropTarget.IsEnemy=$true
Assert-RoundProp (@(Pick-Prop).Count -eq 0) 'Custom filter ignored the friendly skill faction'
$trigger.PropTarget.SupportCustomFilter=$false
Assert-RoundProp ((Pick-Prop)[0].TargetUnitId -eq 7) 'Disabled custom filter did not retain the explicit property selector'
$copy=$trigger.WithSource($source)
$copy.PropTarget.RoleTypes[0]=999
Assert-RoundProp ($trigger.PropTarget.RoleTypes[0] -eq 1) 'Collected property selector shared its role array with the source'
Write-Output 'Round property targets passed: originals 25119/25206, actual 24395/24673 effects, role/faction/self/alive filters, final-property highest/lowest, exclude-before-sort, empty retry, copies, receipt and legacy continuation.'

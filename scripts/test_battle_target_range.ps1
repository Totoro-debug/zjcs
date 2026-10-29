. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Range($condition, $message) {
    if (-not $condition) { throw $message }
}

function New-Unit($id, $side, $x, $y, $hp, $attack) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.X = $x; $unit.Y = $y
    $unit.Hp = $hp; $unit.MaxHp = 100
    $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = $attack })
    return $unit
}

function New-Skill($range) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = 1; $skill.ItemId = 1; $skill.TargetType = 1
    foreach ($point in $range) { $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new($point[0], $point[1])) }
    return $skill
}

$actor = New-Unit 1 1 0 0 100 10
$near = New-Unit 2 2 1 0 100 10
$far = New-Unit 3 2 2 0 10 10
$outside = New-Unit 4 2 3 0 100 10
$units = [Zjcs.Combat.Official.FightBattleUnitState[]]@($actor, $near, $far, $outside)

# The configured Range order (near to far) decides the default target, not the lowest HP ratio.
$ordered = New-Skill @(@(1, 0), @(2, 0))
$selected = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $ordered)
Assert-Range ($selected[0].Id -eq 2) 'Range order did not prefer the nearer target'
$inRange = [Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange($actor, $units, $ordered)
Assert-Range ($inRange.Count -eq 1 -and $inRange[0].Id -eq 2) 'SelectInSkillRange did not keep the nearest enemy target'
$allOrdered = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 4, 0, $false, $false, $ordered)
Assert-Range ($allOrdered.Count -eq 4 -and $allOrdered[0].Id -eq 2 -and $allOrdered[1].Id -eq 3 -and $allOrdered[2].Id -eq 1 -and $allOrdered[3].Id -eq 4) 'All-target range ordering mismatch'

# The same list reversed follows the configured order, which official data is expected to sort.
$reversed = New-Skill @(@(2, 0), @(1, 0))
$reversedSelected = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $reversed)
Assert-Range ($reversedSelected[0].Id -eq 3) 'Range order did not follow the configured index order'

# In-range candidates are preferred over out-of-range candidates for movement planning.
$singleRangeSkill = New-Skill (, @(2, 0))
$selectedWithOutside = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $singleRangeSkill)
Assert-Range ($selectedWithOutside[0].Id -eq 3) 'In-range target was not preferred over an out-of-range target'

# An explicit AI priority property still wins over the range order.
$priority = New-Skill @(@(1, 0), @(2, 0))
$priority.PriorityProperty = 1
$priority.PriorityFindMax = $false
$prioritySelected = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $priority)
Assert-Range ($prioritySelected[0].Id -eq 2) 'Tie on the AI priority property did not fall back to range order'
$near.Properties[0].Value = 50
$prioritySelectedLow = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $priority)
Assert-Range ($prioritySelectedLow[0].Id -eq 3) 'AI priority property did not keep the lower value first'
$far.Properties[0].Value = 1
$prioritySelectedFar = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $priority)
Assert-Range ($prioritySelectedFar[0].Id -eq 3) 'AI priority property was overridden by the range order'

# Approved MoreHit precedence retains eligibility, control and the original tie breakers.
$mark = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$mark.AiMoreHit = $true
$far.Statuses.Add($mark)
$ordered.AiPriorityTypes.Add(4)
$marked = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $ordered)
Assert-Range ($marked[0].Id -eq 3) 'Marked target did not precede the original distance priority'
$taunted = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 2, $false, $false, $ordered)
Assert-Range ($taunted[0].Id -eq 2) 'MoreHit overrode the forced taunt source'
$blocked = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $true, $false, $ordered)
Assert-Range ($blocked.Count -eq 0) 'MoreHit bypassed confusion release restrictions'
$far.Targetable = $false
$ineligible = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $ordered)
Assert-Range ($ineligible[0].Id -eq 2) 'MoreHit made an untargetable unit selectable'
$far.Targetable = $true
$nearMark = [Zjcs.Combat.Official.FightBattleStatusState]::new(); $nearMark.AiMoreHit = $true
$near.Statuses.Add($nearMark)
$tied = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $ordered)
Assert-Range ($tied[0].Id -eq 2) 'Equal marked targets lost their original distance ordering'
$near.Statuses.Clear(); $far.Statuses.Clear()
$ally = New-Unit 5 1 1 0 100 10; $ally.Statuses.Add($mark)
$confusionUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($actor, $near, $far, $ally)
$confusedTargets = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $confusionUnits, 1, 0, $true, $true, $ordered)
Assert-Range ($confusedTargets[0].Id -eq 5) 'Eligible confused attacks ignored a marked ally'

# The selected target determines a cardinal facing, and a cast cannot mix directions.
$directionActor = New-Unit 11 1 5 5 100 10
$directionActor.Direction = 1
$directionTarget = New-Unit 12 2 7 5 100 10
$directionSkill = New-Skill @(@(2, 0), @(-2, 0))
$directionSelected = [Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange(
    $directionActor, [Zjcs.Combat.Official.FightBattleUnitState[]]@($directionActor, $directionTarget), $directionSkill)
Assert-Range ($directionActor.Direction -eq 3 -and $directionSelected.Count -eq 1 -and $directionSelected[0].Id -eq 12) 'Target selection did not face right before selecting the cast target'
$directionActor.Direction = 2
$directionTarget.X = 8
$directionExcluded = [Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange(
    $directionActor, [Zjcs.Combat.Official.FightBattleUnitState[]]@($directionActor, $directionTarget), $directionSkill)
Assert-Range ($directionExcluded.Count -eq 0 -and $directionActor.Direction -eq 2) 'Out-of-range directional target changed the caster facing'

$multiActor = New-Unit 21 1 5 5 100 10
$rightNear = New-Unit 22 2 6 5 100 10
$rightFar = New-Unit 23 2 7 5 100 10
$left = New-Unit 24 2 4 5 100 10
$multiSkill = New-Skill @(@(1, 0), @(2, 0), @(-1, 0))
$multiSkill.TargetType = 4
$sameFacing = [Zjcs.Combat.Official.FightBattleTargeting]::SelectInSkillRange(
    $multiActor, [Zjcs.Combat.Official.FightBattleUnitState[]]@($multiActor, $rightNear, $rightFar, $left), $multiSkill)
Assert-Range ($sameFacing.Count -eq 2 -and @($sameFacing | Where-Object { $_.X -lt $multiActor.X }).Count -eq 0) 'A directional cast mixed targets from opposite facings'

# Coverage would favor the near pair, but the confirmed marked-target priority wins first.
$coverageSkill = New-Skill @(@(1, 0), @(2, 0), @(3, 0))
$coverageSkill.AiPriorityTypes.Add(1)
$scopeHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$scopeHit.Anchor = 0; $scopeHit.TargetMode = 1
$scopeHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$scopeHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$coverageSkill.Hits.Add($scopeHit)
$withoutMark = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $coverageSkill)
$outside.Statuses.Add($mark)
$withMark = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 1, 0, $false, $false, $coverageSkill)
Assert-Range ($withoutMark[0].Id -eq 2 -and $withMark[0].Id -eq 4) 'Marked target did not precede larger hit coverage'
$heal = New-Skill @(@(1, 0), @(2, 0), @(3, 0)); $heal.TargetType=4; $heal.SkillType=3; $heal.AiPriorityTypes.Add(2)
$healingTargets = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 4, 0, $false, $false, $heal)
Assert-Range ($healingTargets[0].Id -eq 3) 'An attack-target mark overrode healing HP priorities'
$outside.Statuses.Clear()
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$far.Turn.Id = $far.Id; $far.Turn.Hp = $far.Hp
$far.Statuses.Add($mark); $checkpoint.StageId = 1; $checkpoint.RandomSeed = 7; $checkpoint.MaxRound = 15; $checkpoint.Units.Add($far)
$saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
Assert-Range ($saved.Units[0].Statuses[0].AiMoreHit) 'MoreHit flag did not survive persistence'
Write-Output 'Battle range targeting checks passed: configured order, AI priority, cardinal facing, out-of-range rejection and same-facing multi-target selection.'

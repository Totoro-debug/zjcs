$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
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

Write-Output 'Battle range-order targeting checks passed: near-to-far order, configured index order, in-range preference and AI priority precedence.'

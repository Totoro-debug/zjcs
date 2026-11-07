. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)
$root = Split-Path -Parent $PSScriptRoot
function Assert-LocalScope($ok, $message) { if (-not $ok) { throw $message } }
function Read-LocalScopeRule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-LocalScopeRule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-LocalScopeRule 'skill-rules'; $battle = Read-LocalScopeRule 'battle-rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-LocalScopeRule $skills),'854_156383',$battle,(Hash-LocalScopeRule $battle),'854_156383')
$skill = $catalog.CreateSkill(10630,0,10011,1,40,0,$null)
$hits = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)
Assert-LocalScope ($hits.Count -eq 3 -and $hits[0].ScopeTransform -ne $null) 'Original 10011 local-scope metadata missing'
$actor = [Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Side=1;X=10;Y=10;Hp=100;MaxHp=100}
$target = [Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;Side=2;Hp=100;MaxHp=100}
$units = [Zjcs.Combat.Official.FightBattleUnitState[]]@($actor,$target)
# Explicit golden forward positions in local Down/Left/Up/Right indices.
$forward = @(@(10,9),@(9,10),@(10,11),@(11,10))
for ($direction=0; $direction -lt 4; $direction++) {
    $actor.Direction=$direction; $target.X=$forward[$direction][0]; $target.Y=$forward[$direction][1]
    foreach ($hit in $hits) {
        $selected = [Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets($actor,$units,$skill,$target,$hit)
        Assert-LocalScope ($selected.Count -eq 1) "Original melee failed direction $direction"
    }
}
# Freeze skill-facing before later movement/listeners change the actor's facing.
$cast = [Zjcs.Combat.Official.FightBattleSkillCastPresentation]@{FacingIndex=3;FlipX=$false}
$actor.Direction=2; $target.X=11; $target.Y=10
$selected = [Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets($actor,$units,$skill,$target,$hits[0],$null,$cast)
Assert-LocalScope ($selected.Count -eq 1) 'Hit did not inherit the captured skill orientation'
# Actual failure location: caster (10,14), boss centre (13,16), body (-2..2)^2.
$actor.X=10; $actor.Y=14; $actor.Direction=3; $target.X=13; $target.Y=16
for ($x=-2;$x -le 2;$x++) { for ($y=-2;$y -le 2;$y++) { $target.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y)) } }
$selected = [Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets($actor,$units,$skill,$target,$hits[0])
Assert-LocalScope ($selected.Count -eq 1) 'Original 10011 missed the boss body at the observed failure location'
# RealCenter comes from the original half-cell hit 56051, not the caster or target centre.
$wideSkill = $catalog.CreateSkill(56050,0,0,1,40,0,$null)
$wideHit = @([Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($wideSkill) | Where-Object EntityId -eq 56051)[0]
Assert-LocalScope ($wideHit.ScopeTransform.RealCenterX -eq .5 -and $wideHit.ScopeTransform.RealCenterY -eq .5) 'Original half-cell RealCenter was lost'
$actor.X=10; $actor.Y=10; $actor.Direction=3; $actor.HasFlipX=$true; $actor.FlipX=$true
$target.BodyOffsets.Clear(); $target.X=12; $target.Y=10
$wideHit.Anchor=1
$selected=[Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets($actor,$units,$wideSkill,$target,$wideHit)
Assert-LocalScope ($selected.Count -eq 1) 'Half-cell flip/rotation did not cover the original expected cell'
$target.X=12; $target.Y=11
$selected=[Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets($actor,$units,$wideSkill,$target,$wideHit)
Assert-LocalScope ($selected.Count -eq 1) 'Half-cell second scope cell lost'
# The random selector must transform the parent node's range before choosing grid cells.
$grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=30;Height=30;DefaultGroundType=2}
$random=[Zjcs.Combat.Official.FightRandomStream]::new(7)
$selections=[Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $actor,$units,$wideSkill,$actor,$wideHit.Scope,$grid,1,$random,$false,$true,
    $false,$null,$false,$null,$false,$null,$true,$wideHit.ScopeTransform,$null)
Assert-LocalScope ($selections.Count -eq 1 -and $selections[0].Position.X -eq 12 -and $selections[0].Position.Y -eq 11) 'Random range used untransformed local cells'
$randomSkill=$catalog.CreateSkill(56880,0,0,1,40,0,$null)
$randomHits=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($randomSkill)
$randomChild=@($randomHits | Where-Object {$_.RandomTargetGroupId -gt 0})[0]
Assert-LocalScope ($randomChild.RandomTargetScopeTransform.RealCenterX -eq .5 -and $randomChild.RandomTargetScopeTransform.RealCenterY -eq .5) 'Random parent RealCenter did not reach its child hit'
# Restore a legacy checkpoint whose graph predates scope metadata without rebuilding skill state.
$actor.Skills.Add($skill); $actor.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,3))
$actor.Turn.Id=1; $actor.Turn.Hp=100; $actor.Turn.Speed=100
$target.Turn.Id=2; $target.Turn.Hp=100; $target.Turn.Speed=1
$cp=[Zjcs.Combat.Official.FightBattleCheckpoint]@{StageId=1;RandomSeed=7;Grid=$grid}
$cp.Units.Add($actor); $cp.Units.Add($target)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
$formula=[Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(),[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$settlement=[Zjcs.Combat.Official.FightDamageSettlement]::new()
$null=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,$settlement)
$actor.SkillState.Slots[0].ReleasedTimes=2
$before=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
$legacy=$before -replace '"ScopeTransform":\{"RealCenterX":0,"RealCenterY":0\}', '"ScopeTransform":null'
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($legacy)
$null=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored,$formula,$settlement,$catalog)
$restoredHits=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($restored.Units[0].Skills[0])
Assert-LocalScope ($restoredHits[0].ScopeTransform -ne $null -and $restored.Units[0].SkillState.Slots[0].ReleasedTimes -eq 2 -and $restored.Units[0].Hp -eq $actor.Hp) 'Legacy restore lost scope metadata, HP or release counters'
Write-Output 'Original local scopes passed: 10011 three hits/four directions, captured orientation, observed boss body edge, 56051 half-cell flip/rotation, random parent transform and legacy checkpoint hydration.'

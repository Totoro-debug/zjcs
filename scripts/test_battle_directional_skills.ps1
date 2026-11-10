$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (Get-FightCombatSources)
$root = Split-Path -Parent $PSScriptRoot
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function Assert-Direction($ok, $message) { if (-not $ok) { throw $message } }
$skills=Read-Rule 'skill-rules'; $battle=Read-Rule 'battle-rules'; $rules=Read-Rule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-Rule $skills),'854_156383',$battle,(Hash-Rule $battle),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Hash-Rule $rules),'854_156383')
$item=$items.Required(10026)
Assert-Direction (($item.DirectionEntityIds[0],$item.DirectionEntityIds[1],$item.DirectionEntityIds[2],$item.DirectionEntityIds[3] -join ',') -eq '11640,11660,11650,11650') 'Official four-direction mapping lost'
foreach ($case in @(@(0,1,11640,11641),@(0,-1,11660,11661),@(-1,0,11650,11651),@(1,0,11650,11651))) {
    $cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new(); $cp.StageId=1; $cp.MaxRound=5; $cp.RandomSeed=91
    $cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=10;Height=10;DefaultGroundType=2}
    foreach ($id in @(1,2)) {
        $u=[Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id=$id; $u.Side=$id; $u.CharacterType=1; $u.Hp=$u.MaxHp=$u.LastMaxHp=1000000
        $u.X=4; $u.Y=4; $u.Turn.Id=$id; $u.Turn.Hp=$u.Hp; $u.Turn.Speed=if($id -eq 1){100}else{1}
        $cp.Units.Add($u)
    }
    $p=$cp.Units[0]; $enemy=$cp.Units[1]; $enemy.X+=$case[0]; $enemy.Y+=$case[1]
    $base=$catalog.CreateSkill(10780,0,10026,2,20,1,$null)
    $base.HasCustomDamage=$true; $base.CustomDamage=100
    $p.Skills.Add($base); $p.SkillState.Slots.Add($catalog.CreateSkillSlot($base,3,-10))
    $w=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $w.UnitIds.Add(2); $cp.Waves.Add($w)
    $formula=[Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(),[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    $ex=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $before=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    $module=[Zjcs.Combat.Official.FightSkillCastModule]::new($catalog,$items)
    $targets=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new(); $targets.Add($enemy)
    $prepared=$module.Prepare($p,$base,$targets)
    $variant=$prepared.Skill
    Assert-Direction ($prepared.Cast.SkillEntityId -eq $variant.EntityId -and $prepared.Cast.TargetIds[0] -eq $enemy.Id) 'Damage definition and presentation target diverged'
    Assert-Direction ($prepared.Cast.Presentation.FacingIndex -eq ($case[1] -gt 0 ? 2 : $case[1] -lt 0 ? 0 : $case[0] -lt 0 ? 1 : 3)) 'Presentation lost common-to-local four-direction conversion'
    Assert-Direction ($prepared.Cast.Presentation.FlipX -eq ($case[0] -lt 0)) 'Presentation lost left/right mirror'
    Assert-Direction ($variant.EntityId -eq $case[2] -and $variant.RootEntityId -eq 10780 -and $variant.Rank -eq 2 -and $variant.Level -eq 20) 'Variant lost root or cultivation'
    $expected=$catalog.CalculateSkillProps($case[2],2,20,1)
    foreach ($prop in $variant.Properties) { Assert-Direction ($expected[$prop.Id] -eq $prop.Value) 'Variant reused default entity properties' }
    Assert-Direction ([object]::ReferenceEquals($base,$module.Prepare($p,$base,$null).Skill)) 'Missing target did not preserve default'
    $targets.Clear()
    Assert-Direction ([object]::ReferenceEquals($base,$module.Prepare($p,$base,$targets).Skill)) 'Empty target did not preserve default'
    $targets.Add($enemy)
    $saved=$item.DirectionEntityIds[$case[2] -eq 11640 ? 0 : $case[2] -eq 11660 ? 1 : $case[0] -lt 0 ? 2 : 3]
    $key=$case[2] -eq 11640 ? 0 : $case[2] -eq 11660 ? 1 : $case[0] -lt 0 ? 2 : 3
    $item.DirectionEntityIds.Remove($key) | Out-Null
    Assert-Direction ([object]::ReferenceEquals($base,$module.Prepare($p,$base,$targets).Skill)) 'Unmapped direction did not preserve default'
    $item.DirectionEntityIds[$key]=$saved
    $step=$ex.Step(0,1)
    Assert-Direction ($step.Event.SkillCasts.Count -eq 1 -and $step.Event.SkillCasts[0].SkillEntityId -eq $case[2]) "Wrong actual cast: $($case -join ',')"
    $cast=$step.Event.SkillCasts[0]
    Assert-Direction ($cast.Slot -eq 0 -and $cast.SkillItemId -eq 10026) 'Variant lost equipped provenance'
    Assert-Direction ($p.SkillState.Slots[0].ReleasedTimes -eq 1 -and $p.Skills[0].EntityId -eq 10780) 'Variant altered equipment or consumed extra releases'
    Assert-Direction ($step.Event.DamageEvents.Count -gt 0 -and $enemy.Hp -lt 1000000) 'Directional hit did not settle damage'
    $replayed=$ex.Step(0,1)
    Assert-Direction ($replayed.Replayed -and $p.SkillState.Slots[0].ReleasedTimes -eq 1) 'Step receipt consumed variant twice'
    $restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($before)
    $again=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($restored,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $repeat=$again.Step(0,1)
    Assert-Direction ($repeat.Event.SkillCasts[0].SkillEntityId -eq $case[2] -and $restored.Units[1].Hp -eq $enemy.Hp) 'Cold restore changed directional result'
}
Write-Output 'Directional skill 10026 passed four actual cast/hit paths, one slot consumption, receipt replay and cold restore.'

# Skills without entity overrides still use the same cast and presentation boundary.
$ordinary=$catalog.CreateSkill(10630,2,10011,2,20,1,$null)
$prepared=$module.Prepare($p,$ordinary,$targets)
Assert-Direction ([object]::ReferenceEquals($ordinary,$prepared.Skill) -and $prepared.Cast.SkillEntityId -eq 10630) 'Ordinary skill changed without an official direction mapping'
Assert-Direction ($prepared.Cast.TargetIds[0] -eq $enemy.Id -and $prepared.Cast.Presentation.TargetId -eq $enemy.Id) 'Ordinary targeting and visual snapshot diverged'

# A second binding exercises the public module without adding a skill-specific branch.
$other=$items.Required(10011)
$other.DirectionEntityIds=[Collections.Generic.Dictionary[int,int]]::new($item.DirectionEntityIds)
$base=$catalog.CreateSkill(10630,2,10011,2,20,1,$null)
$p.X=4; $p.Y=4; $enemy.X=5; $enemy.Y=4
$targets.Clear(); $targets.Add($enemy)
$prepared=$module.Prepare($p,$base,$targets)
Assert-Direction ($prepared.Skill.EntityId -eq 11650 -and $prepared.Cast.Slot -eq 2 -and $prepared.Cast.SkillItemId -eq 10011) 'Common module still hardcodes skill 10026'
Assert-Direction ($prepared.Skill.RootEntityId -eq 10630 -and $prepared.Cast.Presentation -ne $null) 'Generic cast lost its root or presentation snapshot'
$enemy.X=5; $enemy.Y=5
$prepared=$module.Prepare($p,$base,$targets)
Assert-Direction ($prepared.Skill.EntityId -eq 11650 -and $prepared.Cast.Presentation.FacingIndex -eq 3) 'Diagonal tie no longer follows original horizontal direction'
$enemy.X=$p.X; $enemy.Y=$p.Y
Assert-Direction ([object]::ReferenceEquals($base,$module.Prepare($p,$base,$targets).Skill)) 'Same-cell target did not preserve default'
Write-Output 'Shared cast module passed a second configured binding without skill-specific code.'

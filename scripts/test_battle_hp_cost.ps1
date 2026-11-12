. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Assert-Cost($condition, $message) { if (-not $condition) { throw $message } }
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-Rule 'skill-rules'; $battle = Read-Rule 'battle-rules'; $rules = Read-Rule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skills, (Hash-Rule $skills), '854_156383', $battle, (Hash-Rule $battle), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash-Rule $rules), '854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, (Hash-Rule $rules), '854_156383')

function Fixture($mode = 'status', $restore = $false, $canTrigger = $true, $whitelist = $false, $cost = 40000) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width=12; Height=8; DefaultGroundType=2 }
    foreach ($id in 1,2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $u.Side = $id; $u.X = $id + 2; $u.Y = 2; $u.CharacterType = 1
        $u.MaxHp = 100000; $u.Hp = $(if ($id -eq 1) {60000} else {100000})
        $u.Level = $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=1;Value=1000})
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=3;Value=100000})
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) {1000} else {1})
        $cp.Units.Add($u)
    }
    $actor = $cp.Units[0]
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.EntityId = $skill.ItemId = 990201; $skill.Slot = 0; $skill.TargetType = 1
    $skill.ActionDuration = $skill.Duration = 1
    $skill.CostType = 1; $skill.SkillCost = $cost; $skill.CanTriggerChild = $canTrigger
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
    $actor.Skills.Add($skill)
    $actor.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
    $statusId = $(if ($mode -eq 'configured') {24202} else {25067})
    if ($mode -eq 'legacy') {
        $listener = [Zjcs.Combat.Official.FightBattleStatusState]@{
            InstanceId=990400;ClassId=990400;OwnerId=1;CreatorId=1;DurationRound=-1;CurDurationRound=-1;
            HpTriggerPer=0.3;HpTriggerAtStart=$false;HpTriggerMaxCount=1
        }
        $listener.HpTriggerSkills.Add(10170)
    } else { $listener = $catalog.CreateStatus($statusId,1,1,9925067) }
    $actor.Statuses.Add($listener)
    if ($whitelist) { $skill.CanTriggerStatusEntityIds.Add($statusId) }
    if ($mode -ne 'status') {
        $skill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
            EntityId=990301;DamageId=990301;DamageType=8;HasCustomDamage=$true;CustomDamage=1;Delay=0.4
        })
    }
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0
    $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $actor = $cp.Units[0]
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    $step = $runner.Step(0,1)
    $payments = @($step.Event.DamageEvents | Where-Object IsSkillCost)
    Assert-Cost ($step.Event.ActorId -eq 1 -and $actor.Hp -eq 60000-$cost -and $payments.Count -eq $(if ($cost -gt 0) {1} else {0})) 'Skill did not pay its actual HP cost once'
    $parentCast = @($step.Event.SkillCasts | Where-Object SkillEntityId -eq 990201)
    Assert-Cost ($parentCast.Count -eq 1 -and $parentCast[0].HasSkillCostHp -eq ($cost -gt 0)) 'Actual cast lost its known cost notification or invented a zero-payment event'
    if ($cost -gt 0) {
        Assert-Cost ($parentCast[0].SkillCostBeforeHp -eq 60000 -and $parentCast[0].SkillCostAfterHp -eq 60000-$cost) 'Cast cost snapshot was replaced by subsequent HP listener results'
        $roundtrip = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $savedCast = @($roundtrip.Receipts[0].Event.SkillCasts | Where-Object SkillEntityId -eq 990201)[0]
        Assert-Cost ($savedCast.HasSkillCostHp -and $savedCast.SkillCostBeforeHp -eq 60000 -and $savedCast.SkillCostAfterHp -eq 60000-$cost) 'Checkpoint roundtrip lost the actual cast cost notification'
    }
    if ($mode -eq 'status') {
        $expected = $cost -gt 0 -and ($canTrigger -or $whitelist)
        Assert-Cost (($actor.Statuses.ClassId -contains 24315) -eq $expected) 'HP cost with no hit did not notify original 25067, or ignored its trigger policy'
    } else {
        $cast = @($step.Event.SkillCasts | Where-Object SkillEntityId -eq 10170)
        Assert-Cost ($cast.Count -eq 1 -and $cast[0].ActorId -eq 1 -and $cast[0].Delay -eq 0) 'HP cost lost its configured/legacy skill or cast-start time'
        $rootHit = @($step.Event.DamageEvents | Where-Object SkillEntityId -eq 990201 | Where-Object { -not $_.IsSkillCost })
        Assert-Cost ($rootHit.Count -eq 1 -and [Math]::Abs($rootHit[0].Delay-0.4) -lt 0.00001) 'HP cost listener changed the parent hit time'
        Assert-Cost ($step.Event.DamageEvents[0].IsSkillCost -and $step.Event.DamageEvents[1].SkillEntityId -eq 10170) 'HP cost listener did not execute between payment and parent hit'
        if ($mode -eq 'configured') { Assert-Cost ($actor.Statuses.ClassId -notcontains 24202) 'HP cost lost configured listener automatic removal' }
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
$direct = Fixture
Assert-Cost ($direct -eq (Fixture 'status' $true)) 'HP cost threshold changed after preparation restore'
[void](Fixture 'status' $false $false)
[void](Fixture 'status' $false $false $true)
[void](Fixture 'status' $false $true $false 0)
[void](Fixture 'configured')
[void](Fixture 'legacy')
Write-Output 'HP cost passed: original 25067 with no hit, configured/legacy skill before parent hit, source/time, automatic removal, trigger policy/whitelist, zero payment and deterministic restore.'

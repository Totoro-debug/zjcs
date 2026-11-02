. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Assert-Recovery($condition, $message) { if (-not $condition) { throw $message } }
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-Rule 'skill-rules'; $battle = Read-Rule 'battle-rules'; $rules = Read-Rule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skills, (Hash-Rule $skills), '854_156383', $battle, (Hash-Rule $battle), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash-Rule $rules), '854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, (Hash-Rule $rules), '854_156383')

function Fixture($restore = $false, $canTrigger = $true, $whitelist = $false, $legacy = $false, $configured = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width=12; Height=8; DefaultGroundType=2 }
    foreach ($id in 1,2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $u.Side = $id; $u.X = $id + 2; $u.Y = 2; $u.CharacterType = 1
        $u.MaxHp = 100000; $u.Hp = $(if ($id -eq 1) {40000} else {100000})
        $u.Level = $u.SubRank = 1
        foreach ($entry in @(@(1,1000), @(3,100000), @($formula.PropertyId('Speed'), $(if ($id -eq 1) {900} else {1000})), @(990999,10000))) {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$entry[0];Value=$entry[1]})
        }
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) {900} else {1000})
        $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
        $skill.EntityId = $skill.ItemId = 990200 + $id; $skill.Slot = 0; $skill.TargetType = 1
        $skill.ActionDuration = $skill.Duration = 1
        $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
        $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(-1,0))
        if ($id -eq 1) {
            $skill.CanTriggerChild = $canTrigger
            if ($whitelist) { $skill.CanTriggerStatusEntityIds.Add(25067) }
        }
        $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
        $hit.EntityId = $hit.DamageId = 990300 + $id; $hit.DamageType = 8
        $hit.HasCustomDamage = $true; $hit.CustomDamage = $(if ($id -eq 1) {50000} else {20000})
        $hit.Delay = 0.4
        if ($id -eq 1) { $hit.HemophagiaProperty = 990999 }
        $skill.Hits.Add($hit); $u.Skills.Add($skill)
        $u.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
        $cp.Units.Add($u)
    }
    $threshold = $catalog.CreateStatus(25067,1,1,9925067,25067,14521,5,20,1)
    $threshold.Origin = [Zjcs.Combat.Official.FightBattleStatusOrigin]@{
        IsSet=$true; UnitId=1; EntityKind=1; EntityId=8801; SkillItemId=14521; SkillEntityId=25067 }
    $cp.Units[0].Statuses.Add($threshold)
    $other = $catalog.CreateStatus(24315,1,1,9924315,25067,14521,5,20,1)
    $other.Origin = $threshold.Origin.Copy(); $other.Origin.EntityId = 8802; $other.StackCount = 7
    $cp.Units[0].Statuses.Add($other)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0
    $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $first = $runner.Step(0,1)
    Assert-Recovery ($first.Event.ActorId -eq 2 -and $cp.Units[0].Hp -eq 20000 -and $cp.Units[0].Statuses.ClassId -contains 24315) "Enemy hit did not activate original 25067 -> 24315 below 30% HP: actor=$($first.Event.ActorId) hp=$($cp.Units[0].Hp) statuses=$($cp.Units[0].Statuses.ClassId) hits=$($first.Event.DamageEvents.Count)"
    Assert-Recovery (@($cp.Units[0].Statuses | Where-Object ClassId -eq 24315).Count -eq 2) 'A real hit merged different equipment contributions.'
    if ($legacy) {
        # The old HP skill list has no explicit source ID. It must execute on the healed actor.
        $status = [Zjcs.Combat.Official.FightBattleStatusState]@{
            InstanceId=990400;ClassId=990400;OwnerId=1;CreatorId=1;DurationRound=-1;CurDurationRound=-1;
            HpTriggerPer=1;HpTriggerAtStart=$false;HpTriggerMaxCount=1
        }
        $status.HpTriggerSkills.Add(10170); $cp.Units[0].Statuses.Add($status)
    }
    if ($configured) {
        # Widen only this isolated copy's HP threshold, retaining its original skill invocation.
        $status = $catalog.CreateStatus(24202,1,1,9924202)
        $status.HpLessThanPercentage = 1; $status.HpLessThanTryAtStart = $false
        $cp.Units[0].Statuses.Add($status)
    }
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp,$formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    }
    $step = $runner.Step(0,2)
    $heals = @($step.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 990201 -and $_.IsCure })
    Assert-Recovery ($step.Event.ActorId -eq 1 -and $heals.Count -eq 1 -and $heals[0].SourceId -eq 1 -and $heals[0].TargetId -eq 1 -and $heals[0].AfterHp -gt 30000 -and [Math]::Abs($heals[0].Delay - 0.4) -lt 0.00001) 'Lifesteal did not heal its caster above 30% at the parent hit time'
    $shouldRemove = $canTrigger -or $whitelist
    $remaining = @($cp.Units[0].Statuses | Where-Object ClassId -eq 24315)
    Assert-Recovery ($remaining.Count -eq $(if ($shouldRemove) {1} else {2}) `
        -and @($remaining | Where-Object { $_.Origin.EntityId -eq 8802 -and $_.StackCount -eq 7 }).Count -eq 1) `
        "Original 24315 recovery cleanup ignored source ownership or trigger policy: canTrigger=$canTrigger whitelist=$whitelist"
    if ($legacy -or $configured) {
        $casts = @($step.Event.SkillCasts | Where-Object SkillEntityId -eq 10170)
        Assert-Recovery ($casts.Count -eq 1 -and $casts[0].ActorId -eq 1) 'Healed actor HP skill executed on the damage target'
        if ($configured) {
            Assert-Recovery ([Math]::Abs($casts[0].Delay - 0.4) -lt 0.00001 -and $cp.Units[0].Statuses.ClassId -notcontains 24202) 'Healed actor configured skill lost parent hit time or automatic removal'
        }
    }
    return [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
}
$direct = Fixture
Assert-Recovery ($direct -eq (Fixture $true)) 'Lifesteal HP notification changed after mid-battle restore'
[void](Fixture $false $false)
[void](Fixture $false $false $true)
[void](Fixture $false $true $false $true)
[void](Fixture $false $true $false $false $true)
Write-Output 'HP recovery passed: real hit activates original 25067 -> 24315; lifesteal removes only its own contribution; other equipment, trigger policy, whitelist, actor skill source, parent hit time and deterministic restore.'

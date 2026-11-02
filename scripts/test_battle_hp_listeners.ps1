. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Assert-HpListener($condition, $message) { if (-not $condition) { throw $message } }
function Read-Rule($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rule($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skills = Read-Rule 'skill-rules'; $battle = Read-Rule 'battle-rules'; $rules = Read-Rule 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skills, (Hash-Rule $skills), '854_156383', $battle, (Hash-Rule $battle), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash-Rule $rules), '854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, (Hash-Rule $rules), '854_156383')
function Fixture($hp, $characterType = 1, $enemy = $false, $restore = $false, $late = $false, $legacy = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 8; DefaultGroundType = 2 }
    foreach ($id in 1, 2, 3) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $id; $u.Side = $(if ($id -eq 2 -or ($id -eq 3 -and $enemy)) { 2 } else { 1 })
        $u.X = $id + 2; $u.Y = 2; $u.CharacterType = $(if ($id -eq 3) { $characterType } else { 1 })
        $u.Hp = $(if ($id -eq 3) { $hp } else { 100000 }); $u.MaxHp = 100000
        $u.Level = $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 1000 })
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 3; Value = 100000 })
        foreach ($base in 'BaseCureAdd', 'BaseBeCureAdd') {
            $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $formula.PropertyId($base); Value = 10000 })
        }
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        $cp.Units.Add($u)
    }
    $status = $catalog.CreateStatus(24478, 1, 1, 24478, 0, 0, 5, 20, 1, $null)
    $status.StackCount = 5
    $cp.Units[0].Statuses.Add($status)
    if ($late) {
        $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
        $skill.EntityId = $skill.ItemId = 990200; $skill.Slot = 0; $skill.TargetType = 1
        $skill.ActionDuration = $skill.Duration = 1
        $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
        $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
        $hit.EntityId = $hit.DamageId = 990201; $hit.DamageType = 8
        $hit.HasCustomDamage = $true; $hit.CustomDamage = 60000; $hit.Delay = 0.4
        $skill.Hits.Add($hit); $cp.Units[1].Skills.Add($skill)
        $cp.Units[1].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
        $cp.Units[1].Turn.Speed = 1000
        $cp.Units[1].Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $formula.PropertyId('Speed'); Value = 1000 })
    }
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0
    $wave.UnitIds.Add(2); if ($enemy) { $wave.UnitIds.Add(3) }; $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog, $items)
    if ($restore) {
        $payload = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
        if ($legacy) {
            $payload = $payload -replace '"HpLessThanChangeStack(?:Resolved|Rate|Unit)":(?:true|false|-?[0-9]+(?:\.[0-9]+)?),', ''
        }
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($payload)
        $status = $cp.Units[0].Statuses[0]
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog, $items)
    }
    $step = $runner.Step(0, 1)
    $heals = @($step.Event.DamageEvents | Where-Object SkillEntityId -eq 12620)
    if ($late) {
        Assert-HpListener (@($step.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 990200 -and $_.TargetId -eq 3 }).Count -eq 1) "Late HP fixture missed its friend: actor=$($step.Event.ActorId), skills=$($step.Event.SkillCasts.SkillEntityId), targets=$($step.Event.DamageEvents.TargetId)"
        $hp = 40000
    }
    if ($enemy -or $characterType -notin 1, 4 -or $hp -gt 50000 -or $hp -le 0) {
        Assert-HpListener ($heals.Count -eq 0 -and $status.StackCount -eq 5) '24478 ignored its friend/character/living/HP listener filter'
    } else {
        Assert-HpListener ($heals.Count -gt 0) 'Original 24478 did not heal its injured friend'
        $expectedDelay = $(if ($late) { 0.4 } else { 0 })
        Assert-HpListener (@($heals | Where-Object { $_.SourceId -ne 1 -or $_.TargetId -ne 3 -or -not $_.IsCure -or [Math]::Abs($_.Delay - $expectedDelay) -gt 0.00001 }).Count -eq 0) '24478 lost its applicator, HP-change target, cure or parent hit time'
        Assert-HpListener ($cp.Units[0].Hp -eq 100000 -and $cp.Units[2].Hp -gt $hp) "24478 HP mismatch: owner=$($cp.Units[0].Hp), friend=$($cp.Units[2].Hp), initial=$hp"
        $expectedCount = [Math]::Min(5, [Math]::Floor((50000 - $hp) / $heals[0].Damage) + 1)
        Assert-HpListener ($heals[0].Damage -gt 0 -and $heals.Count -eq $expectedCount) '24478 did not recheck HP after each immediate heal'
        Assert-HpListener ($status.HpLessThanTriggeredCount -eq $heals.Count -and $status.StackCount -eq 5 - $heals.Count) '24478 did not consume one stack per invocation'
        Assert-HpListener ($cp.Units[0].Statuses.Contains($status) -eq ($status.StackCount -gt 0)) '24478 did not remove exactly when its stacks ran out'
        Assert-HpListener ($cp.Units[0].SkillState.Round -eq 0 -and $cp.Units[2].SkillState.Round -eq 0) 'HP listener advanced a personal turn'
    }
    $saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    Assert-HpListener ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save(
        [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)) -eq $saved) 'HP listener checkpoint round trip changed'
    return $saved
}
$direct = Fixture 1
$restored = Fixture 1 1 $false $true
Assert-HpListener ($direct -eq $restored) 'Preparation restore changed the original 24478 chain'
$upgraded = Fixture 1 1 $false $true $false $true
Assert-HpListener ($direct -eq $upgraded) 'Old checkpoint did not restore the missing ChangeStackCfg'
[void](Fixture 49999)
[void](Fixture 49999 4)
[void](Fixture 49999 6)
[void](Fixture 49999 1 $true)
[void](Fixture 50001)
[void](Fixture 0)
[void](Fixture 100000 1 $false $false $true)
Write-Output 'HP listeners passed: original 24478 -> 12620, injured friend, character/side/death filters, immediate HP rechecks, one stack per invocation, exhaustion, mid-hit time and deterministic restore.'

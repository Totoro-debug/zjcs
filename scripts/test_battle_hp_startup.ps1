. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
function Assert-Start($condition, $message) { if (-not $condition) { throw $message } }
function Text($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
function Hash($text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
$skillJson = Text 'skill-rules'; $battleJson = Text 'battle-rules'; $rules = Text 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash $skillJson), '854_156383', $battleJson, (Hash $battleJson), '854_156383')
$formula = $catalog.CreateDamageFormula($rules, (Hash $rules), '854_156383')
$skillItems = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, (Hash $rules), '854_156383')
function Fixture($kind, $restore = $false) {
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 8; DefaultGroundType = 2 }
    foreach ($id in 1, 2) {
        $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $u.Id = $u.Side = $id; $u.X = $id + 2; $u.Y = 2; $u.CharacterType = 1
        $u.Hp = $(if ($id -eq 1) { 30000 } else { 100000 }); $u.MaxHp = 100000
        $u.Level = $u.SubRank = 1
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = 1; Value = 1000 })
        $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        $cp.Units.Add($u)
    }
    $source = $catalog.CreateStatus(24202, 1, 1, 1, 0, 0, 6, 17, 10, $null)
    if ($kind -eq 'legacy-skill' -or $kind -eq 'legacy-summon') {
        $source.HasHpLessThan = $false
        $source.HpTriggerPer = 0.5; $source.HpTriggerAtStart = $true; $source.HpTriggerMaxCount = 1
        $source.HpTriggerSkills.Add($(if ($kind -eq 'legacy-summon') { 10710 } else { 13500 }))
    } elseif ($kind -eq 'default-limit') {
        $source = $catalog.CreateStatus(60050, 1, 1, 1)
        $source.HpLimitSkills[0].Source = $source.HpLimitSkills[0].Target = 0
        $cp.Units[0].Hp = $cp.Units[0].Turn.Hp = 1
    }
    if ($kind -eq 'hit') {
        $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
        $skill.EntityId = $skill.ItemId = 990100; $skill.Slot = 0; $skill.TargetType = 1
        $skill.ActionDuration = 1; $skill.Duration = 1.2
        $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
        $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
        $hit.EntityId = $hit.DamageId = 990101; $hit.DamageType = 8
        $hit.HasCustomDamage = $true; $hit.CustomDamage = 5; $hit.Delay = 0.4
        $hit.AppliedStatuses.Add([Zjcs.Combat.Official.FightBattleStatusApplication]@{
            StatusId = 24202; TargetType = 1; AffectedByProp = $false
        })
        $skill.Hits.Add($hit); $cp.Units[0].Skills.Add($skill)
        $cp.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    } else { $cp.Units[0].Statuses.Add($source) }
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0
    $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    if ($kind -eq 'preparation-random') {
        $cp.Units[0].SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{
            Slot = 0; Cooldown = 3; LastRound = 2
        })
        $randomCooldown = $catalog.CreateStatus(60801, 1, 1, 2)
        # Observe its real initialization draw independently of its second 10170 invocation.
        $randomCooldown.SkillCooldownModifySkills.Clear()
        $cp.Units[0].Statuses.Add($randomCooldown)
    }
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog, $skillItems)
    if ($kind -eq 'original') {
        Assert-Start ($source.HpLessThanTriggeredCount -eq 0 -and $cp.Counter -eq 0) 'Preparation consumed a startup skill before it could be presented'
    }
    if ($kind -eq 'preparation-random') {
        Assert-Start ($cp.RandomSeed -ne 23) 'Fixture did not consume a preparation cooldown random draw'
    }
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog, $skillItems)
    }
    $result = $runner.Step(0, 1)
    $expected = $(switch ($kind) { 'legacy-skill' { 13500 } 'legacy-summon' { 10710 } 'default-limit' { 60051 } default { 10170 } })
    $casts = @($result.Event.SkillCasts | Where-Object SkillEntityId -eq $expected)
    Assert-Start ($casts.Count -eq 1) "HP startup $kind lost or duplicated its configured skill $expected"
    if ($kind -eq 'legacy-skill') {
        Assert-Start ($casts[0].ActorId -eq 1 -and $casts[0].TargetIds.Contains(2)) 'Default threshold skill did not select its configured enemy target'
    } elseif ($kind -eq 'legacy-summon') {
        Assert-Start ($casts[0].TargetIds.Count -eq 0 -and
            @($cp.Units | Where-Object { $_.MonsterId -eq 3181 -and $_.MasterId -eq 1 }).Count -eq 1) 'A targetless HP startup skill did not create its original summon'
        Assert-Start ($cp.Units[0].SkillState.Round -eq 0) 'Targetless HP startup advanced a personal turn'
    } elseif ($kind -eq 'hit') {
        Assert-Start ([Math]::Abs($casts[0].Delay - 0.4) -lt 0.00001) 'Mid-hit HP startup lost its parent hit time'
    } else {
        Assert-Start ($cp.Units[0].SkillState.Round -eq 0 -and $cp.Units[1].SkillState.Round -eq 0) 'Startup advanced a personal turn'
        Assert-Start ($result.Event.DamageEvents.Count -gt 0) 'Startup omitted original hit/effect events'
    }
    $cp.Units[0].Skills.Clear()
    if (-not $cp.Finished) {
        $second = $runner.Step(0, 2)
        Assert-Start (@($second.Event.SkillCasts | Where-Object SkillEntityId -eq $expected).Count -eq 0) "Startup $kind repeated on the next normal turn"
    } else {
        Assert-Start ($runner.Step(0, 1).Replayed) 'A terminal startup receipt was not idempotent'
    }
    return $result
}
$original = Fixture 'original'
$restored = Fixture 'original' $true
Assert-Start ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($original.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored.Checkpoint)) 'HP startup changed after saving in preparation'
[void](Fixture 'legacy-skill')
[void](Fixture 'legacy-summon')
[void](Fixture 'default-limit')
[void](Fixture 'hit')
$random = Fixture 'preparation-random'
$randomRestored = Fixture 'preparation-random' $true
Assert-Start ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($random.Checkpoint) -eq
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($randomRestored.Checkpoint)) 'Initialization random draws were lost when saving before HP startup'
$legacy = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($original.Checkpoint) | ConvertFrom-Json -AsHashtable
foreach ($unit in $legacy.checkpoint.Units) { foreach ($status in $unit.Statuses) { $status.Remove('HpStartChecked') } }
$loaded = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(($legacy | ConvertTo-Json -Depth 100 -Compress))
Assert-Start (@($loaded.Units | ForEach-Object Statuses | Where-Object { -not $_.HpStartChecked }).Count -eq 0) 'Legacy statuses repeated already-completed initialization checks'
# A full-HP summon exercises registration followed by the old explicit summon-start sweep.
# Only this isolated catalog raises the threshold to 100% and allows two invocations;
# a second sweep must not spend the second invocation without emitting its skill.
$summonDocument = [System.Text.Json.JsonDocument]::Parse($battleJson)
$summonRow = $summonDocument.RootElement.GetProperty('entities').GetProperty('24202').GetRawText()
$summonDocument.Dispose()
Assert-Start ($battleJson.IndexOf($summonRow, [StringComparison]::Ordinal) -eq
    $battleJson.LastIndexOf($summonRow, [StringComparison]::Ordinal)) 'Summon fixture status is not unique'
$summonData = $summonRow | ConvertFrom-Json -AsHashtable
$summonHp = $summonData['FightStatusHpLessThanComponent']
$summonHp['HpLessThanPercentage'] = 1.0
$summonHp['MaxCount'] = 2
$summonHp['AutoDestory'] = $false
$summonHp['StatusList'] = @()
$summonJson = $battleJson.Replace($summonRow, ($summonData | ConvertTo-Json -Depth 100 -Compress))
$summonCatalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash $skillJson), '854_156383', $summonJson, (Hash $summonJson), '854_156383')
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules, (Hash $rules), '854_156383')
$summonCp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$summonCp.StageId = 1001; $summonCp.RandomSeed = 13
foreach ($id in 1, 2) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $u.Side = $id; $u.CharacterType = $id
    $u.X = $u.Y = $(if ($id -eq 1) { 5 } else { 9 })
    $u.Hp = $u.MaxHp = 100007; $u.Level = 150; $u.SubRank = 1
    foreach ($pair in @{ 1 = 10001; 2 = 10003; 3 = 100007; 4 = 101 }.GetEnumerator()) {
        $u.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $pair.Key; Value = $pair.Value })
    }
    $u.Turn.Id = $id; $u.Turn.Hp = $u.Hp; $u.Turn.Speed = $(if ($id -eq 1) { 101 } else { 1 })
    $summonCp.Units.Add($u)
}
$summonSkill = $summonCatalog.CreateSkill(10710, 0, 10001, 5, 20, 1, $null)
foreach ($hit in $summonSkill.HitGraphEntities) { if ($hit.SummonPools.Count -gt 0) { $hit.SummonStatusIds.Add(24202) } }
$summonCp.Units[0].Skills.Add($summonSkill)
$summonCp.Units[0].SkillState.Slots.Add($summonCatalog.CreateSkillSlot($summonSkill, 0))
$summonRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($summonCp, $formula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new(), $summonCatalog, $items)
$summonStep = $summonRunner.Step(0, 1)
$summoned = @($summonCp.Units | Where-Object MonsterId -eq 3181)
Assert-Start ($summoned.Count -eq 1) 'The real 10710 summon fixture did not create its spirit'
$summonStart = @($summoned[0].Statuses | Where-Object ClassId -eq 24202)
Assert-Start ($summonStart.Count -eq 1 -and $summonStart[0].HpStartChecked -and
    $summonStart[0].HpLessThanTriggeredCount -eq 1) 'Summon registration repeated the same HP startup check'
Assert-Start (@($summonStep.Event.SkillCasts | Where-Object SkillEntityId -eq 10170).Count -eq 1) 'Summon HP startup lost its skill'
Write-Output 'HP startup passed: original 24202 -> 10170, no lost preparation skill, no personal-turn advance, mid-hit delay, default HP-limit call, legacy enemy selection, targetless summon, deterministic preparation save, legacy checkpoints and one-shot real summon registration.'

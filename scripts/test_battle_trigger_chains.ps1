. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Get-FightCombatSources)
function Assert-Chain($condition, $message) { if (-not $condition) { throw $message } }
function Read-Rule($name) {
    [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json"))
}
function Hash-Text($text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
}

# Explicit synthetic graphs exercise chains longer than the shipped examples. Official rule files
# remain untouched; the production catalog loader still verifies each in-memory fixture payload.
$skillRules = Read-Rule 'skill-rules' | ConvertFrom-Json -AsHashtable
$battleRules = Read-Rule 'battle-rules' | ConvertFrom-Json -AsHashtable
$template = $skillRules.entities['10640'] | ConvertTo-Json -Depth 100 -Compress
function Add-FixtureSkill($id, $child, $extra, $canTrigger = $false) {
    $entity = $template | ConvertFrom-Json -AsHashtable
    $cfg = $entity.FightSkillComponent
    $cfg.Range = @(@{ X = 1; Y = 0 }, @{ X = -1; Y = 0 }, @{ X = 0; Y = 0 })
    $cfg.HitList = @(@{ ClassId = $id + 1; Delay = 0; ActOnSouce = 0; IgnoreAICount = $false })
    $cfg.CanTriggerChild = $canTrigger; $cfg.CanTriggerStatusEntityIds = @()
    if ($extra -gt 0) {
        $entity.FightSkillExtSkillComponent = @{ ExtSkill = @(@{
            Rate = 1; ExtSkillIdProvider = @{ FixedExtSkillId = $extra }
        }) }
    }
    $skillRules.entities["$id"] = $entity
    $battleRules.entities["$($id + 1)"] = @{ FightHitFixedComponent = @{
        DamageId = $id + 2; DamageType = 8; DamageParam = 7; DamageActionType = 4
    } }
    $battleRules.entities["$($id + 2)"] = @{ FightDamageComponent = @{
        ChildSkillCfg = @{ SkillId = $child; Rate = 1; LessThanHpPer = -1 }
        StatusList = @()
    } }
}
Add-FixtureSkill 9900100 9900200 0 $true
Add-FixtureSkill 9900200 9900300 0
Add-FixtureSkill 9900300 0 9900400
Add-FixtureSkill 9900400 0 9900500
Add-FixtureSkill 9900500 0 0
Add-FixtureSkill 9900600 0 0 $true
Add-FixtureSkill 9900700 0 0 $true
Add-FixtureSkill 9900800 0 0 $true
$skillRules.entities['9900800'].FightSkillComponent.TargetType = 0
$battleRules.entities['9900302'].FightDamageComponent.StatusList = @(@{
    StatusId = 26004; TargetType = 2; BasePercent = 1; AffectedByProp = $false; LessThanHpPer = -1
})
$skillJson = $skillRules | ConvertTo-Json -Depth 100 -Compress
$battleJson = $battleRules | ConvertTo-Json -Depth 100 -Compress
$sourceVersion = (Read-Rule 'skill-rules-source-manifest' | ConvertFrom-Json).manifest
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, (Hash-Text $skillJson), $sourceVersion,
    $battleJson, (Hash-Text $battleJson), $sourceVersion)
function Skill($id) { $catalog.CreateSkill($id, 0, 0, 6, 17, 10, $null) }
function Unit($id, $side, $x, $speed) {
    $u = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $u.Id = $id; $u.Side = $side; $u.X = $x; $u.Hp = 1000; $u.MaxHp = 1000
    $u.Level = 1; $u.SubRank = 1
    $u.Turn.Id = $id; $u.Turn.Hp = 1000; $u.Turn.Speed = $speed
    return $u
}
function Status($id, $owner) {
    $s = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $s.ClassId = $id; $s.InstanceId = $id; $s.OwnerId = $owner; $s.CreatorId = $owner
    $s.DurationRound = -1; $s.CurDurationRound = -1; $s.StartRound = 0
    $s.SourceSkillRank = 6; $s.SourceSkillLevel = 17; $s.SourceSubRank = 10
    return $s
}
function Hp-Status($id, $owner, $child) {
    $s = Status $id $owner
    $s.HpTriggerPer = 1; $s.HpTriggerMaxCount = 1; $s.HpTriggerSkills.Add($child)
    return $s
}
function New-Battle($skill, $playerStatuses = @(), $enemyStatuses = @()) {
    $p = Unit 1 1 0 100; $e = Unit 2 2 1 1
    $p.Skills.Add($skill)
    $p.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    foreach ($s in $playerStatuses) { $p.Statuses.Add($s) }
    foreach ($s in $enemyStatuses) { $e.Statuses.Add($s) }
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 23
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($p); $cp.Units.Add($e)
    $w = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $w.Index = 0; $w.UnitIds.Add(2); $cp.Waves.Add($w)
    return $cp
}
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
function Run-Battle($cp, $restore = $false) {
    $r = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    if ($restore) {
        $cp = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
            [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
        $r = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($cp, $formula,
            [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    }
    return $r.Step(0, 1)
}

$chain = Run-Battle (New-Battle (Skill 9900100))
Assert-Chain (($chain.Event.SkillCasts.SkillEntityId -join ',') -eq '9900200,9900300,9900400,9900500,9900100') "Direct child/extra chain was truncated or reordered: $($chain.Event.SkillCasts.SkillEntityId -join ','); HP $($chain.Checkpoint.Units[1].Hp)"
Assert-Chain ($chain.Event.DamageEvents.Count -eq 5 -and $chain.Checkpoint.Units[1].Hp -eq 965) 'Multi-level chain did not settle every direct hit'
Assert-Chain (@($chain.Event.DamageEvents | Where-Object { $_.SourceId -ne 1 -or $_.TargetId -ne 2 }).Count -eq 0) 'Deep children lost caster or original hit target'
$buff = $chain.Checkpoint.Units[1].Statuses | Where-Object ClassId -eq 26004 | Select-Object -First 1
Assert-Chain ($null -ne $buff -and $buff.SourceSkillRank -eq 6 -and $buff.SourceSkillLevel -eq 17 -and $buff.SourceSubRank -eq 10) 'Closed trigger flag suppressed a direct status or lost inherited cultivation'

# Target HP proc -> attacker HP proc is a second passive level, bounded by each status MaxCount.
$enemyProc = Hp-Status 9910001 2 9900600
$playerProc = Hp-Status 9910002 1 9900700
$passives = Run-Battle (New-Battle (Skill 9900600) @($playerProc) @($enemyProc))
Assert-Chain (($passives.Event.SkillCasts.ActorId -join ',') -eq '2,1,1') "Passive-to-passive chain was truncated: $($passives.Event.SkillCasts | ForEach-Object { [string]$_.ActorId + ':' + $_.SkillEntityId }); counts $($enemyProc.HpTriggeredCount)/$($playerProc.HpTriggeredCount)"
Assert-Chain ($enemyProc.HpTriggeredCount -eq 1 -and $playerProc.HpTriggeredCount -eq 1) 'Passive chain ignored configured counters'

$blockedSkill = Skill 9900600; $blockedSkill.CanTriggerChild = $false
$blocked = Hp-Status 9910010 2 9900600
$blockedResult = Run-Battle (New-Battle $blockedSkill @() @($blocked))
Assert-Chain ($blockedResult.Event.SkillCasts.Count -eq 1 -and $blocked.HpTriggeredCount -eq 0) 'Closed flag consumed or executed a blocked HP response'
$allowedSkill = Skill 9900600; $allowedSkill.CanTriggerChild = $false; $allowedSkill.CanTriggerStatusEntityIds.Add(9910010)
$allowed = Hp-Status 9910010 2 9900600
$excluded = Hp-Status 9910011 2 9900700
$allowResult = Run-Battle (New-Battle $allowedSkill @() @($allowed, $excluded))
Assert-Chain ($allowResult.Event.SkillCasts.Count -eq 2 -and $allowed.HpTriggeredCount -eq 1 -and $excluded.HpTriggeredCount -eq 0) 'Whitelist used a skill ID or let another status respond'

# Skill-start and skill-end gates happen before rolling/counting, including nested casts.
$start = Status 9910020 1
$start.HasSkillStartTrigger = $true; $start.SkillStartRulesSupported = $true
$start.SkillStartSource = 2; $start.SkillStartMaxCount = 2; $start.SkillStartActionTypes.Add(2)
$start.SkillStartSkills.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{
    EntityId = 9900800; Source = 3; Target = 2; TargetPos = 1; BasePercent = 1
})
$nested = Run-Battle (New-Battle (Skill 9900600) @($start))
Assert-Chain ($nested.Event.SkillCasts.Count -eq 3 -and $start.SkillStartTriggeredCount -eq 2) 'Nested skill-start did not continue to its configured limit'
$start2 = Status 9910021 1
$start2.HasSkillStartTrigger = $true; $start2.SkillStartRulesSupported = $true
$start2.SkillStartSource = 2; $start2.SkillStartActionTypes.Add(2)
$start2.SkillStartSkills.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{
    EntityId = 9900600; Source = 3; Target = 2; TargetPos = 1; BasePercent = 0.5
})
$end = $catalog.CreateStatus(25011, 1, 1, 3)
$closed = Skill 9900600; $closed.CanTriggerChild = $false
$closedCp = New-Battle $closed @($start2, $end)
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(12345)
$collected = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::CollectSkillStartSkills(
    $closedCp.Units[0], $closed, $closedCp.Units.GetRange(1, 1), $closedCp.Units, $rng, $catalog)
$endApplied = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses(
    $closedCp.Units[0], $closed, $closedCp.Units, $catalog, $rng)
Assert-Chain ($collected.Count -eq 0 -and $endApplied -eq 0 -and $rng.Seed -eq 12345 -and $start2.SkillStartTriggeredCount -eq 0 -and $end.SkillEndTriggeredCount -eq 0 -and $end.SkillEndMetCountProgress -eq 0) 'Blocked start/end status consumed RNG or counters'
$closed.CanTriggerStatusEntityIds.Add(25011)
Assert-Chain ([Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplySkillEndStatuses(
    $closedCp.Units[0], $closed, $closedCp.Units, $catalog, $rng) -eq 1) 'Whitelisted skill-end status did not apply'

# A blocked accumulated-damage responder neither stores damage nor consumes a threshold.
$acc = Status 9910030 2
$acc.HasHpAccDecrease = $true; $acc.HpAccUnitHp = 1; $acc.HpAccMaxCount = 1
$acc.HpAccTriggerSkills.Add([Zjcs.Combat.Official.FightBattleStatusStateTrigger]@{
    EntityId = 9900600; Source = 2; Target = 2; BasePercent = 0.5
})
$accCp = New-Battle $closed @() @($acc)
$invocations = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusStateTrigger]]::new()
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(12345)
[void][Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyAccumulatedDamage(
    $accCp.Units[1], 7, $catalog, $rng, $invocations, $closed)
Assert-Chain ($acc.HpAccDamageAccumulated -eq 0 -and $acc.HpAccTriggeredCount -eq 0 -and $rng.Seed -eq 12345 -and $invocations.Count -eq 0) 'Blocked accumulated-damage status changed state'

# Shield absorption remains defensive state; only the shield's break response is gated.
function Shield {
    $s = Status 9910040 2
    $s.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
    $s.ShieldInitialized = $true; $s.ShieldValue = 3; $s.ShieldRemainValue = 3
    $s.ShieldDestroyedSkillId = 63434
    return $s
}
$shieldClosed = Skill 9900600; $shieldClosed.CanTriggerChild = $false
$shieldBlocked = Run-Battle (New-Battle $shieldClosed @() @((Shield)))
Assert-Chain ($shieldBlocked.Event.SkillCasts.Count -eq 1 -and $shieldBlocked.Checkpoint.Units[1].Hp -eq 996 -and $shieldBlocked.Event.DamageEvents[0].ShieldDamage -eq 3) 'Blocked shield break lost absorption or executed its proc'
$shieldClosed.CanTriggerStatusEntityIds.Add(9910040)
$shieldAllowed = Run-Battle (New-Battle $shieldClosed @() @((Shield)))
$shieldBuff = $shieldAllowed.Checkpoint.Units[1].Statuses | Where-Object ClassId -eq 24658 | Select-Object -First 1
Assert-Chain ($shieldAllowed.Event.SkillCasts.Count -eq 2 -and $null -ne $shieldBuff -and $shieldBuff.SourceSkillRank -eq 6 -and $shieldBuff.SourceSkillLevel -eq 17 -and $shieldBuff.SourceSubRank -eq 10) 'Whitelisted shield break lost its status cultivation'

# Reactive reduction/falloff gates run before their own chance and progress counters.
$defender = Unit 2 2 1 1; $attacker = Unit 1 1 0 100
$reduction = Status 9910050 2
$reduction.DamageReducePer = [Zjcs.Combat.Official.FightBattleStatusDamageReducePerInfo]::new()
$reduction.DamageReducePer.Rate = 0.5
$defender.Statuses.Add($reduction)
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(12345)
$dtype = [Zjcs.Combat.Official.FightDamageTypeData]::new()
$reduced = [Zjcs.Combat.Official.FightBattleStatusDamageReducePerRuntime]::Resolve($attacker, $defender, $closed, $dtype, $false, $rng)
Assert-Chain ($reduced -eq 0 -and $rng.Seed -eq 12345 -and $reduction.DamageReducePer.ConditionProgress -eq 0 -and $reduction.DamageReducePer.TriggeredCount -eq 0) 'Blocked reduction consumed RNG or condition counts'
$closed.CanTriggerStatusEntityIds.Add(9910050); $reduction.DamageReducePer.Rate = 1
$reduced = [Zjcs.Combat.Official.FightBattleStatusDamageReducePerRuntime]::Resolve($attacker, $defender, $closed, $dtype, $false, $rng)
Assert-Chain ($reduced -gt 0 -and $reduction.DamageReducePer.TriggeredCount -eq 1) 'Whitelisted reduction did not respond'
$falloff = Status 9910051 2
$falloff.CreatorId = 1; $falloff.FalloffTarget = 4; $falloff.IsCheckSkillTrigger = $true
$falloff.Rate = 0.5; $falloff.FalloffPercent = 5000; $falloff.NumOfStart = 0
$defender.Statuses.Clear(); $defender.Statuses.Add($falloff)
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(12345)
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$amount = [Zjcs.Combat.Official.FightDamageFalloff]::Apply($rng, $attacker, $defender, $closed, $hit, -100)
Assert-Chain ($amount -eq -100 -and $rng.Seed -eq 12345 -and $falloff.Counts.Count -eq 0) 'Blocked falloff consumed RNG or cast counts'
$closed.CanTriggerStatusEntityIds.Add(9910051); $falloff.Rate = 1
$amount = [Zjcs.Combat.Official.FightDamageFalloff]::Apply($rng, $attacker, $defender, $closed, $hit, -100)
Assert-Chain ($amount -eq -50 -and $falloff.Counts.Count -eq 1) 'Whitelisted falloff did not respond'

# CanTriggerChild=true also enables a status whose component requests the skill trigger check;
# an empty whitelist must not block all such statuses as the previous implementation did.
$officialRules = Read-Rule 'rules'
$officialFormula = $catalog.CreateDamageFormula($officialRules, (Hash-Text $officialRules), $sourceVersion)
$customExecutor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new($officialFormula,
    [Zjcs.Combat.Official.FightDamageSettlement]::new())
$customHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$customHit.EntityId = 9950001; $customHit.DamageId = 9950002; $customHit.DamageType = 1
$customHit.HasCustomDamage = $true; $customHit.CustomDamage = 100
$attacker.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
    Id = $catalog.PropertyType('Attack'); Value = 100
})
$customStatus = Status 9910052 2
$customStatus.HasDamageCustomProps = $true; $customStatus.DamageCustomPropsPhase = 1
$customStatus.DamageCustomPropsPropertyId = $catalog.PropertyType('StatusDmgReducePer')
$defender.Statuses.Clear(); $defender.Statuses.Add($customStatus)
$defender.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
    Id = $customStatus.DamageCustomPropsPropertyId; Value = 10000
})
$customStatus.DamageCustomPropsRate = 0.5
$rng = [Zjcs.Combat.Official.FightRandomStream]::new(12345)
[void]$customExecutor.Apply($rng, 1, $attacker, $defender, $closed, $customHit, 0, $false)
Assert-Chain ($customStatus.DamageCustomPropsTriggeredCount -eq 0) 'Closed skill enabled an unlisted custom-property response'
$customStatus.DamageCustomPropsRate = 1
$openSkill = Skill 9900600
[void]$customExecutor.Apply([Zjcs.Combat.Official.FightRandomStream]::new(12345), 2,
    $attacker, $defender, $openSkill, $customHit, 0, $false)
Assert-Chain ($customStatus.DamageCustomPropsTriggeredCount -eq 1) 'Open skill incorrectly required an explicit custom-property whitelist entry'

$restored = Run-Battle (New-Battle (Skill 9900100)) $true
$first = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($chain.Checkpoint)
$second = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored.Checkpoint)
Assert-Chain ($first -eq $second) 'Restored chain changed events, counters, statistics or RNG'
Write-Output 'Battle trigger-chain checks passed: five-level direct graph, two passive levels, per-status limits, start/end recursion, whitelist, blocked RNG/counters, inherited cultivation and deterministic restore.'

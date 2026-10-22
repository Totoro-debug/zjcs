$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-ChildItem $combat -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
Add-Type -Path $sources

function Assert-Combat($condition, $message) {
    if (-not $condition) { throw $message }
}

function Convert-Props($source) {
    $result = [System.Collections.Generic.Dictionary[int,long]]::new()
    foreach ($property in $source.PSObject.Properties) {
        $result[[int]$property.Name] = [long]$property.Value
    }
    return ,$result
}

function New-DamageRequest($info, $schema) {
    $request = [Zjcs.Combat.Official.FightDamageFormulaRequest]::new()
    $request.Schema = $schema
    $request.SourceProps = Convert-Props $info.SourceProps
    $request.TargetProps = Convert-Props $info.TargetProps
    $request.SkillDamageBasicPropType = [int]$info.SkillDmgBasicPropType
    $request.SkillDamagePropType = [int]$info.SkillDmgPropType
    $request.SkillDamageFixedPropType = [int]$info.SkillDmgFixedPropType
    $request.SourceType = [int]$info.SourceType
    $request.TargetType = [int]$info.TargetType
    $request.SourceMasterType = [int]$info.SourceMasterType
    $request.TargetMasterType = [int]$info.TargetMasterType
    $request.SourceLevel = [int]$info.SourceLevel
    $request.TargetLevel = [int]$info.TargetLevel
    $request.SourceSubRank = [int]$info.SourceSubRank
    $request.TargetSubRank = [int]$info.TargetSubRank
    $request.SourceProfession = [int]$info.SourceProfession
    $request.TargetProfession = [int]$info.TargetProfession
    $request.SourceElementType = [int]$info.SourceEleType
    $request.TargetMonsterClassId = [int]$info.TargetMonsterClassId
    $request.TargetMonsterType = [int]$info.TargetMonsterType
    $request.TargetHp = [int]$info.TargetHp
    $request.StageType = [int]$info.StageType
    $request.IsPvp = [bool]$info.IsPvp
    $request.CombatRatingSuppression = [double]$info.CombatRatingSuppression
    $request.Distance = [int]$info.Distance
    return $request
}

$rules = Get-Content (Join-Path $root 'assets/official/rules.json') -Raw | ConvertFrom-Json
$skillRules = Get-Content (Join-Path $root 'assets/official/skill-rules.json') -Raw | ConvertFrom-Json
$battleRules = Get-Content (Join-Path $root 'assets/official/battle-rules.json') -Raw | ConvertFrom-Json
$skillSource = Get-Content (Join-Path $root 'assets/reference/skill-rules/source-manifest.json') -Raw | ConvertFrom-Json
$battleSource = Get-Content (Join-Path $root 'assets/reference/battle-rules/source-manifest.json') -Raw | ConvertFrom-Json
$skillJson = Get-Content (Join-Path $root 'assets/official/skill-rules.json') -Raw
$battleJson = Get-Content (Join-Path $root 'assets/official/battle-rules.json') -Raw
$rulesJson = Get-Content (Join-Path $root 'assets/official/rules.json') -Raw
$citySource = Get-Content (Join-Path $root 'assets/reference/official-city/source-manifest.json') -Raw | ConvertFrom-Json
$officialCatalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, $skillSource.output_sha256, $skillSource.manifest,
    $battleJson, $battleSource.output_sha256, $battleSource.manifest)
$officialSkill = $officialCatalog.CreateSkill(16100, 0, 16100, 1, 1, 0, $null)
$officialStatus = $officialCatalog.CreateStatus(16104, 2, 1, 1, 16100, 16100)
Assert-Combat ($officialCatalog.EntityCount -gt 1000) 'Official combat catalog did not merge the pinned entity tables'
Assert-Combat ($officialSkill.HitGraphRootEntityId -eq 0 -and $officialSkill.HitGraphRootEntityIds.Count -eq 1 -and $officialSkill.HitGraphRootEntityIds[0] -eq 16101 -and $officialSkill.TargetType -eq 1 -and $officialSkill.SkillTargetType -eq 0 -and $officialSkill.Range.Count -eq 1 -and $officialSkill.HitGraphEntities.Count -gt 0) 'Official skill definition, target kinds, range, or ordered hit graph root mapping mismatch'
$multiRootSkill = $officialCatalog.CreateSkill(56930, 0, 56930, 1, 1, 0, $null)
Assert-Combat (($multiRootSkill.HitGraphRootEntityIds -join ',') -eq '56131,56131,56131') 'Official repeated hit roots did not preserve their configured order'
Assert-Combat ($officialStatus.FalloffTarget -eq 1 -and $officialStatus.FalloffPercent -eq 4000 -and $officialStatus.RootSkillIds -contains 16100) 'Official falloff status mapping mismatch'
Assert-Combat ($officialStatus.DurationRound -eq 0 -and $officialStatus.StackCount -eq 1) 'Official base status fields were not instantiated'
$skillItems = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    $rulesJson, $citySource.data_sha256.'rules.json', $citySource.manifest)
$swordStarter = $skillItems.Required(10012)
$wandStarter = $skillItems.Required(10028)
Assert-Combat ($skillItems.Count -eq 1874 -and $swordStarter.EntityId -eq 10640 -and $wandStarter.EntityId -eq 10800) 'Official skill item to EC entity mapping mismatch'
Assert-Combat ($skillItems.InitialActiveSkill(200) -eq 10012 -and $skillItems.InitialActiveSkill(201) -eq 10028 -and $skillItems.InitialActiveSkill(0) -eq 10012) 'Official initial weapon skill settings mismatch'
$missingSkillItem = $null
Assert-Combat (-not $skillItems.TryGet(1, [ref]$missingSkillItem) -and $null -eq $missingSkillItem) 'Official skill item catalog resolved an absent item'
$badSkillItemCatalogRejected = $false
try {
    [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rulesJson, ('0' * 64), $citySource.manifest)
} catch { $badSkillItemCatalogRejected = $true }
Assert-Combat $badSkillItemCatalogRejected 'Official skill item catalog accepted a mismatched rules digest'
$monsterSkillProps = $officialCatalog.CalculateSkillProps(16020, 1, 101, 11)
Assert-Combat ($monsterSkillProps.Count -eq 0) 'Official monster skill property factors produced unexpected properties'
$attackPassiveFactors = [System.Collections.Generic.Dictionary[int,long]]::new()
$attackPassiveFactors[1] = 8000
$attackPassive = $officialCatalog.CalculatePassiveSkillProps(2001, 1, 2050, $attackPassiveFactors, 1, 1, 1)
Assert-Combat ($attackPassive.Count -eq 1 -and $attackPassive[1] -eq 7) 'Official passive skill attack property mismatch'
$hpPassiveFactors = [System.Collections.Generic.Dictionary[int,long]]::new()
$hpPassiveFactors[3] = 8000
$hpPassive = $officialCatalog.CalculatePassiveSkillProps(2001, 31, 2051, $hpPassiveFactors, 1, 1, 1)
Assert-Combat ($hpPassive.Count -eq 1 -and $hpPassive[3] -eq 44) 'Official passive skill MaxHp property mismatch'
$emptySourceProps = [System.Collections.Generic.Dictionary[int,long]]::new()
Assert-Combat ($officialCatalog.CalculateSkillCooldown(16020, 1, 101, 11, $emptySourceProps) -eq 0) 'Official skill cooldown was invented without a CD property'
$sourceCooldown = [System.Collections.Generic.Dictionary[int,long]]::new()
$sourceCooldown[$officialCatalog.CooldownPropertyId] = 3
Assert-Combat ($officialCatalog.CalculateSkillCooldown(16020, 1, 101, 11, $sourceCooldown) -eq 3) 'Official skill cooldown did not consume the CD property'
$configSource = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$configSource.Id = 1; $configSource.Hp = 100; $configSource.MaxHp = 100
$configTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$configTarget.Id = 2; $configTarget.Hp = 100; $configTarget.MaxHp = 100
$configTarget.Statuses.Add($officialStatus)
$configHit = [Zjcs.Combat.Official.FightBattleHitCollector]::Collect(
    $officialSkill.HitGraphEntities, $officialSkill.HitGraphRootEntityIds[0])[0]
$configCastSerial = 0
[Zjcs.Combat.Official.FightDamageFalloff]::BeginCast($officialSkill, [ref]$configCastSerial)
$officialFalloffValues = [long[]]@(
    [Zjcs.Combat.Official.FightDamageFalloff]::Apply($null, $configSource, $configTarget, $officialSkill, $configHit, -10000),
    [Zjcs.Combat.Official.FightDamageFalloff]::Apply($null, $configSource, $configTarget, $officialSkill, $configHit, -10000),
    [Zjcs.Combat.Official.FightDamageFalloff]::Apply($null, $configSource, $configTarget, $officialSkill, $configHit, -10000))
Assert-Combat (($officialFalloffValues -join ',') -eq '-10000,-6000,-3600') 'Official status 16104 did not apply its configured falloff sequence'
[Zjcs.Combat.Official.FightDamageFalloff]::EndCast(
    [Zjcs.Combat.Official.FightBattleUnitState[]]@($configSource, $configTarget), $officialSkill)
$integrationSkillData = $officialCatalog.CreateSkill(12860, 0, 12860, 1, 1, 0, $null)
Assert-Combat ($integrationSkillData.HitGraphEntities.Count -eq 6 -and $integrationSkillData.HitGraphEntities[0].ChainedHits[0] -eq 12862) 'Official skill 12860 chained hit graph mapping mismatch'
$integrationPlayer = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$integrationPlayer.Id = 1; $integrationPlayer.Side = 1; $integrationPlayer.CharacterType = 1
$integrationPlayer.Hp = 1000; $integrationPlayer.MaxHp = 1000
$integrationPlayer.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$integrationPlayer.Turn.Id = 1; $integrationPlayer.Turn.Hp = 1000; $integrationPlayer.Turn.Speed = 100
$integrationSkills = [System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialSkillBinding]]::new()
$integrationSkills.Add([Zjcs.Combat.Official.FightOfficialSkillBinding]@{
    EntityId = 12860; Slot = 0; ItemId = 12860; Rank = 1; Level = 1; Cooldown = 0
})
$officialCatalog.AssembleUnit($integrationPlayer, $integrationSkills, $null)
$integrationSkill = $integrationPlayer.Skills[0]
# The fixed damage value is a test-only player input; skill and status data stay on the pinned official catalog.
$integrationSkill.HasCustomDamage = $true; $integrationSkill.CustomDamage = 100
$integrationEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$integrationEnemy.Id = 2; $integrationEnemy.Side = 2; $integrationEnemy.CharacterType = 2
$integrationEnemy.Y = -1
$integrationEnemy.Hp = 236; $integrationEnemy.MaxHp = 236
$integrationEnemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$integrationEnemy.Turn.Id = 2; $integrationEnemy.Turn.Hp = 236; $integrationEnemy.Turn.Speed = 50
$integrationStatuses = [System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialStatusBinding]]::new()
$integrationStatuses.Add([Zjcs.Combat.Official.FightOfficialStatusBinding]@{
    ClassId = 24078; InstanceId = 1; CreatorId = 1; RootSkillId = 12860; ApplyingSkillItemId = 12860
})
$officialCatalog.AssembleUnit($integrationEnemy, $null, $integrationStatuses)
Assert-Combat ($integrationPlayer.Skills.Count -eq 1 -and $integrationPlayer.SkillState.Slots.Count -eq 1 -and $integrationEnemy.Statuses[0].FalloffPercent -eq 4000) 'Official catalog unit assembly did not attach configured skills and status components'
$badCatalogRejected = $false
try {
    [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        $skillJson, ('0' * 64), $skillSource.manifest,
        $battleJson, $battleSource.output_sha256, $battleSource.manifest)
} catch { $badCatalogRejected = $true }
Assert-Combat $badCatalogRejected 'Official catalog accepted a payload with a mismatched source digest'
$badFormulaRulesRejected = $false
try {
    $officialCatalog.CreateDamageFormula($rulesJson, ('0' * 64), $citySource.manifest)
} catch { $badFormulaRulesRejected = $true }
Assert-Combat $badFormulaRulesRejected 'Official damage formula accepted a rules payload with a mismatched source digest'
$reference = Get-Content (Join-Path $root 'assets/reference/battle-rules/damage-reference.json') -Raw | ConvertFrom-Json

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
foreach ($property in $skillRules.prop_names.PSObject.Properties) {
    $schema.Names.Add([Zjcs.Combat.Official.FightPropertyName]@{ Name = $property.Value; Id = [int]$property.Name })
}
foreach ($property in $rules.prop_cfg) {
    if ($property.Float) { $schema.FloatPropertyIds.Add([int]$property.PropType) }
}

$formula = $officialCatalog.CreateDamageFormula(
    $rulesJson, $citySource.data_sha256.'rules.json', $citySource.manifest)
$integrationCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$integrationCheckpoint.StageId = 22001; $integrationCheckpoint.RandomSeed = 1
$integrationCheckpoint.Units.Add($integrationPlayer); $integrationCheckpoint.Units.Add($integrationEnemy)
$integrationWave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$integrationWave.Index = 0; $integrationWave.UnitIds.Add(2); $integrationCheckpoint.Waves.Add($integrationWave)
$integrationBattle = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $integrationCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$integrationResult = $integrationBattle.Step(0, 1)
Assert-Combat ($integrationResult.Event.ActorId -eq 1 -and $integrationResult.Event.SkillEntityId -eq 12860) 'Configured battle did not execute official skill 12860'
Assert-Combat ($integrationEnemy.Hp -eq 0 -and $integrationCheckpoint.Outcome.Won -and $integrationCheckpoint.Finished) 'Official skill and falloff status did not produce the expected battle victory'
Assert-Combat ($integrationResult.Event.HpChanges.Count -eq 1 -and $integrationResult.Event.HpChanges[0].Before -eq 236 -and $integrationResult.Event.HpChanges[0].After -eq 0) 'Configured battle did not record the complete status-modified HP change'
$integrationRestored = [Zjcs.Combat.Official.FightBattleExecutor]::Restore(
    $integrationCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$integrationReplay = $integrationRestored.Step(0, 1)
Assert-Combat ($integrationReplay.Replayed -and $integrationEnemy.Hp -eq 0 -and $integrationCheckpoint.DamageEventSerial -eq 6) 'Configured battle checkpoint replay applied official hit nodes twice'
foreach ($vector in $reference.cases) {
    $request = New-DamageRequest $vector.info $schema
    $flags = [Zjcs.Combat.Official.FightDamageTypeData]::new()
    $flags.Blinding = [bool]$vector.flags.IsBlinding
    $flags.Dodge = [bool]$vector.flags.IsDodge
    $flags.Block = [bool]$vector.flags.IsBlock
    $flags.Critical = [bool]$vector.flags.IsCrit
    $flags.Immunity = [bool]$vector.flags.IsImmunity
    $customDamage = $null
    if ($null -ne $vector.custom) { $customDamage = [long]$vector.custom }
    $actual = $formula.Calculate($request, $flags, $customDamage, [double]$vector.confrontation)
    Assert-Combat ($actual.Damage -eq [long]$vector.expected.damage) "$($vector.name): damage mismatch"
    Assert-Combat ($actual.AbsoluteDamage -eq [long]$vector.expected.absolute) "$($vector.name): absolute damage mismatch"
    Assert-Combat ($actual.ElementAffected -eq [bool]$vector.expected.element_affect) "$($vector.name): elemental result mismatch"
}

$cureSource = [System.Collections.Generic.Dictionary[int,long]]::new()
$cureTarget = [System.Collections.Generic.Dictionary[int,long]]::new()
$cureSource[$schema.Id('MaxHp')] = 1000
$cureSource[$schema.Id('SkillCureByHp')] = 2000
$cureSource[$schema.Id('SkillFixedCure')] = 50
$cureSource[$schema.Id('CureAddPercent')] = 1000
$cureSource[$schema.Id('BaseCureAdd')] = 1
$cureSource[$schema.Id('FinalCureScale')] = 1000
$cureTarget[$schema.Id('MaxHp')] = 800
$cureTarget[$schema.Id('BeCureAddPercent')] = 500
$cureTarget[$schema.Id('BaseBeCureAdd')] = 1
$cure = $formula.CalculateCure($cureSource, $cureTarget, $schema.Id('SkillCureByHp'), $schema.Id('SkillFixedCure'))
Assert-Combat ($cure.Cure -eq 316 -and $cure.AbsoluteCure -eq 316) 'Official cure formula order or rounding mismatch'
$cureTargetProperties = $cureTarget
$cureActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$cureActor.Id = 21; $cureActor.Hp = 1000; $cureActor.MaxHp = 1000; $cureActor.CharacterType = 1
foreach ($entry in $cureSource.GetEnumerator()) {
    $cureActor.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $entry.Key; Value = $entry.Value })
}
$cureTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$cureTarget.Id = 22; $cureTarget.Hp = 10; $cureTarget.MaxHp = 100; $cureTarget.CharacterType = 1
foreach ($entry in $cureTargetProperties.GetEnumerator()) {
    $cureTarget.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $entry.Key; Value = $entry.Value })
}
$cureSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$cureSkill.SkillType = 3
$cureHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$cureHit.DamageProperty = $schema.Id('SkillCureByHp'); $cureHit.FixedDamageProperty = $schema.Id('SkillFixedCure')
$hitSettlement = [Zjcs.Combat.Official.FightDamageSettlement]::new()
$hitExecutor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new($formula, $hitSettlement)
$cureEvent = $hitExecutor.Apply($null, 800, $cureActor, $cureTarget, $cureSkill, $cureHit, 0, $false)
Assert-Combat ($cureTarget.Hp -eq 100 -and $cureEvent.Settlement.RealDamageValue -eq 90) 'Formula-to-unit cure settlement mismatch'
$directTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$directTarget.Id = 23; $directTarget.Hp = 100; $directTarget.MaxHp = 100; $directTarget.CharacterType = 2
$directHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$directHit.DamageType = 8; $directHit.HasCustomDamage = $true; $directHit.CustomDamage = 23
$directOne = $hitExecutor.Apply($null, 801, $cureActor, $directTarget, $null, $directHit, 0, $false)
$directTwo = $hitExecutor.Apply($null, 801, $cureActor, $directTarget, $null, $directHit, 0, $false)
Assert-Combat ($directTarget.Hp -eq 54 -and $directOne.Settlement.RealDamageValue -eq -23 -and $directTwo.Settlement.RealDamageValue -eq -23) 'Sequential hit HP writeback mismatch'
$falloffStatus = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$falloffStatus.InstanceId = 1; $falloffStatus.ClassId = 24102; $falloffStatus.OwnerId = 24
$falloffStatus.FalloffTarget = 1; $falloffStatus.RootSkillId = 13080
$falloffStatus.DamageId = 10021; $falloffStatus.FalloffPercent = 3000
$falloffStatus.NumOfStart = 1; $falloffStatus.MaxFalloffCount = 2
$falloffTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$falloffTarget.Id = 24; $falloffTarget.Hp = 50000; $falloffTarget.MaxHp = 50000
$falloffTarget.Statuses.Add($falloffStatus)
$falloffSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$falloffSkill.EntityId = 13080; $falloffSkill.RootEntityId = 13080; $falloffSkill.ItemId = 500
$falloffHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$falloffHit.EntityId = 56222; $falloffHit.DamageId = 10021
$castSerial = 0
[Zjcs.Combat.Official.FightDamageFalloff]::BeginCast($falloffSkill, [ref]$castSerial)
$falloffFirst = [Zjcs.Combat.Official.FightDamageFalloff]::Apply($null, $cureActor, $falloffTarget, $falloffSkill, $falloffHit, -10000)
$falloffSecond = [Zjcs.Combat.Official.FightDamageFalloff]::Apply($null, $cureActor, $falloffTarget, $falloffSkill, $falloffHit, -10000)
$falloffThird = [Zjcs.Combat.Official.FightDamageFalloff]::Apply($null, $cureActor, $falloffTarget, $falloffSkill, $falloffHit, -10000)
Assert-Combat ($falloffFirst -eq -10000 -and $falloffSecond -eq -6999 -and $falloffThird -eq -4899) 'Official consecutive hit falloff sequence mismatch'
Assert-Combat ($falloffTarget.FalloffCounts.Count -eq 1 -and $falloffTarget.FalloffCounts[0].Count -eq 3) 'Target-owned falloff counter persistence mismatch'
$falloffHit.DamageType = 8; $falloffHit.HasCustomDamage = $true; $falloffHit.CustomDamage = 10000
$falloffApplied = $hitExecutor.Apply($null, 802, $cureActor, $falloffTarget, $falloffSkill, $falloffHit, 0, $false)
Assert-Combat ($falloffApplied.Formula.Damage -eq -4899 -and $falloffTarget.Hp -eq 45101) 'Damage falloff was not applied before entity HP settlement'
[Zjcs.Combat.Official.FightDamageFalloff]::EndCast([Zjcs.Combat.Official.FightBattleUnitState[]]@($cureActor, $falloffTarget), $falloffSkill)
Assert-Combat ($falloffTarget.FalloffCounts.Count -eq 0 -and $falloffSkill.FalloffCastId -eq 0) 'Completed action retained transient falloff counters'

$random = [Zjcs.Combat.Official.FightRandomStream]::new(1)
$firstRandom = $random.Next()
Assert-Combat ($random.Seed -eq 48271 -and [Math]::Abs($firstRandom - (48271.0 / 2147483647.0)) -lt 1e-15) 'Park-Miller stream mismatch'

$first = [Zjcs.Combat.Official.FightTurnUnit]::new()
$first.Id = 1; $first.Hp = 10; $first.Speed = 100
$second = [Zjcs.Combat.Official.FightTurnUnit]::new()
$second.Id = 2; $second.Hp = 10; $second.Speed = 100
$timeline = [Zjcs.Combat.Official.FightTurnQueueState]::new()
$queue = [Zjcs.Combat.Official.FightTurnQueue]::new($timeline, [Zjcs.Combat.Official.FightTurnUnit[]]@($first, $second))
$queue.Initialize()
Assert-Combat ($timeline.Entries[0].Id -eq 1 -and $timeline.Entries[1].Id -eq 2) 'Stable same-time actor order mismatch'
$null = $queue.Next()
$queue.ChangeInterval(1, -0.5)
$queue.Finish($first)
Assert-Combat ($timeline.Entries[0].Id -eq 2 -and $timeline.Entries[1].Next -eq 25000000) 'Deferred action-bar adjustment mismatch'
$second.Hp = 0
Assert-Combat ($queue.Next().Id -eq 1 -and $timeline.Entries.Count -eq 1) 'Dead actor queue pruning mismatch'

$skillAgent = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$skillAgent.IsDriving = $true
$skill = [Zjcs.Combat.Official.FightSkillSlotState]::new()
$skill.Slot = 0; $skill.Cooldown = 3; $skill.LimitedTimes = 2; $skill.ResetCooldownAtStart = $true
$skillAgent.Slots.Add($skill)
$skillState = [Zjcs.Combat.Official.FightSkillState]::new($skillAgent)
$skillState.Initialize(0)
Assert-Combat ($skill.LastRound -eq -3 -and $skillState.LeftCooldown(0) -eq 1) 'Initial skill cooldown mismatch'
$skillState.BeginRound(1)
Assert-Combat ($skillState.LeftCooldown(0) -eq 0) 'Personal-round cooldown mismatch'
$skillState.RunSkill(0); $skillState.FinishRound()
Assert-Combat ($skillState.LeftCooldown(0) -eq 3) 'Cooldown display phase mismatch'
$skillState.RunSkill(0); $skillState.RunSkill(0)
Assert-Combat (-not $skillState.CanRelease(0)) 'Limited skill release count mismatch'

$firstVector = $reference.cases[0]
$request = New-DamageRequest $firstVector.info $schema
$settlement = [Zjcs.Combat.Official.FightDamageSettlement]::new()
$events = [System.Collections.Generic.List[string]]::new()
$settlement.add_TargetDamaged([Action[Zjcs.Combat.Official.FightDamageEvent]]{ param($event) $events.Add('target') })
$settlement.add_SourceHit([Action[Zjcs.Combat.Official.FightDamageEvent]]{ param($event) $events.Add('source') })
$settlement.add_BattleDamaged([Action[Zjcs.Combat.Official.FightDamageEvent]]{ param($event) $events.Add('battle') })
$resolver = [Zjcs.Combat.Official.FightDamageResolver]::new($formula, $settlement)
$source = [Zjcs.Combat.Official.FightEntityState]::new(1, 1000, 1000)
$target = [Zjcs.Combat.Official.FightEntityState]::new(2, 1000, 1000)
$resolved = $resolver.Resolve([Zjcs.Combat.Official.FightRandomStream]::new(1), $request, 700, $source, $target, 3)
Assert-Combat ($resolved.Formula.Damage -eq -50 -and $resolved.Settlement.RealDamageValue -eq -50 -and $target.Hp -eq 950) 'Formula-to-entity hit execution mismatch'
Assert-Combat (($events -join ',') -eq 'target,source,battle') 'Official damage callback ordering mismatch'

$combatUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$combatUnit.Id = 1; $combatUnit.Side = 1; $combatUnit.Hp = 950; $combatUnit.MaxHp = 1000
$combatUnit.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$combatUnit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$combatUnit.Turn.Id = 1
$checkpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$checkpoint.StageId = 22001; $checkpoint.RandomSeed = 48271
$checkpoint.Units.Add($combatUnit)
[Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($checkpoint)
$checkpoint.Timeline.Entries.Add([Zjcs.Combat.Official.FightTurnEntry]@{ Id = 1; Pre = 0; Next = 10 })
$checkpoint.Timeline.Entries.Add([Zjcs.Combat.Official.FightTurnEntry]@{ Id = 1; Pre = 10; Next = 20 })
$duplicateTimelineRejected = $false
try { [Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($checkpoint) }
catch { $duplicateTimelineRejected = $true }
$checkpoint.Timeline.Entries.Clear()
Assert-Combat $duplicateTimelineRejected 'Checkpoint accepted duplicate timeline entries'
$checkpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{
    Index = 0; UnitIds = [System.Collections.Generic.List[int]]::new()
})
$checkpoint.Waves[0].UnitIds.Add(999)
$unknownWaveUnitRejected = $false
try { [Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($checkpoint) }
catch { $unknownWaveUnitRejected = $true }
$checkpoint.Waves.Clear()
Assert-Combat $unknownWaveUnitRejected 'Checkpoint accepted a wave with an unknown unit id'
$combatUnit.Hp = 1001
$invalidCheckpointRejected = $false
try { [Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($checkpoint) }
catch { $invalidCheckpointRejected = $true }
Assert-Combat $invalidCheckpointRejected 'Invalid checkpoint HP state was accepted'

$actor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$actor.Id = 1; $actor.Side = 1; $actor.Hp = 100; $actor.MaxHp = 100
$actor.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$readySkillState = [Zjcs.Combat.Official.FightSkillSlotState]::new()
$readySkillState.Slot = 0; $readySkillState.LimitedTimes = -1
$actor.SkillState.Slots.Add($readySkillState)
$heal = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$heal.Slot = 0; $heal.SkillType = 3; $heal.TargetType = 2
$actor.Skills.Add($heal)
$enemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$enemy.Id = 2; $enemy.Side = 2; $enemy.Hp = 30; $enemy.MaxHp = 100
$ally = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$ally.Id = 3; $ally.Side = 1; $ally.Hp = 40; $ally.MaxHp = 100
$units = [Zjcs.Combat.Official.FightBattleUnitState[]]@($actor, $enemy, $ally)
$selected = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 2, 0, $false, $false, $heal)
Assert-Combat ($selected.Count -eq 1 -and $selected[0].Id -eq 3) 'Lowest health ally target selection mismatch'
$ally.Hp = 100
Assert-Combat ($null -eq [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($actor, $units, 1)) 'Full health team accepted an unnecessary heal'
$ally.Hp = 70
Assert-Combat ($null -ne [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($actor, $units, 1)) 'Available heal skill was not selected'
$multiple = [Zjcs.Combat.Official.FightBattleTargeting]::Select($actor, $units, 4)
$chosen = [Zjcs.Combat.Official.FightBattleTargeting]::SelectHitTargets($multiple, 2, 2, [Zjcs.Combat.Official.FightRandomStream]::new(7))
Assert-Combat ($chosen.Count -eq 2 -and $chosen[0].Id -ne $chosen[1].Id) 'Random multi-target hit repeated a target'

$hitRoot = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$hitRoot.EntityId = 10; $hitRoot.HasFixedDamage = $true; $hitRoot.ChainedHits.Add(11)
$hitRoot.CustomTargetHits.Add(12); $hitRoot.RandomTargetHits.Add(13); $hitRoot.MiniHitTargetCount = 3
$hitChain = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$hitChain.EntityId = 11; $hitChain.HasFixedDamage = $true; $hitChain.ChainedHits.Add(10)
$hitAll = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$hitAll.EntityId = 12; $hitAll.HasFixedDamage = $true
$hitRandom = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$hitRandom.EntityId = 13; $hitRandom.HasFixedDamage = $true
$hitEntities = [Zjcs.Combat.Official.FightBattleHitEntity[]]@($hitRoot, $hitChain, $hitAll, $hitRandom)
$collectedHits = [Zjcs.Combat.Official.FightBattleHitCollector]::Collect($hitEntities, 10)
Assert-Combat ($collectedHits.Count -eq 4) 'Hit graph collection count or cycle protection mismatch'
Assert-Combat (($collectedHits.EntityId -join ',') -eq '10,11,12,13') 'Hit graph traversal order mismatch'
Assert-Combat (($collectedHits.TargetMode -join ',') -eq '0,0,1,2' -and $collectedHits[3].RandomCount -eq 3) 'Hit graph target mode propagation mismatch'

$runnerPlayer = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$runnerPlayer.Id = 1; $runnerPlayer.MasterId = 0; $runnerPlayer.Side = 1
$runnerPlayer.Hp = 100; $runnerPlayer.MaxHp = 100; $runnerPlayer.CharacterType = 1
$runnerPlayer.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $schema.Id('Attack'); Value = 100 })
$runnerPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$runnerPlayer.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$runnerPlayer.Turn.Id = 1; $runnerPlayer.Turn.MasterId = 0; $runnerPlayer.Turn.Hp = 100
$runnerPlayer.Turn.Speed = 100
$runnerEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$runnerEnemy.Id = 42; $runnerEnemy.Side = 2; $runnerEnemy.Hp = 200; $runnerEnemy.MaxHp = 200
$runnerEnemy.CharacterType = 2
$runnerEnemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$runnerEnemy.Turn.Id = 42; $runnerEnemy.Turn.Hp = 200; $runnerEnemy.Turn.Speed = 100
$runnerNextWave = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$runnerNextWave.Id = 43; $runnerNextWave.Side = 2; $runnerNextWave.Hp = 100; $runnerNextWave.MaxHp = 100
$runnerNextWave.CharacterType = 2
$runnerNextWave.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$runnerNextWave.Turn.Id = 43; $runnerNextWave.Turn.Hp = 100; $runnerNextWave.Turn.Speed = 100
$runnerSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$runnerSkill.Slot = 0; $runnerSkill.EntityId = 901; $runnerSkill.ItemId = 902; $runnerSkill.TargetType = 1
$runnerSkill.ResetReleasedTimeOnNextWave = $true
$runnerSkill.HitGraphRootEntityIds.Add(903)
$runnerSkill.HitGraphRootEntityIds.Add(905)
$runnerHitNode = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$runnerHitNode.EntityId = 903; $runnerHitNode.HasFixedDamage = $true
$runnerHitNode.DamageId = 904; $runnerHitNode.DamageType = 8; $runnerHitNode.DamageParam = 100
$runnerSkill.HitGraphEntities.Add($runnerHitNode)
$runnerSecondHitNode = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$runnerSecondHitNode.EntityId = 905; $runnerSecondHitNode.HasFixedDamage = $true
$runnerSecondHitNode.DamageId = 904; $runnerSecondHitNode.DamageType = 8; $runnerSecondHitNode.DamageParam = 100
$runnerSkill.HitGraphEntities.Add($runnerSecondHitNode)
$runnerPlayer.Skills.Add($runnerSkill)
$runnerSlot = [Zjcs.Combat.Official.FightSkillSlotState]::new()
$runnerSlot.Slot = 0; $runnerSlot.LimitedTimes = 1; $runnerSlot.ResetCooldownAtStart = $true
$runnerPlayer.SkillState.Slots.Add($runnerSlot)
$runnerCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$runnerCheckpoint.StageId = 22001; $runnerCheckpoint.RandomSeed = 1; $runnerCheckpoint.Timeline.Round = 4
$runnerCheckpoint.Units.Add($runnerPlayer); $runnerCheckpoint.Units.Add($runnerEnemy); $runnerCheckpoint.Units.Add($runnerNextWave)
$runnerCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 0; UnitIds = [System.Collections.Generic.List[int]]::new() })
$runnerCheckpoint.Waves[0].UnitIds.Add(42)
$runnerCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 1; UnitIds = [System.Collections.Generic.List[int]]::new() })
$runnerCheckpoint.Waves[1].UnitIds.Add(43)
$runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($runnerCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
Assert-Combat $runnerSlot.HasInitializedLastRound 'Battle begin skipped configured skill cooldown initialization'
Assert-Combat ($runnerCheckpoint.Outcome.EnemyHp -eq 300) 'Future wave HP was counted twice in the initial outcome'
$runnerResult = $runner.Step(0, 1)
Assert-Combat ($runnerResult.Event.ActorId -eq 1 -and $runnerEnemy.Hp -eq 0 -and $runnerResult.Event.GlobalRound -eq 4) 'Battle executor did not apply the first scheduled skill hit at the current round'
Assert-Combat ($runnerCheckpoint.DamageEventSerial -eq 2) 'Battle executor did not execute every configured hit root in order'
Assert-Combat ($runnerCheckpoint.WaveIndex -eq 1 -and -not $runnerNextWave.Ended -and $runnerSlot.ReleasedTimes -eq 0) 'Next-wave spawn or configured skill-use reset mismatch'
Assert-Combat ($runnerNextWave.Turn.RoundOffset -eq 3 -and $runnerCheckpoint.Timeline.Round -eq 4) 'Spawned wave did not inherit the current global-round offset'
$restoredRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Restore($runnerResult.Checkpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$replayedStep = $restoredRunner.Step(0, 1)
Assert-Combat ($replayedStep.Replayed -and $runnerEnemy.Hp -eq 0) 'Restored battle executor replay applied a step twice'
Assert-Combat ($runnerCheckpoint.DamageEventSerial -eq 2) 'Replaying a restored step advanced the damage event serial'
$runnerPlayer.Hp = 0
$runnerPlayer.Turn.Hp = 0
$runnerEnemy.Hp = 0
$runnerEnemy.Turn.Hp = 0
$runnerNextWave.Hp = 0
$runnerNextWave.Turn.Hp = 0
$terminalStep = $restoredRunner.Step(0, 2)
Assert-Combat ($terminalStep.Event.Kind -eq 3 -and $terminalStep.Checkpoint.Counter -eq 2) 'Terminal battle step did not persist a replayable receipt'
$replayedTerminal = $restoredRunner.Step(0, 2)
Assert-Combat ($replayedTerminal.Replayed -and $replayedTerminal.Event.Kind -eq 3) 'Terminal step receipt was not idempotent'

$multiActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$multiActor.Id = 1; $multiActor.Side = 1; $multiActor.Hp = 50000; $multiActor.MaxHp = 50000
$multiActor.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$multiActor.Turn.Id = 1; $multiActor.Turn.Hp = 50000; $multiActor.Turn.Speed = 100
$multiActor.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$multiActor.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$multiSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$multiSkill.Slot = 0; $multiSkill.EntityId = 905; $multiSkill.RootEntityId = 905; $multiSkill.TargetType = 4
$multiSkill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 906; DamageId = 907; DamageType = 8; DamageParam = 10000; TargetMode = 1
})
$multiSkill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 908; DamageId = 907; DamageType = 8; DamageParam = 10000; TargetMode = 1
})
$multiActor.Skills.Add($multiSkill)
$multiTargets = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
foreach ($targetId in @(52, 53)) {
    $targetUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $targetUnit.Id = $targetId; $targetUnit.Side = 2; $targetUnit.Hp = 50000; $targetUnit.MaxHp = 50000
    $targetUnit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $targetUnit.Turn.Id = $targetId; $targetUnit.Turn.Hp = 50000; $targetUnit.Turn.Speed = 100
    $status = [Zjcs.Combat.Official.FightBattleStatusState]::new()
    $status.ClassId = 24102; $status.OwnerId = $targetId; $status.FalloffTarget = 1
    $status.RootSkillId = 905; $status.DamageId = 907; $status.FalloffPercent = 3000
    $status.NumOfStart = 1; $status.MaxFalloffCount = 2
    $targetUnit.Statuses.Add($status)
    $multiTargets.Add($targetUnit)
}
$multiCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$multiCheckpoint.StageId = 22001; $multiCheckpoint.RandomSeed = 1
$multiCheckpoint.Units.Add($multiActor)
foreach ($targetUnit in $multiTargets) { $multiCheckpoint.Units.Add($targetUnit) }
$multiRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($multiCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$null = $multiRunner.Step(0, 1)
$multiAssertion = 'Per-target falloff dedupe leaked across a multi-target hit'
Assert-Combat ($multiTargets[0].Hp -eq 33001 -and $multiTargets[1].Hp -eq 33001) $multiAssertion
Assert-Combat ($multiCheckpoint.DamageEventSerial -eq 6) 'Multi-target damage events were not assigned unique serials'

function New-WaveUnit($id, $side, $x, $y, $hp) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.CharacterType = if ($side -eq 1) { 1 } else { 2 }
    $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $hp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = 100
    return $unit
}

# Cross-wave regression: later-wave units are not selectable, hittable or occupied before spawn.
$wavePlayer = New-WaveUnit 1 1 0 0 1000
$waveEnemy = New-WaveUnit 2 2 -2 0 100
$waveFuture = New-WaveUnit 3 2 20 0 10
$waveCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$waveCheckpoint.StageId = 22001; $waveCheckpoint.RandomSeed = 1
$waveCheckpoint.Units.Add($wavePlayer); $waveCheckpoint.Units.Add($waveEnemy); $waveCheckpoint.Units.Add($waveFuture)
$waveCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 0; UnitIds = [System.Collections.Generic.List[int]]::new() })
$waveCheckpoint.Waves[0].UnitIds.Add(2)
$waveCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 1; UnitIds = [System.Collections.Generic.List[int]]::new() })
$waveCheckpoint.Waves[1].UnitIds.Add(3)
$waveRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $waveCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
Assert-Combat ($waveFuture.Ended -and $waveFuture.Turn.Ended) 'Unspawned later-wave unit was not excluded before re-evaluation'
$waveUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($wavePlayer, $waveEnemy, $waveFuture)
$waveTargets = [Zjcs.Combat.Official.FightBattleTargeting]::Select($wavePlayer, $waveUnits, 1)
Assert-Combat ($waveTargets.Count -eq 1 -and $waveTargets[0].Id -eq 2) 'Unspawned later-wave unit leaked into target selection'
$waveHitTargets = [Zjcs.Combat.Official.FightBattleTargeting]::SelectHitTargets($waveUnits, 1, 3, $null)
Assert-Combat ($waveHitTargets.Count -eq 2 -and -not ($waveHitTargets | Where-Object { $_.Id -eq 3 })) 'Unspawned later-wave unit leaked into hit collection'
$waveStep = $waveRunner.Step(0, 1)
Assert-Combat ($waveStep.Event.Kind -eq 0 -and $waveFuture.Hp -eq 10 -and $waveEnemy.Hp -eq 100) 'A unit without a releaseable skill dealt unconfigured damage'

# A configured long-range skill still cannot reach an unspawned wave unit.
$rangePlayer = New-WaveUnit 1 1 0 0 1000
$rangeSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$rangeSkill.Slot = 0; $rangeSkill.EntityId = 910; $rangeSkill.ItemId = 911; $rangeSkill.TargetType = 1
$rangeSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(20, 0))
$rangeSkill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 912; DamageId = 913; DamageType = 8; HasCustomDamage = $true; CustomDamage = 5000
})
$rangePlayer.Skills.Add($rangeSkill)
$rangePlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$rangeFuture = New-WaveUnit 3 2 20 0 10
$rangeCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$rangeCheckpoint.StageId = 22001; $rangeCheckpoint.RandomSeed = 1
$rangeCheckpoint.Units.Add($rangePlayer); $rangeCheckpoint.Units.Add($rangeFuture)
$rangeCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 0; UnitIds = [System.Collections.Generic.List[int]]::new() })
$rangeCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 1; UnitIds = [System.Collections.Generic.List[int]]::new() })
$rangeCheckpoint.Waves[1].UnitIds.Add(3)
$rangeRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $rangeCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$rangeStep = $rangeRunner.Step(0, 1)
Assert-Combat ($rangeStep.Event.SkillCasts.Count -eq 0 -and $rangeFuture.Hp -eq 10) 'A ranged skill released against an unspawned later-wave unit'

# Spawning the next wave makes its units selectable in the same step.
$spawnPlayer = New-WaveUnit 1 1 0 0 1000
$spawnKill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$spawnKill.Slot = 0; $spawnKill.EntityId = 920; $spawnKill.ItemId = 921; $spawnKill.TargetType = 1
$spawnKill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$spawnKill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 922; DamageId = 923; DamageType = 8; HasCustomDamage = $true; CustomDamage = 5000
})
$spawnPlayer.Skills.Add($spawnKill)
$spawnPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$spawnEnemy = New-WaveUnit 2 2 1 0 10
$spawnFuture = New-WaveUnit 3 2 2 0 10
$spawnCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$spawnCheckpoint.StageId = 22001; $spawnCheckpoint.RandomSeed = 1
$spawnCheckpoint.Units.Add($spawnPlayer); $spawnCheckpoint.Units.Add($spawnEnemy); $spawnCheckpoint.Units.Add($spawnFuture)
$spawnCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 0; UnitIds = [System.Collections.Generic.List[int]]::new() })
$spawnCheckpoint.Waves[0].UnitIds.Add(2)
$spawnCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 1; UnitIds = [System.Collections.Generic.List[int]]::new() })
$spawnCheckpoint.Waves[1].UnitIds.Add(3)
$spawnRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $spawnCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$null = $spawnRunner.Step(0, 1)
Assert-Combat ($spawnEnemy.Hp -eq 0 -and $spawnCheckpoint.WaveIndex -eq 1 -and -not $spawnFuture.Ended) 'Next wave did not become selectable after the previous wave was cleared'
Assert-Combat ([Zjcs.Combat.Official.FightBattleTargeting]::Select($spawnPlayer, [Zjcs.Combat.Official.FightBattleUnitState[]]@($spawnPlayer, $spawnEnemy, $spawnFuture), 1)[0].Id -eq 3) 'Spawned next-wave unit was still excluded from targeting'
[Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($spawnCheckpoint)

Write-Output "Unity combat core checks passed: $($reference.cases.Count) pinned formula vectors, official catalog loading and digest validation, skill/status assembly, official chained hit graph, status falloff, battle victory, checkpoint replay, random stream, timeline, targeting, multi-target falloff, and cross-wave spawn selection."

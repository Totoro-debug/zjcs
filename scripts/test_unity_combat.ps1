. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
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

$rules = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/rules.json') -Raw | ConvertFrom-Json
$skillRules = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json') -Raw | ConvertFrom-Json
$battleRules = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json') -Raw | ConvertFrom-Json
$skillSource = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$battleSource = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$skillJson = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json') -Raw
$battleJson = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json') -Raw
$hpPliesJson = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/monster-hp-plies.json') -Raw
$rulesJson = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/rules.json') -Raw
$rulesSource = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/rules-source-manifest.json') -Raw | ConvertFrom-Json
$hpPliesSource = Get-Content (Join-Path $root 'unity/Assets/Official/Combat/monster-hp-plies-source-manifest.json') -Raw | ConvertFrom-Json
$officialCatalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    $skillJson, $skillSource.output_sha256, $skillSource.manifest,
    $battleJson, $battleSource.output_sha256, $battleSource.manifest,
    $hpPliesJson, $hpPliesSource.output_sha256, $hpPliesSource.manifest)
Assert-Combat ($officialCatalog.MonsterHpPliesFor(3601, 2, 9) -eq 50 -and $officialCatalog.MonsterHpPliesFor(3601, 2, 10) -eq 60 -and $officialCatalog.MonsterHpPliesFor(999999, 0, 10) -eq 1) 'Official monster HP plies group and level lookup mismatch'
$moveProperties = [System.Collections.Generic.Dictionary[int,long]]::new()
$moveProperties[$officialCatalog.PropertyType('MoveDist')] = 4
$moveProperties[$officialCatalog.PropertyType('MoveDistPercent')] = 2500
Assert-Combat ($officialCatalog.CalculateMoveDist($moveProperties) -eq 5) 'Official MoveDist percentage calculation mismatch'
$officialSkill = $officialCatalog.CreateSkill(16100, 0, 16100, 1, 1, 0, $null)
$officialStatus = $officialCatalog.CreateStatus(16104, 2, 1, 1, 16100, 16100)
$ridiculeStatus = $officialCatalog.CreateStatus(24216, 2, 2, 1)
$confusionStatus = $officialCatalog.CreateStatus(24217, 2, 2, 1)
$backToLifeStatus = $officialCatalog.CreateStatus(24233, 2, 2, 2)
Assert-Combat ($ridiculeStatus.ActionType -eq 58 -and $confusionStatus.ActionType -eq 54) 'Official status action types for ridicule or confusion did not map from ActionComponent'
Assert-Combat ($backToLifeStatus.ActionType -eq 62 -and $backToLifeStatus.HasBackToLifeComponent) 'Official FightStatusBackToLifeComponent was not mapped from the status entity'
$ridiculeOwner = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$ridiculeOwner.Id = 2
$appliedRidiculeStatus = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply(
    $ridiculeOwner, $ridiculeStatus, 0, 1)
Assert-Combat ($appliedRidiculeStatus.ActionType -eq 58) 'Status application dropped the official action type'
$releaseDuringControlSkill = $officialCatalog.CreateSkill(12410, 0, 0, 1, 1, 0, $null)
Assert-Combat ($releaseDuringControlSkill.ReleaseWhileRidicule -and $releaseDuringControlSkill.ReleaseWhileConfusion) 'Official skill AI release-under-control flags were not assembled'
Assert-Combat ($officialCatalog.EntityCount -gt 1000) 'Official combat catalog did not merge the pinned entity tables'
$hpAccEntities = @($battleRules.entities.PSObject.Properties | Where-Object {
    $null -ne $_.Value -and $_.Value.PSObject.Properties.Name -contains 'FightStatusHpAccDecreaseComponent'
})
Assert-Combat ($hpAccEntities.Count -eq 22) 'Pinned HP accumulated damage status catalog count changed'
$hpAccStatus = $officialCatalog.CreateStatus(25185, 2, 2, 1)
Assert-Combat ($hpAccStatus.HasHpAccDecrease -and [Math]::Abs($hpAccStatus.HpAccUnitHpPercentage - 0.15) -lt 0.0001 -and $hpAccStatus.HpAccUnitHp -eq -1 -and $hpAccStatus.HpAccMaxCount -eq -1 -and -not $hpAccStatus.HpAccResetAtRoundEnd -and $hpAccStatus.HpAccTriggerSkills.Count -eq 1 -and $hpAccStatus.HpAccTriggerSkills[0].EntityId -eq 58170 -and $hpAccStatus.HpAccTriggerSkills[0].Source -eq 2 -and $hpAccStatus.HpAccTriggerSkills[0].Target -eq 2 -and $hpAccStatus.HpAccTriggerSkills[0].TargetPos -eq 1) 'Official HP accumulated damage skill status was not assembled'
$unflinchingStatus = $officialCatalog.CreateStatus(25275, 2, 2, 2)
Assert-Combat ($unflinchingStatus.HasUnflinchingOnDamageComponent -and [Math]::Abs($unflinchingStatus.UnflinchingOnDamageRate - 0.5) -lt 0.0001) 'Official Unflinching status rate was not assembled'
$hpAccLimitedStatus = $officialCatalog.CreateStatus(24252, 2, 2, 2)
$hpAccResetStatus = $officialCatalog.CreateStatus(25043, 2, 2, 3)
Assert-Combat ($hpAccLimitedStatus.HpAccMaxCount -eq 99 -and $hpAccResetStatus.HpAccResetAtRoundEnd -and $hpAccResetStatus.HpAccTriggerStatuses.Count -eq 1 -and $hpAccResetStatus.HpAccTriggerStatuses[0].EntityId -eq 24240 -and $hpAccResetStatus.HpAccTriggerStatuses[0].Target -eq 2) 'HP accumulated damage limit, round reset or status trigger config mismatch'
Assert-Combat ($officialSkill.HitGraphRootEntityId -eq 0 -and $officialSkill.HitGraphRootEntityIds.Count -eq 1 -and $officialSkill.HitGraphRootEntityIds[0] -eq 16101 -and $officialSkill.TargetType -eq 1 -and $officialSkill.SkillTargetType -eq 0 -and $officialSkill.Range.Count -eq 1 -and $officialSkill.HitGraphEntities.Count -gt 0) 'Official skill definition, target kinds, range, or ordered hit graph root mapping mismatch'
Assert-Combat ($officialSkill.HitGraphRoots.Count -eq 1 -and [Math]::Abs($officialSkill.HitGraphRoots[0].Delay - 0.7) -lt 0.001 -and $officialSkill.HitGraphRoots[0].Anchor -eq 0) 'Official skill root hit delay or anchor mismatch'
Assert-Combat ([Math]::Abs($officialSkill.ActionDuration - 2.6) -lt 0.001 -and [Math]::Abs($officialSkill.Duration - 3.2) -lt 0.001 -and $officialSkill.AllRelevantEndedActionDuration -lt 0 -and $officialSkill.AllRelevantEndedDuration -lt 0) 'Official skill action/lifetime duration mapping mismatch'
$multiRootSkill = $officialCatalog.CreateSkill(56930, 0, 56930, 1, 1, 0, $null)
Assert-Combat (($multiRootSkill.HitGraphRootEntityIds -join ',') -eq '56131,56131,56131') 'Official repeated hit roots did not preserve their configured order'
Assert-Combat ($officialStatus.FalloffTarget -eq 1 -and $officialStatus.FalloffPercent -eq 4000 -and $officialStatus.RootSkillIds -contains 16100) 'Official falloff status mapping mismatch'
Assert-Combat ($officialStatus.DurationRound -eq 0 -and $officialStatus.StackCount -eq 1) 'Official base status fields were not instantiated'
$skillItems = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    $rulesJson, $rulesSource.output_sha256, $rulesSource.manifest)
$swordStarter = $skillItems.Required(10012)
$wandStarter = $skillItems.Required(10028)
Assert-Combat ($skillItems.Count -eq 1874 -and $swordStarter.EntityId -eq 10640 -and $wandStarter.EntityId -eq 10800) 'Official skill item to EC entity mapping mismatch'
Assert-Combat ($skillItems.InitialActiveSkill(200) -eq 10012 -and $skillItems.InitialActiveSkill(201) -eq 10028 -and $skillItems.InitialActiveSkill(0) -eq 10012) 'Official initial weapon skill settings mismatch'
Assert-Combat ($skillItems.ProfessionAvailabilityFor(10008, 1, 0, 0) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Available -and $skillItems.ProfessionAvailabilityFor(10008, 10, 0, 0) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Mismatch) 'Official profession skill ownership lookup mismatch'
Assert-Combat ($skillItems.ProfessionAvailabilityFor(10125, 1, 0, 0) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Available -and $skillItems.ProfessionAvailabilityFor(10000, 11, 0, 0) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Available) 'Official FightUsedSkills or predecessor profession inheritance lookup mismatch'
Assert-Combat ($skillItems.ItemTypeFor(10012) -eq 100 -and $skillItems.ProfessionAvailabilityFor(10012, 0, 200, 201) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Available -and $skillItems.ProfessionAvailabilityFor(10028, 0, 200, 201) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Mismatch -and $skillItems.ProfessionAvailabilityFor(10028, 0, 0, 201) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Available -and $skillItems.ProfessionAvailabilityFor(10012, 0, 0, 0) -eq [Zjcs.Combat.Official.FightSkillProfessionAvailability]::Unknown) 'Official safe profession fallback from current then initial weapon mismatch'
$prioritySkillItem = $skillItems.Required(10009)
Assert-Combat ($prioritySkillItem.AiPriorityId -eq 10 -and $prioritySkillItem.LastAiPriorityId -eq 11 -and ($skillItems.PriorityTypes(10) -join ',') -eq '11,10,7,1,0,2,9,6' -and ($skillItems.PriorityTypes(11) -join ',') -eq '11,10,7,1,3,2,9,6') 'Official AI priority id, last-priority id or ordered priority table mapping mismatch'
Assert-Combat ($officialCatalog.MonsterCharacterType(3201) -eq 2 -and $officialCatalog.MonsterAiType(3201) -eq 1) 'Official MonsterInfo character type or AI type lookup mismatch'
$missingSkillItem = $null
Assert-Combat (-not $skillItems.TryGet(1, [ref]$missingSkillItem) -and $null -eq $missingSkillItem) 'Official skill item catalog resolved an absent item'
$badSkillItemCatalogRejected = $false
try {
    [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rulesJson, ('0' * 64), $rulesSource.manifest)
} catch { $badSkillItemCatalogRejected = $true }
Assert-Combat $badSkillItemCatalogRejected 'Official skill item catalog accepted a mismatched rules digest'
$pigName = $officialCatalog.MonsterName(3206)
Assert-Combat ($pigName -eq [string]::new([char[]]@(0x91CE, 0x732A))) 'Official monster name lookup mismatch'
Assert-Combat ($null -eq $officialCatalog.MonsterName(0) -and $null -eq $officialCatalog.MonsterName(999999)) 'Monster name lookup invented a name for an unknown id'
$scopeHit = $officialSkill.HitGraphEntities | Where-Object { $_.EntityId -eq 16101 } | Select-Object -First 1
Assert-Combat ($null -ne $scopeHit -and $scopeHit.Scope.Count -eq 48 -and $scopeHit.AllowEmptyScope -and $scopeHit.Scope[0].X -eq 0 -and $scopeHit.Scope[0].Y -eq -2) 'Official hit scope mapping mismatch'
$plainHit = $officialCatalog.CreateSkill(12860, 0, 0, 1, 1, 0, $null).HitGraphEntities | Where-Object { $_.EntityId -eq 12861 } | Select-Object -First 1
Assert-Combat ($null -ne $plainHit -and $plainHit.Scope.Count -eq 1 -and $plainHit.Scope[0].X -eq 0 -and $plainHit.Scope[0].Y -eq 0) 'Official single-cell hit scope mapping mismatch'
$delaySkill = $officialCatalog.CreateSkill(12330, 0, 0, 1, 1, 0, $null)
$delayHit = $delaySkill.HitGraphEntities | Where-Object { $_.EntityId -eq 12331 } | Select-Object -First 1
Assert-Combat ($null -ne $delayHit -and [Math]::Abs($delayHit.DelayDistFactor - 0.04) -lt 0.0001) 'Official delay distance factor mapping mismatch'
$cureHit = $officialCatalog.CreateSkill(12400, 0, 0, 1, 1, 0, $null).HitGraphEntities | Where-Object { $_.EntityId -eq 12401 } | Select-Object -First 1
Assert-Combat ($null -ne $cureHit -and $cureHit.ActionType -eq 5) 'Official cure hit action mapping mismatch'
$noneHit = $officialCatalog.CreateSkill(12980, 0, 0, 1, 1, 0, $null).HitGraphEntities | Where-Object { $_.EntityId -eq 12981 } | Select-Object -First 1
Assert-Combat ($null -ne $noneHit -and $noneHit.ActionType -eq 0) 'Official none hit action mapping mismatch'

Assert-Combat ($null -ne $plainHit -and [Math]::Abs($plainHit.DelayDistFactor - 0.125) -lt 0.0001) 'Official chained hit distance delay factor mismatch'
$orderedSkill = $officialCatalog.CreateSkill(12860, 0, 0, 1, 1, 0, $null)
Assert-Combat ($orderedSkill.Range.Count -eq 24 -and $orderedSkill.Range[0].Y -eq -1 -and $orderedSkill.Range[1].X -eq -1 -and $orderedSkill.Range[2].X -eq 1 -and $orderedSkill.Range[3].Y -eq 1 -and $orderedSkill.Range[4].X -eq 2) 'Official skill range order was not preserved'
$moveSkill = $officialCatalog.CreateSkill(15980, 0, 0, 1, 1, 0, $null)
Assert-Combat ($moveSkill.SourceMoves.Count -eq 1 -and [Math]::Abs($moveSkill.SourceMoves[0].Delay - 0.5667) -lt 0.001 -and $moveSkill.SourceMoves[0].Distance -eq -1 -and $moveSkill.SourceMoves[0].Direction -eq 1 -and $moveSkill.SourceMoves[0].TowardsTargetPos -and -not $moveSkill.SourceMoves[0].HasConditions) 'Official skill SourceMoveList mapping mismatch'
$conditionMoveSkill = $officialCatalog.CreateSkill(13500, 0, 0, 1, 1, 0, $null)
$conditionMove = $conditionMoveSkill.SourceMoves[0]
Assert-Combat ($conditionMoveSkill.EntityId -eq 13500 -and $conditionMove.MoveBehaviourId -eq 13505 -and [Math]::Abs($conditionMove.Delay - 0.8) -lt 0.001 -and [Math]::Abs($conditionMove.MoveDuration - 0.2) -lt 0.001 -and $conditionMove.MoveSpeed -eq 0 -and $conditionMove.HasConditions -and $conditionMove.Conditions.Count -eq 1 -and $conditionMove.Conditions[0].Type -eq 'SkillTargetDistanceMoveCondition' -and $conditionMove.Conditions[0].Distance -eq 3) 'Official SkillTargetDistanceMoveCondition or action movement timing was not preserved in the battle snapshot'
$speedMoveSkill = $officialCatalog.CreateSkill(10850, 0, 0, 1, 1, 0, $null)
$speedMoveHit = $speedMoveSkill.HitGraphEntities | Where-Object { $_.EntityId -eq 10851 } | Select-Object -First 1
Assert-Combat ($null -ne $speedMoveHit -and $speedMoveHit.HitMove.MoveBehaviourId -eq 10853 -and $speedMoveHit.HitMove.MoveDuration -lt 0 -and [Math]::Abs($speedMoveHit.HitMove.MoveSpeed - 2.5) -lt 0.001) 'Official speed-based hit movement did not preserve its action entity timing'
$monsterSkillProps = $officialCatalog.CalculateSkillProps(16020, 1, 101, 11)
Assert-Combat ($monsterSkillProps.Count -eq 1 -and $monsterSkillProps[200] -eq 7500) 'Official default property 10000 did not preserve the monster skill 75% damage factor'
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
$defaultSpeeds = [Zjcs.Combat.Official.FightBattleSpeedSettings]::Default()
Assert-Combat (($defaultSpeeds.Resolve(0, $null) -join ',') -eq '0') 'Default speed settings resolved more than the unconditional speed'
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
$randomTargetSkill = $officialCatalog.CreateSkill(11200, 0, 11200, 1, 1, 0, $null)
$randomTargetHits = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($randomTargetSkill)
$randomTargetChildren = @($randomTargetHits | Where-Object { $_.RandomTargetGroupId -gt 0 })
Assert-Combat ($randomTargetChildren.Count -eq 3 -and $randomTargetChildren[0].RandomTargetGroupId -eq $randomTargetChildren[2].RandomTargetGroupId -and $randomTargetChildren[0].RandomTargetNotSameTarget -and $randomTargetChildren[0].RandomCount -eq 3 -and $randomTargetChildren[0].RandomTargetScope.Count -eq 24) 'Official random-target damage graph did not preserve parent scope, count, or de-duplication group'
$randomTargetWideSkill = $officialCatalog.CreateSkill(13990, 0, 13990, 1, 1, 0, $null)
$randomTargetWideHits = @([Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($randomTargetWideSkill) | Where-Object { $_.RandomTargetGroupId -gt 0 })
Assert-Combat ($randomTargetWideHits.Count -eq 7 -and $randomTargetWideHits[0].RandomCount -eq 7 -and $randomTargetWideHits[0].RandomTargetScope.Count -eq 40) 'Official random-target multi-hit graph 13990 was not collected in HitList order'
$randomTargetSummonSkill = $officialCatalog.CreateSkill(59360, 0, 59360, 1, 1, 0, $null)
$randomTargetSummonHits = @([Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($randomTargetSummonSkill) | Where-Object { $_.RandomTargetGroupId -gt 0 })
Assert-Combat ($randomTargetSummonHits.Count -eq 3 -and @($randomTargetSummonHits | Where-Object { -not $_.IsSummon }).Count -eq 0 -and $randomTargetSummonHits[0].RandomTargetNotSameTarget -and $randomTargetSummonHits[0].RandomTargetScope.Count -eq 48) 'Official random-target summon graph 59360 did not retain target context for its summons'
$randomTargetActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomTargetActor.Id = 20; $randomTargetActor.Side = 1; $randomTargetActor.Hp = 100; $randomTargetActor.MaxHp = 100; $randomTargetActor.Targetable = $true
$randomTargetPrimary = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomTargetPrimary.Id = 21; $randomTargetPrimary.Side = 2; $randomTargetPrimary.Hp = 100; $randomTargetPrimary.MaxHp = 100; $randomTargetPrimary.Targetable = $true
$randomTargetInsideA = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomTargetInsideA.Id = 22; $randomTargetInsideA.Side = 2; $randomTargetInsideA.X = 0; $randomTargetInsideA.Y = 1; $randomTargetInsideA.Hp = 100; $randomTargetInsideA.MaxHp = 100; $randomTargetInsideA.Targetable = $true
$randomTargetInsideB = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomTargetInsideB.Id = 23; $randomTargetInsideB.Side = 2; $randomTargetInsideB.X = 1; $randomTargetInsideB.Y = 0; $randomTargetInsideB.Hp = 100; $randomTargetInsideB.MaxHp = 100; $randomTargetInsideB.Targetable = $true
$randomTargetOutside = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomTargetOutside.Id = 24; $randomTargetOutside.Side = 2; $randomTargetOutside.X = 10; $randomTargetOutside.Y = 10; $randomTargetOutside.Hp = 100; $randomTargetOutside.MaxHp = 100; $randomTargetOutside.Targetable = $true
$randomTargetScopeSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$randomTargetScopeSkill.TargetType = 1
$randomTargetScopeHit = $randomTargetChildren[0]
$randomTargetScopeDefinition = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$randomTargetScopeDefinition.TargetMode = 2; $randomTargetScopeDefinition.Anchor = $randomTargetScopeHit.RandomTargetAnchor; $randomTargetScopeDefinition.Scope = $randomTargetScopeHit.RandomTargetScope
$randomTargetScopeCandidates = [Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets(
    $randomTargetActor, [Zjcs.Combat.Official.FightBattleUnitState[]]@($randomTargetActor, $randomTargetPrimary, $randomTargetInsideA, $randomTargetInsideB, $randomTargetOutside),
    $randomTargetScopeSkill, $randomTargetPrimary, $randomTargetScopeDefinition, [Zjcs.Combat.Official.FightRandomStream]::new(17))
Assert-Combat ($randomTargetScopeCandidates.Count -eq 2 -and @($randomTargetScopeCandidates | Where-Object { $_.Id -eq 22 -or $_.Id -eq 23 }).Count -eq 2 -and @($randomTargetScopeCandidates | Where-Object { $_.Id -eq 24 }).Count -eq 0) 'Official random-target parent Scope did not constrain candidates to its grid offsets'
$usedRandomTargetIds = [System.Collections.Generic.HashSet[int]]::new()
$randomTargetCandidates = [Zjcs.Combat.Official.FightBattleUnitState[]]@($randomTargetPrimary, $randomTargetInsideA, $randomTargetInsideB, $randomTargetOutside)
$randomTargetStream = [Zjcs.Combat.Official.FightRandomStream]::new(919)
$randomTargetBatchA = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomHitTargets($randomTargetCandidates, 3, $randomTargetStream, $usedRandomTargetIds)
foreach ($selectedRandomTarget in $randomTargetBatchA) { $usedRandomTargetIds.Add($selectedRandomTarget.Id) | Out-Null }
$randomTargetBatchB = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomHitTargets($randomTargetCandidates, 3, $randomTargetStream, $usedRandomTargetIds)
foreach ($selectedRandomTarget in $randomTargetBatchB) { $usedRandomTargetIds.Add($selectedRandomTarget.Id) | Out-Null }
$randomTargetBatchC = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomHitTargets($randomTargetCandidates, 3, $randomTargetStream, $usedRandomTargetIds)
Assert-Combat ($randomTargetBatchA.Count -eq 3 -and $randomTargetBatchB.Count -eq 1 -and $randomTargetBatchC.Count -eq 0 -and $usedRandomTargetIds.Count -eq 4) 'Random-target HitList processing did not avoid repeats and skip exhausted sub-hits'
$randomTargetGrid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$randomTargetGrid.Width = 3; $randomTargetGrid.Height = 3
$randomTargetCellScope = [System.Collections.Generic.List[Zjcs.Combat.Official.FightGridPoint]]::new()
foreach ($point in @(@{X=0;Y=0}, @{X=0;Y=1}, @{X=1;Y=0}, @{X=2;Y=0}, @{X=3;Y=0})) {
    $randomTargetCellScope.Add([Zjcs.Combat.Official.FightGridPoint]::new($point.X, $point.Y))
}
$randomTargetCellUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@(
    $randomTargetActor, $randomTargetPrimary, $randomTargetInsideA,
    $randomTargetInsideB, $randomTargetOutside)
$randomTargetCellSelections = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, $randomTargetCellUnits, $randomTargetScopeSkill,
    $randomTargetPrimary, $randomTargetCellScope, $randomTargetGrid, 10,
    [Zjcs.Combat.Official.FightRandomStream]::new(771), $true, $false,
    $false, $null, $false, $null, $false, $null, $true)
$randomTargetCellTargetIds = [System.Collections.Generic.HashSet[int]]::new()
foreach ($selection in $randomTargetCellSelections) {
    foreach ($selectedTarget in $selection.Targets) { $randomTargetCellTargetIds.Add($selectedTarget.Id) | Out-Null }
}
$randomTargetEmptySelections = @($randomTargetCellSelections | Where-Object { $_.IsEmpty })
Assert-Combat ($randomTargetCellSelections.Count -eq 4 -and $randomTargetCellTargetIds.Count -eq 3 -and $randomTargetEmptySelections.Count -eq 1 -and @($randomTargetCellSelections | Where-Object { $_.Position.X -eq 3 }).Count -eq 0) 'Random-target scope did not filter out-of-grid cells before selecting units and empty cells'
$randomTargetUsedEmptyCells = [System.Collections.Generic.HashSet[Zjcs.Combat.Official.FightGridPoint]]::new()
$randomTargetUsedGridCells = [System.Collections.Generic.HashSet[Zjcs.Combat.Official.FightGridPoint]]::new()
foreach ($selection in $randomTargetCellSelections) {
    $randomTargetUsedGridCells.Add($selection.Position) | Out-Null
    if ($selection.IsEmpty) { $randomTargetUsedEmptyCells.Add($selection.Position) | Out-Null }
}
$randomTargetNoRepeatedEmpty = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, $randomTargetCellUnits, $randomTargetScopeSkill,
    $randomTargetPrimary, $randomTargetCellScope, $randomTargetGrid, 10,
    [Zjcs.Combat.Official.FightRandomStream]::new(772), $true, $false,
    $false, $null, $true, $randomTargetUsedEmptyCells, $false, $null, $true)
$randomTargetNoEmptyAllowed = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, $randomTargetCellUnits, $randomTargetScopeSkill,
    $randomTargetPrimary, $randomTargetCellScope, $randomTargetGrid, 10,
    [Zjcs.Combat.Official.FightRandomStream]::new(773), $true, $true,
    $false, $null, $false, $null, $false, $null, $true)
$randomTargetNoRepeatedGrid = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, $randomTargetCellUnits, $randomTargetScopeSkill,
    $randomTargetPrimary, $randomTargetCellScope, $randomTargetGrid, 10,
    [Zjcs.Combat.Official.FightRandomStream]::new(774), $true, $false,
    $false, $null, $false, $null, $true, $randomTargetUsedGridCells, $true)
$randomTargetAllowOutOfGrid = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, $randomTargetCellUnits, $randomTargetScopeSkill,
    $randomTargetPrimary, $randomTargetCellScope, $randomTargetGrid, 10,
    [Zjcs.Combat.Official.FightRandomStream]::new(775), $true, $false,
    $false, $null, $false, $null, $false, $null, $false)
Assert-Combat (@($randomTargetNoRepeatedEmpty | Where-Object { $_.IsEmpty }).Count -eq 0 -and $randomTargetNoRepeatedEmpty.Count -eq 3 -and $randomTargetNoEmptyAllowed.Count -eq 3 -and $randomTargetNoRepeatedGrid.Count -eq 0 -and $randomTargetAllowOutOfGrid.Count -eq 5) 'Random-target empty/grid deduplication flags or FilterOutOfGroundScope behavior mismatch'
$randomTargetMultiCellUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomTargetMultiCellUnit.Id = 26; $randomTargetMultiCellUnit.Side = 2
$randomTargetMultiCellUnit.X = 1; $randomTargetMultiCellUnit.Y = 1
$randomTargetMultiCellUnit.Hp = 100; $randomTargetMultiCellUnit.MaxHp = 100
$randomTargetMultiCellUnit.BodyOffsets.Clear()
$randomTargetMultiCellUnit.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$randomTargetMultiCellUnit.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$randomTargetMultiCellScope = [System.Collections.Generic.List[Zjcs.Combat.Official.FightGridPoint]]::new()
$randomTargetMultiCellScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$randomTargetMultiCellScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$randomTargetMultiCellSelections = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, [Zjcs.Combat.Official.FightBattleUnitState[]]@($randomTargetMultiCellUnit),
    $randomTargetScopeSkill, $randomTargetMultiCellUnit, $randomTargetMultiCellScope,
    $randomTargetGrid, 2, [Zjcs.Combat.Official.FightRandomStream]::new(776),
    $false, $false, $false, $null, $false, $null, $false, $null, $true)
$randomTargetExcludedMultiCellId = [System.Collections.Generic.HashSet[int]]::new()
$randomTargetExcludedMultiCellId.Add($randomTargetMultiCellUnit.Id) | Out-Null
$randomTargetNoRepeatMultiCell = [Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions(
    $randomTargetActor, [Zjcs.Combat.Official.FightBattleUnitState[]]@($randomTargetMultiCellUnit),
    $randomTargetScopeSkill, $randomTargetMultiCellUnit, $randomTargetMultiCellScope,
    $randomTargetGrid, 2, [Zjcs.Combat.Official.FightRandomStream]::new(777),
    $false, $false, $true, $randomTargetExcludedMultiCellId, $false, $null,
    $false, $null, $true)
Assert-Combat ($randomTargetMultiCellSelections.Count -eq 2 -and $randomTargetMultiCellSelections[0].Targets[0].Id -eq 26 -and $randomTargetMultiCellSelections[1].Targets[0].Id -eq 26 -and $randomTargetNoRepeatMultiCell.Count -eq 0) 'Random-target cell selection did not separate multi-cell grid repeats from NotSameTarget unit de-duplication'
$randomBattlePlayer = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomBattlePlayer.Id = 1; $randomBattlePlayer.Side = 1; $randomBattlePlayer.X = 0; $randomBattlePlayer.Y = 0
$randomBattlePlayer.Hp = 1000; $randomBattlePlayer.MaxHp = 1000
$randomBattlePlayer.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
$randomBattlePlayer.Turn.Id = 1; $randomBattlePlayer.Turn.Hp = 1000; $randomBattlePlayer.Turn.Speed = 100
$randomBattleBindings = [System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialSkillBinding]]::new()
$randomBattleBindings.Add([Zjcs.Combat.Official.FightOfficialSkillBinding]@{
    EntityId = 11200; Slot = 0; ItemId = 11200; Rank = 1; Level = 1; Cooldown = 0
})
$officialCatalog.AssembleUnit($randomBattlePlayer, $randomBattleBindings, $null)
$randomBattleSkill = $randomBattlePlayer.Skills[0]
$randomBattleSkill.TargetType = 1
$randomBattleSkill.Range.Clear(); $randomBattleSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(4, 0))
$randomBattleSkill.HasCustomDamage = $true; $randomBattleSkill.CustomDamage = 100
$randomBattleEnemies = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
foreach ($enemyInfo in @(@{Id=2;X=4;Y=0}, @{Id=3;X=0;Y=1}, @{Id=4;X=-1;Y=0}, @{Id=5;X=0;Y=-1})) {
    $randomEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $randomEnemy.Id = $enemyInfo.Id; $randomEnemy.Side = 2; $randomEnemy.X = $enemyInfo.X; $randomEnemy.Y = $enemyInfo.Y
    $randomEnemy.Hp = 1000; $randomEnemy.MaxHp = 1000
    $randomEnemy.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $randomEnemy.Turn.Id = $randomEnemy.Id; $randomEnemy.Turn.Hp = 1000; $randomEnemy.Turn.Speed = 10
    $randomBattleEnemies.Add($randomEnemy)
}
$randomBattleCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$randomBattleCheckpoint.StageId = 22001; $randomBattleCheckpoint.RandomSeed = 919
$randomBattleCheckpoint.Units.Add($randomBattlePlayer)
foreach ($randomEnemy in $randomBattleEnemies) { $randomBattleCheckpoint.Units.Add($randomEnemy) }
$randomBattleWave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$randomBattleWave.Index = 0
foreach ($randomEnemy in $randomBattleEnemies) { $randomBattleWave.UnitIds.Add($randomEnemy.Id) }
$randomBattleCheckpoint.Waves.Add($randomBattleWave)
$randomBattleFormula = $officialCatalog.CreateDamageFormula(
    $rulesJson, $rulesSource.output_sha256, $rulesSource.manifest)
$randomBattleExecutor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $randomBattleCheckpoint, $randomBattleFormula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$randomBattleResult = $randomBattleExecutor.Step(0, 1)
$randomBattleDamageEvents = @($randomBattleResult.Event.DamageEvents)
$randomBattleTargetIds = @($randomBattleDamageEvents | ForEach-Object { $_.TargetId } | Sort-Object -Unique)
Assert-Combat ($randomBattleResult.Event.ActorId -eq 1 -and $randomBattleDamageEvents.Count -eq 3 -and $randomBattleTargetIds.Count -eq 3 -and @($randomBattleDamageEvents | Where-Object { $_.SkillEntityId -ne 11200 }).Count -eq 0) 'Battle executor did not apply random HitList children once to distinct selected targets'
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
$integrationStatuses.Add([Zjcs.Combat.Official.FightOfficialStatusBinding]@{
    ClassId = 25275; InstanceId = 2; CreatorId = 2
})
$officialCatalog.AssembleUnit($integrationEnemy, $null, $integrationStatuses)
Assert-Combat ($integrationEnemy.Statuses[1].HasUnflinchingOnDamageComponent -and [Math]::Abs($integrationEnemy.Statuses[1].UnflinchingOnDamageRate - 0.5) -lt 0.0001) 'Applied Unflinching status lost its configured rate'
# Force successful checks in this test so every actual HP loss can be matched to one custom-action event.
$integrationEnemy.Statuses[1].UnflinchingOnDamageRate = 1.0
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
    $officialCatalog.CreateDamageFormula($rulesJson, ('0' * 64), $rulesSource.manifest)
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
    $rulesJson, $rulesSource.output_sha256, $rulesSource.manifest)
$speeds = $officialCatalog.SpeedSettings
Assert-Combat ($speeds.Speeds.Count -eq 4 -and $speeds.Speeds[1] -eq 1.5 -and $speeds.Label(2) -eq '3') 'Official battle speed table or label mapping mismatch'
$lockedSpeeds = $speeds.Resolve(0, { param($id) $false })
Assert-Combat ($lockedSpeeds.Count -eq 1 -and $lockedSpeeds[0] -eq 0) 'Official battle speed conditions were opened without evidence'
$levelSpeeds = $speeds.Resolve(0, { param($id) $id -eq 3005 })
Assert-Combat (($levelSpeeds -join ',') -eq '0,1') 'Official level-gated battle speed was not resolved'
$allSpeeds = $speeds.Resolve(0, { param($id) $true })
Assert-Combat (($allSpeeds -join ',') -eq '0,1,2') 'Official None-mode speed list did not keep its configured indexes'
Assert-Combat ($speeds.Multiplier(0) -eq 1.0 -and $speeds.Multiplier(3) -eq 2.5 -and $speeds.Multiplier(99) -eq 1.0) 'Official speed multiplier fell back to the UI label'
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
Assert-Combat ($integrationResult.Event.DamageEvents.Count -eq 6) 'Battle step did not record every resolved hit as a damage event'
$integrationActualHpLosses = @($integrationResult.Event.DamageEvents | Where-Object {
    -not $_.IsCure -and $_.AfterHp -lt $_.BeforeHp
}).Count
$integrationUnflinchingActions = @($integrationResult.Event.StatusCustomActions | Where-Object {
    $_.UnitId -eq 2 -and $_.Action -eq 2
}).Count
Assert-Combat ($integrationActualHpLosses -gt 0 -and $integrationUnflinchingActions -eq $integrationActualHpLosses) 'Actual HP damage did not emit one configured Unflinching action per successful check'
$firstDamageEvent = $integrationResult.Event.DamageEvents[0]
Assert-Combat ($firstDamageEvent.SourceId -eq 1 -and $firstDamageEvent.TargetId -eq 2 -and $firstDamageEvent.SkillEntityId -eq 12860 -and $firstDamageEvent.SkillItemId -eq 12860 -and -not $firstDamageEvent.IsCure) 'Damage event identity or source attribution mismatch'
Assert-Combat ($firstDamageEvent.BeforeHp -eq 236 -and $firstDamageEvent.AfterHp -lt 236 -and $firstDamageEvent.MaxHp -eq 236) 'Damage event HP snapshot mismatch'
Assert-Combat ([Math]::Abs($firstDamageEvent.Damage) -gt 0 -and $firstDamageEvent.Damage -lt 0) 'Damage event settlement sign mismatch'
$playerStats = $integrationCheckpoint.Statistics.Unit(1)
$enemyStats = $integrationCheckpoint.Statistics.Unit(2)
$integrationSkillStats = $integrationCheckpoint.Statistics.Skill(1, 12860, 12860)
Assert-Combat ($playerStats.Casts -eq 1 -and $playerStats.Hits -eq 6 -and $playerStats.DamageDealt -eq 236) 'Player battle statistics did not accumulate the official skill'
Assert-Combat ($enemyStats.HitsTaken -eq 6 -and $enemyStats.DamageTaken -eq 236 -and $enemyStats.HealTaken -eq 0) 'Target battle statistics did not accumulate received damage'
Assert-Combat ($integrationSkillStats.Casts -eq 1 -and $integrationSkillStats.Hits -eq 6 -and $integrationSkillStats.Damage -eq 236) 'Per-skill statistics did not accumulate cast, hits and damage'
$healthDamageEvent = $integrationResult.Event.DamageEvents | Where-Object { $_.AfterHp -eq 0 } | Select-Object -First 1
Assert-Combat ($null -ne $healthDamageEvent) 'The lethal hit was not recorded with its post-hit HP'
$integrationRestored = [Zjcs.Combat.Official.FightBattleExecutor]::Restore(
    $integrationCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$integrationReplay = $integrationRestored.Step(0, 1)
Assert-Combat ($integrationReplay.Replayed -and $integrationEnemy.Hp -eq 0 -and $integrationCheckpoint.DamageEventSerial -eq 6) 'Configured battle checkpoint replay applied official hit nodes twice'
Assert-Combat (@($integrationReplay.Event.StatusCustomActions | Where-Object { $_.UnitId -eq 2 -and $_.Action -eq 2 }).Count -eq $integrationUnflinchingActions) 'Unflinching custom action was not preserved by receipt replay'
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
$typedHitTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$typedHitTarget.Id = 25; $typedHitTarget.Hp = 100; $typedHitTarget.MaxHp = 100
$typedHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$typedHit.ActionType = 50; $typedHit.DamageType = 8; $typedHit.HasCustomDamage = $true; $typedHit.CustomDamage = 7
$typedResult = $hitExecutor.Apply($null, 805, $cureActor, $typedHitTarget, $null, $typedHit, 0, $false)
Assert-Combat ($typedHitTarget.Hp -eq 93 -and $typedResult.Settlement.RealDamageValue -eq -7) 'Status action category incorrectly suppressed its configured damage settlement'
$noneHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$noneHit.ActionType = 0; $noneHit.DamageType = 8; $noneHit.HasCustomDamage = $true; $noneHit.CustomDamage = 7
$noneResult = $hitExecutor.Apply($null, 806, $cureActor, $typedHitTarget, $null, $noneHit, 0, $false)
Assert-Combat ($typedHitTarget.Hp -eq 93 -and $null -eq $noneResult.Settlement) 'None action category unexpectedly settled HP'
$deadTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$deadTarget.Id = 26; $deadTarget.Hp = 0; $deadTarget.MaxHp = 100
$appliedBackToLife = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($deadTarget,
    $backToLifeStatus, 0, 1)
Assert-Combat ($deadTarget.Hp -eq 1 -and $appliedBackToLife.HasBackToLifeComponent) 'FightStatusBackToLifeComponent did not restore the newly affected unit to one HP'
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

$damageCustomStatus = $officialCatalog.CreateStatus(24181, 72, 71, 1)
Assert-Combat ($schema.Id('StatusDmgReducePer') -eq 232 -and $damageCustomStatus.HasDamageCustomProps -and $damageCustomStatus.DamageCustomPropsPhase -eq 1 -and $damageCustomStatus.DamageCustomPropsPropertyId -eq 232 -and [Math]::Abs($damageCustomStatus.DamageCustomPropsDmgGreaterMaxHpPer - 0.04) -lt 0.00001 -and $damageCustomStatus.DamageCustomPropsMaxCount -eq 1 -and $damageCustomStatus.DamageCustomPropsAutoRemove -and -not $damageCustomStatus.DamageCustomPropsCheckSkillTrigger) 'Official status damage custom property configuration mismatch'
$reductionActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$reductionActor.Id = 71; $reductionActor.Hp = 10000; $reductionActor.MaxHp = 10000; $reductionActor.CharacterType = 2
$reductionActor.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $schema.Id('Attack'); Value = 1000 })
$reductionTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$reductionTarget.Id = 72; $reductionTarget.Hp = 10000; $reductionTarget.MaxHp = 10000; $reductionTarget.CharacterType = 2
$reductionTarget.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $schema.Id('StatusDmgReducePer'); Value = 5000 })
$appliedReduction = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($reductionTarget, $damageCustomStatus, 0, 1)
$reductionHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$reductionHit.DamageType = 1; $reductionHit.HasCustomDamage = $true; $reductionHit.CustomDamage = 1200
$reductionResult = $hitExecutor.Apply([Zjcs.Combat.Official.FightRandomStream]::new(1), 803,
    $reductionActor, $reductionTarget, $null, $reductionHit, 0, $false)
Assert-Combat ($reductionResult.Settlement.RealDamageValue -eq -533 -and $reductionTarget.Statuses.Count -eq 0 -and $appliedReduction.DamageCustomPropsTriggeredCount -eq 1) 'Status 24181 did not reduce one eligible damage segment and consume its configured trigger'
$belowThreshold = $officialCatalog.CreateStatus(24181, 74, 73, 1)
$belowThresholdActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$belowThresholdActor.Id = 73; $belowThresholdActor.Hp = 10000; $belowThresholdActor.MaxHp = 10000; $belowThresholdActor.CharacterType = 2
$belowThresholdActor.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $schema.Id('Attack'); Value = 1000 })
$belowThresholdTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$belowThresholdTarget.Id = 74; $belowThresholdTarget.Hp = 10000; $belowThresholdTarget.MaxHp = 10000; $belowThresholdTarget.CharacterType = 2
$belowThresholdTarget.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $schema.Id('StatusDmgReducePer'); Value = 5000 })
$null = [Zjcs.Combat.Official.FightStatusLifecycle]::Apply($belowThresholdTarget, $belowThreshold, 0, 1)
$smallReductionHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$smallReductionHit.DamageType = 1; $smallReductionHit.HasCustomDamage = $true; $smallReductionHit.CustomDamage = 300
$smallReductionResult = $hitExecutor.Apply([Zjcs.Combat.Official.FightRandomStream]::new(1), 804,
    $belowThresholdActor, $belowThresholdTarget, $null, $smallReductionHit, 0, $false)
Assert-Combat ($smallReductionResult.Settlement.RealDamageValue -eq -200 -and $belowThresholdTarget.Statuses.Count -eq 1 -and $belowThresholdTarget.Statuses[0].DamageCustomPropsTriggeredCount -eq 0) 'Status 24181 triggered below its strict 4% max HP damage threshold'

$shieldActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$shieldActor.Id = 81; $shieldActor.Hp = 1000; $shieldActor.MaxHp = 1000; $shieldActor.CharacterType = 2
$shieldTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$shieldTarget.Id = 82; $shieldTarget.Hp = 1000; $shieldTarget.MaxHp = 1000; $shieldTarget.CharacterType = 2
$persistentShield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$persistentShield.InstanceId = 1; $persistentShield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$persistentShield.ShieldValue = 60; $persistentShield.ShieldRemainValue = 60; $persistentShield.ShieldInitialized = $true
$persistentShield.ShieldDestroyedSkillId = 9001
$shieldTarget.Statuses.Add($persistentShield)
$shieldHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$shieldHit.DamageType = 8; $shieldHit.HasCustomDamage = $true; $shieldHit.CustomDamage = 100
$shieldResult = $hitExecutor.Apply($null, 805, $shieldActor, $shieldTarget, $null, $shieldHit, 0, $false)
Assert-Combat ($shieldTarget.Hp -eq 960 -and $shieldResult.Formula.Damage -eq -40 -and $shieldTarget.Statuses.Count -eq 0 -and $shieldResult.TriggeredStatusSkills -contains 9001) 'Persistent shield did not absorb damage, expire and emit its destroyed skill'
$countedShield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$countedShield.InstanceId = 1; $countedShield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Counted
$countedShield.ShieldValue = 40; $countedShield.ShieldRemainValue = 40; $countedShield.ShieldRemainCount = 1
$countedShield.ShieldInitialized = $true
$shieldTarget.Hp = 1000; $shieldTarget.Statuses.Add($countedShield)
$countedResult = $hitExecutor.Apply($null, 806, $shieldActor, $shieldTarget, $null, $shieldHit, 0, $false)
Assert-Combat ($shieldTarget.Hp -eq 940 -and $countedResult.Formula.Damage -eq -60 -and $shieldTarget.Statuses.Count -eq 0) 'Counted shield did not absorb its configured value and spend one use'
$invincibleShield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$invincibleShield.InstanceId = 1; $invincibleShield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Invincible
$invincibleShield.ShieldRemainCount = 1; $invincibleShield.ShieldInitialized = $true
$shieldTarget.Hp = 1000; $shieldTarget.Statuses.Add($invincibleShield)
$invincibleResult = $hitExecutor.Apply($null, 807, $shieldActor, $shieldTarget, $null, $shieldHit, 0, $false)
Assert-Combat ($shieldTarget.Hp -eq 1000 -and $invincibleResult.Formula.Damage -eq 0 -and $shieldTarget.Statuses.Count -eq 0) 'Invincible shield did not cancel one eligible hit'

$customShieldUnit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$customShieldUnit.Id = 91; $customShieldUnit.Hp = 1000; $customShieldUnit.MaxHp = 1000
$customShieldBindings = [System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialStatusBinding]]::new()
$customShieldBindings.Add([Zjcs.Combat.Official.FightOfficialStatusBinding]@{
    ClassId = 24174; InstanceId = 1; CreatorId = 91; CustomShieldValue = 320
})
$officialCatalog.AssembleUnit($customShieldUnit, $null, $customShieldBindings)
[Zjcs.Combat.Official.FightStatusPropertyRuntime]::Rebuild($customShieldUnit,
    [Zjcs.Combat.Official.FightBattleUnitState[]]@($customShieldUnit), $officialCatalog, $formula)
Assert-Combat ($customShieldUnit.Statuses[0].ShieldUsesCustomValue -and $customShieldUnit.Statuses[0].ShieldRemainValue -eq 320 -and [Zjcs.Combat.Official.FightBattleShieldRuntime]::PersistentShieldTotal($customShieldUnit) -eq 320) 'External custom shield value was not applied to the configured status'

$costPlayer = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$costPlayer.Id = 1; $costPlayer.Side = 1; $costPlayer.CharacterType = 1; $costPlayer.Hp = 1000; $costPlayer.MaxHp = 1000; $costPlayer.Level = 1
$costPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$costShield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$costShield.InstanceId = 1; $costShield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$costShield.ShieldValue = 70; $costShield.ShieldRemainValue = 70; $costShield.ShieldInitialized = $true
$costPlayer.Statuses.Add($costShield)
$costSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$costSkill.Slot = 0; $costSkill.EntityId = 910; $costSkill.ItemId = 911; $costSkill.CostType = 2
$costSkill.TargetType = 1; $costSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$costSkill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 912; DamageId = 913; DamageType = 6; BasicProperty = $schema.Id('Attack');
    DamageProperty = $schema.Id('DmgAddPercent')
})
$costPlayer.Skills.Add($costSkill)
$costEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$costEnemy.Id = 2; $costEnemy.Side = 2; $costEnemy.CharacterType = 2; $costEnemy.Hp = 500; $costEnemy.MaxHp = 500; $costEnemy.X = 1; $costEnemy.Level = 1
$costPlayer.Turn.Id = $costPlayer.Id; $costPlayer.Turn.Hp = $costPlayer.Hp
$costEnemy.Turn.Id = $costEnemy.Id; $costEnemy.Turn.Hp = $costEnemy.Hp
$costCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$costCheckpoint.StageId = 22002; $costCheckpoint.RandomSeed = 1
$costCheckpoint.Units.Add($costPlayer); $costCheckpoint.Units.Add($costEnemy)
$costCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 0; Spawned = $true })
$costCheckpoint.Waves[0].UnitIds.Add(1); $costCheckpoint.Waves[0].UnitIds.Add(2)
$costRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $costCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$costStep = $costRunner.Step(0, 1)
Assert-Combat ($costSkill.SkillCost -eq 70 -and $costShield.ShieldRemainValue -eq 0 -and $costEnemy.Hp -eq 430) 'AllPersistentShield cost was not consumed and converted to 1:1 damage'

$hpCostPlayer = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$hpCostPlayer.Id = 1; $hpCostPlayer.Side = 1; $hpCostPlayer.CharacterType = 1; $hpCostPlayer.Hp = 1000; $hpCostPlayer.MaxHp = 1000; $hpCostPlayer.Level = 1
$hpCostPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$hpCostSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$hpCostSkill.Slot = 0; $hpCostSkill.EntityId = 920; $hpCostSkill.ItemId = 921; $hpCostSkill.CostType = 1; $hpCostSkill.SkillCost = 50
$hpCostSkill.TargetType = 1; $hpCostSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$hpCostSkill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 922; DamageId = 923; DamageType = 6; BasicProperty = $schema.Id('Attack');
    DamageProperty = $schema.Id('DmgAddPercent')
})
$hpCostPlayer.Skills.Add($hpCostSkill)
$hpCostEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$hpCostEnemy.Id = 2; $hpCostEnemy.Side = 2; $hpCostEnemy.CharacterType = 2; $hpCostEnemy.Hp = 500; $hpCostEnemy.MaxHp = 500; $hpCostEnemy.X = 1; $hpCostEnemy.Level = 1
$hpCostPlayer.Turn.Id = $hpCostPlayer.Id; $hpCostPlayer.Turn.Hp = $hpCostPlayer.Hp
$hpCostEnemy.Turn.Id = $hpCostEnemy.Id; $hpCostEnemy.Turn.Hp = $hpCostEnemy.Hp
$hpCostCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$hpCostCheckpoint.StageId = 22003; $hpCostCheckpoint.RandomSeed = 1
$hpCostCheckpoint.Units.Add($hpCostPlayer); $hpCostCheckpoint.Units.Add($hpCostEnemy)
$hpCostCheckpoint.Waves.Add([Zjcs.Combat.Official.FightBattleWaveState]@{ Index = 0; Spawned = $true })
$hpCostCheckpoint.Waves[0].UnitIds.Add(1); $hpCostCheckpoint.Waves[0].UnitIds.Add(2)
$hpCostRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $hpCostCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new())
$hpCostStep = $hpCostRunner.Step(0, 1)
$hpCostEvent = $hpCostStep.Event.DamageEvents | Where-Object { $_.IsSkillCost } | Select-Object -First 1
Assert-Combat ($null -ne $hpCostEvent -and $hpCostEvent.SourceId -eq 1 -and $hpCostEvent.TargetId -eq 1 -and $hpCostEvent.Damage -eq -50 -and $hpCostEvent.BeforeHp -eq 1000 -and $hpCostEvent.AfterHp -eq 950 -and $hpCostCheckpoint.Statistics.Unit(1).Hits -eq 1 -and $hpCostCheckpoint.Statistics.Unit(1).DamageTaken -eq 0) 'HP skill cost did not emit a self-targeted visual event without inflating hit statistics'
$hpCostRoundTrip = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($hpCostStep.Checkpoint))
$savedHpCost = $hpCostRoundTrip.Receipts[0].Event.DamageEvents | Where-Object { $_.IsSkillCost } | Select-Object -First 1
Assert-Combat ($null -ne $savedHpCost -and $savedHpCost.Damage -eq -50 -and $savedHpCost.TargetId -eq 1) 'HP skill cost presentation event did not survive checkpoint replay persistence'

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
$aiActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$aiActor.Id = 10; $aiActor.Side = 1; $aiActor.Hp = 100; $aiActor.MaxHp = 100
$aiPreferred = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$aiPreferred.Id = 11; $aiPreferred.Side = 2; $aiPreferred.CharacterType = 2; $aiPreferred.Hp = 80; $aiPreferred.MaxHp = 100
$aiLowHp = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$aiLowHp.Id = 12; $aiLowHp.Side = 2; $aiLowHp.CharacterType = 4; $aiLowHp.Hp = 20; $aiLowHp.MaxHp = 100
$aiUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($aiActor, $aiPreferred, $aiLowHp)
$aiSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$aiSkill.TargetType = 1; $aiSkill.PriorityCharacterType = 2; $aiSkill.AiPriorityTypes.Add(11)
$aiSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($aiActor, $aiUnits, 1, 0, $false, $false, $aiSkill)
Assert-Combat ($aiSelection.Count -eq 1 -and $aiSelection[0].Id -eq 11) 'Configured priority character type did not precede lower-health fallback'
$aiSkill.AiPriorityTypes.Clear(); $aiSkill.AiPriorityTypes.Add(2)
$aiSkill.PriorityCharacterType = -1; $aiSkill.HpLowerCharacterTypeFilter.Add(4)
$aiSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($aiActor, $aiUnits, 1, 0, $false, $false, $aiSkill)
Assert-Combat ($aiSelection.Count -eq 1 -and $aiSelection[0].Id -eq 11) 'Low-HP character-type filter did not exclude its configured type when another target was eligible'
$ignored = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$ignored.ClassId = 70001; $aiLowHp.Statuses.Add($ignored)
$aiSkill.AiPriorityTypes.Clear(); $aiSkill.IgnoreStatusIds.Add(70001)
$aiSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($aiActor, $aiUnits, 1, 0, $false, $false, $aiSkill)
Assert-Combat ($aiSelection.Count -eq 1 -and $aiSelection[0].Id -eq 11) 'Configured ignored status did not remove the target from AI selection'
$aiSkill.IgnoreStatusIds.Clear(); $aiSkill.AiPriorityTypes.Clear(); $aiSkill.AiPriorityTypes.Add(18)
$aiSkill.HpLowerCharacterTypeFilter.Clear()
$aiSkill.PriorityCharacterTypeGroups.Add([System.Collections.Generic.List[int]]::new())
$aiSkill.PriorityCharacterTypeGroups[0].Add(2)
$aiSkill.PriorityCharacterTypeGroups.Add([System.Collections.Generic.List[int]]::new())
$aiSkill.PriorityCharacterTypeGroups[1].Add(4)
$aiSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($aiActor, $aiUnits, 1, 0, $false, $false, $aiSkill)
Assert-Combat ($aiSelection.Count -eq 1 -and $aiSelection[0].Id -eq 11) 'Configured character-type group ordering was not applied'
$aiSkill.UseLastAiPriority = $true; $aiSkill.LastAiPriorityTypes.Add(2)
$aiSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($aiActor, $aiUnits, 1, 0, $false, $false, $aiSkill)
Assert-Combat ($aiSelection.Count -eq 1 -and $aiSelection[0].Id -eq 12) 'Last-skill AI priority list was not selected when configured'
$aiActor.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$aiActorSkillSlot = [Zjcs.Combat.Official.FightSkillSlotState]::new()
$aiActorSkillSlot.Slot = 0; $aiActorSkillSlot.LimitedTimes = -1
$aiActor.SkillState.Slots.Add($aiActorSkillSlot)
$aiSkill.Slot = 0; $aiSkill.PriorityCharacterType = 2
$aiActor.Skills.Add($aiSkill)
$aiSkill.AiPriorityTypes.Clear(); $aiSkill.AiPriorityTypes.Add(11)
$aiSkill.UseLastAiPriority = $true
$readyAiSkill = [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($aiActor, $aiUnits, 1)
Assert-Combat ($null -ne $readyAiSkill -and -not $aiSkill.UseLastAiPriority) 'A previous turn last-skill priority leaked into the next turn'
$aiSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($aiActor, $aiUnits, 1, 0, $false, $false, $aiSkill)
Assert-Combat ($aiSelection.Count -eq 1 -and $aiSelection[0].Id -eq 11) 'Normal skill priority did not resume after last-skill selection'

$approxActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$approxActor.Id = 100; $approxActor.Side = 1; $approxActor.Hp = 100; $approxActor.MaxHp = 100
$approxActor.Direction = 3
$clusterTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$clusterTarget.Id = 101; $clusterTarget.Side = 2; $clusterTarget.X = 4; $clusterTarget.Hp = 100; $clusterTarget.MaxHp = 100
$clusterMate = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$clusterMate.Id = 102; $clusterMate.Side = 2; $clusterMate.X = 5; $clusterMate.Hp = 100; $clusterMate.MaxHp = 100
$isolatedTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$isolatedTarget.Id = 103; $isolatedTarget.Side = 2; $isolatedTarget.X = 10; $isolatedTarget.Hp = 100; $isolatedTarget.MaxHp = 100
$approxUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($approxActor, $clusterTarget, $clusterMate, $isolatedTarget)
$approxSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$approxSkill.TargetType = 1; $approxSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$approxSkill.AiPriorityTypes.Add(3)
$approxSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($approxActor, $approxUnits, 1, 0, $false, $false, $approxSkill)
Assert-Combat ($approxSelection[0].Id -eq 103) 'SaferPos approximation did not prefer the less threatened approach tile'
$approxSkill.AiPriorityTypes.Clear(); $approxSkill.AiPriorityTypes.Add(16)
$approxSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($approxActor, $approxUnits, 1, 0, $false, $false, $approxSkill)
Assert-Combat ($approxSelection[0].Id -eq 103) 'StayAwayFromEnemies approximation did not prefer the most isolated approach tile'
$approxSkill.AiPriorityTypes.Clear(); $approxSkill.AiPriorityTypes.Add(20)
$approxSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($approxActor, $approxUnits, 1, 0, $false, $false, $approxSkill)
Assert-Combat ($approxSelection[0].Id -eq 102) 'ShorterEnemyDistToTargetPos approximation did not group enemies'
$approxActor.AiPreviousTargetId = 103
$approxSkill.AiPriorityTypes.Clear(); $approxSkill.AiPriorityTypes.Add(6)
$approxSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($approxActor, $approxUnits, 1, 0, $false, $false, $approxSkill)
Assert-Combat ($approxSelection[0].Id -eq 103) 'PriorTargetEntityPos approximation did not retain the previous target'

$directionActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$directionActor.Id = 120; $directionActor.Side = 1; $directionActor.Hp = 100; $directionActor.MaxHp = 100; $directionActor.Direction = 3
$eastTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$eastTarget.Id = 121; $eastTarget.Side = 2; $eastTarget.X = 6; $eastTarget.Hp = 100; $eastTarget.MaxHp = 100
$northTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$northTarget.Id = 122; $northTarget.Side = 2; $northTarget.Y = 6; $northTarget.Hp = 100; $northTarget.MaxHp = 100
$directionUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($directionActor, $eastTarget, $northTarget)
$directionSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$directionSkill.TargetType = 1; $directionSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$directionSkill.AiPriorityTypes.Add(9)
$directionSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($directionActor, $directionUnits, 1, 0, $false, $false, $directionSkill)
Assert-Combat ($directionSelection[0].Id -eq 121) 'ShorterEnemyTargetPosDistAndSameDir approximation did not prefer the current facing'

$coverageActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$coverageActor.Id = 140; $coverageActor.Side = 1; $coverageActor.Hp = 100; $coverageActor.MaxHp = 100
$largeBodyTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$largeBodyTarget.Id = 141; $largeBodyTarget.Side = 2; $largeBodyTarget.X = 3; $largeBodyTarget.Hp = 100; $largeBodyTarget.MaxHp = 100
$largeBodyTarget.BodyOffsets.Clear()
$largeBodyTarget.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$largeBodyTarget.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$largeBodyTarget.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(2, 0))
$smallBodyTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$smallBodyTarget.Id = 142; $smallBodyTarget.Side = 2; $smallBodyTarget.X = 10; $smallBodyTarget.Hp = 100; $smallBodyTarget.MaxHp = 100
$smallBodyMate = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$smallBodyMate.Id = 143; $smallBodyMate.Side = 2; $smallBodyMate.X = 11; $smallBodyMate.Hp = 100; $smallBodyMate.MaxHp = 100
$coverageUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($coverageActor, $largeBodyTarget, $smallBodyTarget, $smallBodyMate)
$coverageSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$coverageSkill.TargetType = 1; $coverageSkill.AiPriorityTypes.Add(12)
$coverageHit = [Zjcs.Combat.Official.FightBattleHitEntity]::new()
$coverageHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$coverageHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$coverageHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(2, 0))
$coverageSkill.HitGraphEntities.Add($coverageHit)
$coverageSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($coverageActor, $coverageUnits, 1, 0, $false, $false, $coverageSkill)
Assert-Combat ($coverageSelection[0].Id -eq 141) 'PriorBiggerBodyHitCoverage approximation did not count covered body cells'

$healer = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$healer.Id = 150; $healer.Side = 1; $healer.Hp = 100; $healer.MaxHp = 100
$woundedAlly = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$woundedAlly.Id = 151; $woundedAlly.Side = 1; $woundedAlly.Hp = 30; $woundedAlly.MaxHp = 100
$lowerRatioAlly = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$lowerRatioAlly.Id = 152; $lowerRatioAlly.Side = 1; $lowerRatioAlly.Hp = 60; $lowerRatioAlly.MaxHp = 300
$healerUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($healer, $woundedAlly, $lowerRatioAlly)
$childSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$childSkill.TargetType = 2; $childSkill.AiPriorityTypes.Add(17)
$childSelection = [Zjcs.Combat.Official.FightBattleTargeting]::Select($healer, $healerUnits, 2, 0, $false, $false, $childSkill)
Assert-Combat ($childSelection[0].Id -eq 152) 'AttachOperateChildSkillFriendHp approximation did not choose the lower HP ratio'

$randomActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$randomActor.Id = 160; $randomActor.Side = 1; $randomActor.Hp = 100; $randomActor.MaxHp = 100
$randomTargets = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$randomTargets.Add($randomActor)
foreach ($id in 161..164) {
    $randomTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $randomTarget.Id = $id; $randomTarget.Side = 2; $randomTarget.X = $id; $randomTarget.Hp = 100; $randomTarget.MaxHp = 100
    $randomTargets.Add($randomTarget)
}
$randomSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$randomSkill.TargetType = 1; $randomSkill.AiPriorityTypes.Add(13)
$randomA = [Zjcs.Combat.Official.FightRandomStream]::new(2468)
$randomB = [Zjcs.Combat.Official.FightRandomStream]::new(2468)
$randomSelectionA = [Zjcs.Combat.Official.FightBattleTargeting]::Select($randomActor, $randomTargets, 1, 0, $false, $false, $randomSkill, $randomA)
$randomSelectionB = [Zjcs.Combat.Official.FightBattleTargeting]::Select($randomActor, $randomTargets, 1, 0, $false, $false, $randomSkill, $randomB)
Assert-Combat ($randomSelectionA[0].Id -eq $randomSelectionB[0].Id -and $randomA.Seed -eq $randomB.Seed -and $randomA.Seed -ne 2468) 'PriorRandom did not replay deterministically from the battle random stream'

$controlActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$controlActor.Id = 70; $controlActor.Side = 1; $controlActor.Hp = 100; $controlActor.MaxHp = 100
$controlActor.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$controlSlot = [Zjcs.Combat.Official.FightSkillSlotState]::new()
$controlSlot.Slot = 0; $controlSlot.LimitedTimes = -1
$controlActor.SkillState.Slots.Add($controlSlot)
$controlSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$controlSkill.Slot = 0; $controlSkill.TargetType = 1
$controlActor.Skills.Add($controlSkill)
$normalTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$normalTarget.Id = 71; $normalTarget.Side = 2; $normalTarget.Hp = 20; $normalTarget.MaxHp = 100
$tauntSource = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$tauntSource.Id = 72; $tauntSource.Side = 2; $tauntSource.Hp = 80; $tauntSource.MaxHp = 100
$taunt = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$taunt.ActionType = 58; $taunt.CreatorId = $tauntSource.Id; $taunt.InstanceId = 1
$controlActor.Statuses.Add($taunt)
$controlUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($controlActor, $normalTarget, $tauntSource)
Assert-Combat ($null -eq [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($controlActor, $controlUnits, 1)) 'A skill without ReleaseWhileRidicule released during ridicule'
$controlSkill.ReleaseWhileRidicule = $true
$tauntTargets = [Zjcs.Combat.Official.FightBattleTargeting]::Select($controlActor, $controlUnits, 1, 0, $false, $false, $controlSkill)
Assert-Combat ($tauntTargets.Count -eq 1 -and $tauntTargets[0].Id -eq $tauntSource.Id) 'Allowed ridicule skill did not prioritize its status source'
Assert-Combat ($null -ne [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($controlActor, $controlUnits, 1)) 'Allowed ridicule skill was incorrectly blocked'

$controlActor.Statuses.Clear()
$confusion = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$confusion.ActionType = 54; $confusion.InstanceId = 2
$controlActor.Statuses.Add($confusion)
$confusedAlly = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$confusedAlly.Id = 73; $confusedAlly.Side = 1; $confusedAlly.X = 2
$confusedAlly.Hp = 30; $confusedAlly.MaxHp = 100
$confusedEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$confusedEnemy.Id = 74; $confusedEnemy.Side = 2; $confusedEnemy.X = 3
$confusedEnemy.Hp = 50; $confusedEnemy.MaxHp = 100
$controlActor.X = 0; $normalTarget.Hp = 100; $tauntSource.Hp = 100
$confusedUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@(
    $controlActor, $confusedAlly, $confusedEnemy)
$controlSkill.ReleaseWhileRidicule = $false
Assert-Combat ($null -eq [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($controlActor, $confusedUnits, 1)) 'A skill without ReleaseWhileConfusion released during confusion'
$controlSkill.ReleaseWhileConfusion = $true
$confusionTargets = [Zjcs.Combat.Official.FightBattleTargeting]::Select(
    $controlActor, $confusedUnits, 1, 0, $false, $false, $controlSkill)
Assert-Combat ($confusionTargets.Count -eq 1 -and $confusionTargets[0].Id -eq $confusedAlly.Id) 'Allowed confusion skill still filtered targets by faction'
$confusionHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$confusionHit.TargetMode = 1
$confusionHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]@{ X = 0; Y = 0 })
$confusionHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]@{ X = 1; Y = 0 })
$confusionScope = [Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets(
    $controlActor, $confusedUnits, $controlSkill, $confusionTargets[0], $confusionHit)
Assert-Combat ($confusionScope.Count -eq 2 -and @($confusionScope.Id) -contains $confusedEnemy.Id) 'Confusion did not remove faction filtering from the hit scope'

$mutexActor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$mutexActor.Id = 75; $mutexActor.Side = 1; $mutexActor.Hp = 100; $mutexActor.MaxHp = 100
$mutexActor.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$mutexSlot = [Zjcs.Combat.Official.FightSkillSlotState]::new()
$mutexSlot.Slot = 0; $mutexSlot.LimitedTimes = -1
$mutexActor.SkillState.Slots.Add($mutexSlot)
$mutexActionSkill = $officialCatalog.CreateSkill(12410, 0, 12410, 1, 1, 0, $null)
$mutexActor.Skills.Add($mutexActionSkill)
$mutexEnemy = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$mutexEnemy.Id = 76; $mutexEnemy.Side = 2; $mutexEnemy.Hp = 100; $mutexEnemy.MaxHp = 100
$mutexUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@($mutexActor, $mutexEnemy)
Assert-Combat ($mutexActionSkill.ActionType -eq 2 -and $mutexActionSkill.BlockingSourceActionTypes.Contains(57)) 'Official state mutex blockers were not compiled onto the skill'
Assert-Combat ($null -ne [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($mutexActor, $mutexUnits, 1)) 'An unblocked official skill was not ready'
$restricted = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$restricted.ActionType = 51
$mutexActor.Statuses.Add($restricted)
Assert-Combat ($null -ne [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($mutexActor, $mutexUnits, 1)) 'Restrict incorrectly blocked a Skill action'
$moveAllowed = $officialCatalog.CanStartAction($mutexActor,
    [Zjcs.Combat.Official.FightOfficialCombatCatalog]::MoveActionType)
$skillMoveAllowed = $officialCatalog.CanStartAction($mutexActor,
    [Zjcs.Combat.Official.FightOfficialCombatCatalog]::SkillMoveActionType)
Assert-Combat (-not $moveAllowed -and -not $skillMoveAllowed) "Restrict did not block Move and SkillMove through the official state mutex (Move=$moveAllowed, SkillMove=$skillMoveAllowed)"
Assert-Combat (-not $officialCatalog.CanStartEntityAction($mutexActor, 12103)) 'Restrict did not block a configured SkillMove action entity'
$mutexActor.Statuses.Clear()
$frozen = $officialCatalog.CreateStatus(24215, 2, 1, 1)
$mutexActor.Statuses.Add($frozen)
Assert-Combat ($null -eq [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill($mutexActor, $mutexUnits, 1)) 'Frozen state did not block a skill through the official state mutex'
Assert-Combat (-not $officialCatalog.CanStartAction($mutexActor, [Zjcs.Combat.Official.FightOfficialCombatCatalog]::MoveActionType) -and -not $officialCatalog.CanStartAction($mutexActor, [Zjcs.Combat.Official.FightOfficialCombatCatalog]::SkillMoveActionType)) 'Frozen state did not block Move and SkillMove through the official state mutex'
Assert-Combat (-not $officialCatalog.CanStartEntityAction($mutexActor, 12083) -and -not $officialCatalog.CanStartEntityAction($mutexActor, 12878)) 'Frozen state did not block configured knockback and drag action entities'

$hitActionSkill = $officialCatalog.CreateSkill(12030, 0, 12030, 1, 1, 0, $null)
$safeAttackerTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$safeAttackerTarget.Id = 77; $safeAttackerTarget.Side = 2; $safeAttackerTarget.X = 1
$safeAttackerTarget.Hp = 100; $safeAttackerTarget.MaxHp = 100
$safeAttackerTarget.Statuses.Add($officialCatalog.CreateStatus(24306, 2, 2, 1))
$invincibleTarget = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$invincibleTarget.Id = 78; $invincibleTarget.Side = 2; $invincibleTarget.X = 2
$invincibleTarget.Hp = 100; $invincibleTarget.MaxHp = 100
$invincibleStatus = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$invincibleStatus.ActionType = 55
$invincibleTarget.Statuses.Add($invincibleStatus)
$actionTargetUnits = [Zjcs.Combat.Official.FightBattleUnitState[]]@(
    $mutexActor, $safeAttackerTarget, $invincibleTarget)
Assert-Combat ($hitActionSkill.HitTargetActionType -eq 4 -and $hitActionSkill.BlockingTargetActionTypes.Contains(72) -and -not $hitActionSkill.BlockingTargetActionTypes.Contains(55)) 'Hit target action legality did not match the official damage mutex'
Assert-Combat ([Zjcs.Combat.Official.FightBattleTargeting]::IsActionBlockedByStatus($safeAttackerTarget, $hitActionSkill.BlockingTargetActionTypes)) 'Explicit target action legality helper missed a blocked target'
Assert-Combat (-not $officialCatalog.CanStartEntityAction($safeAttackerTarget, 12083)) 'SafeAttacker state did not block a configured knockback action entity'
$actionTargets = [Zjcs.Combat.Official.FightBattleTargeting]::Select(
    $mutexActor, $actionTargetUnits, 1, 0, $false, $false, $hitActionSkill)
Assert-Combat ($actionTargets.Count -eq 1 -and $actionTargets[0].Id -eq $invincibleTarget.Id) 'HitTargetActionType did not filter a target whose state discards damage'
$actionHit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$actionHit.TargetMode = 1
$actionHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]@{ X = -1; Y = 0 })
$actionHit.Scope.Add([Zjcs.Combat.Official.FightGridPoint]@{ X = 0; Y = 0 })
$actionScope = [Zjcs.Combat.Official.FightBattleTargeting]::SelectScopeTargets(
    $mutexActor, $actionTargetUnits, $hitActionSkill, $invincibleTarget, $actionHit)
Assert-Combat ($actionScope.Count -eq 1 -and $actionScope[0].Id -eq $invincibleTarget.Id) 'HitTargetActionType was not applied to area hit candidates'

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

foreach ($targetDistance in 3, 4) {
    $conditionActor = New-WaveUnit 1 1 1 1 1000
    $conditionActor.MoveMask = 4
    $conditionSkill = $officialCatalog.CreateSkill(13500, 0, 10181, 1, 1, 0, $null)
    $conditionSkill.HitGraphRootEntityIds.Clear()
    $conditionSkill.HitGraphRoots.Clear()
    $conditionSkill.HitGraphEntities.Clear()
    $conditionSkill.Hits.Clear()
    $conditionActor.Skills.Add($conditionSkill)
    $conditionActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($conditionSkill, 0))
    $conditionEnemy = New-WaveUnit 2 2 (1 + $targetDistance) 1 1000
    $conditionCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $conditionCheckpoint.StageId = 22001; $conditionCheckpoint.RandomSeed = 13000 + $targetDistance
    $conditionCheckpoint.Units.Add($conditionActor); $conditionCheckpoint.Units.Add($conditionEnemy)
    $conditionCheckpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
    $conditionCheckpoint.Grid.Width = 8; $conditionCheckpoint.Grid.Height = 5
    $conditionRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
        $conditionCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
        $officialCatalog, $skillItems)
    $conditionStep = $conditionRunner.Step(0, 1)
    $expectedActorX = if ($targetDistance -le 3) { 0 } else { 1 }
    Assert-Combat ($conditionStep.Event.SkillCasts.Count -eq 1 -and $conditionActor.X -eq $expectedActorX) "Official skill target-distance move condition produced the wrong result at distance $targetDistance"
    if ($targetDistance -le 3) {
        $sourceMovement = $conditionStep.Event.Movements | Where-Object { $_.UnitId -eq $conditionActor.Id } | Select-Object -First 1
        Assert-Combat ($null -ne $sourceMovement -and [Math]::Abs($sourceMovement.Delay - 0.8) -lt 0.001 -and [Math]::Abs($sourceMovement.MoveDuration - 0.2) -lt 0.001 -and $sourceMovement.MoveSpeed -eq 0 -and $sourceMovement.Path.Count -eq 2 -and $conditionStep.Event.Duration -ge 1.0) 'Official source movement event did not retain its delay, per-segment duration and path'
    } else {
        Assert-Combat ($conditionStep.Event.Movements.Count -eq 0) 'Conditionally blocked movement still emitted a movement event'
    }
}

$priorityBindingPlayer = New-WaveUnit 1 1 0 0 1000
$priorityBindingPlayer.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
$priorityBindingSkill = $officialCatalog.CreateSkill(10610, 0, 10009, 1, 1, 0, $null)
$priorityBindingPlayer.Skills.Add($priorityBindingSkill)
$priorityBindingPlayer.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($priorityBindingSkill, 0))
$priorityBindingEnemy = New-WaveUnit 2 2 2 0 1000
$priorityBindingEnemy.MonsterId = 3201; $priorityBindingEnemy.CharacterType = 99
$priorityBindingCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$priorityBindingCheckpoint.StageId = 22001; $priorityBindingCheckpoint.RandomSeed = 1
$priorityBindingCheckpoint.Units.Add($priorityBindingPlayer); $priorityBindingCheckpoint.Units.Add($priorityBindingEnemy)
$priorityBindingWave = [Zjcs.Combat.Official.FightBattleWaveState]::new()
$priorityBindingWave.Index = 0; $priorityBindingWave.UnitIds.Add(2)
$priorityBindingCheckpoint.Waves.Add($priorityBindingWave)
$priorityBindingRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $priorityBindingCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
    $officialCatalog, $skillItems)
Assert-Combat ($priorityBindingSkill.AiPriorityId -eq 10 -and $priorityBindingSkill.LastAiPriorityId -eq 11 -and ($priorityBindingSkill.AiPriorityTypes -join ',') -eq '11,10,7,1,0,2,9,6' -and ($priorityBindingSkill.LastAiPriorityTypes -join ',') -eq '11,10,7,1,3,2,9,6') 'Battle executor did not bind the skill item AI priority groups'
Assert-Combat ($priorityBindingEnemy.CharacterType -eq 2 -and $priorityBindingEnemy.AiType -eq 1) 'Battle executor did not bind MonsterInfo to unit AI state'

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

# Pinned summon skill execution: spawn pool units, attach their official statuses and register turns.
$idleActor = New-WaveUnit 1 1 1 1 1000
$idleActor.MoveMask = 4
$idleMoveDist = 2
$idleActor.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
    Id = $formula.PropertyId('MoveDist'); Value = $idleMoveDist
})
$coolingSkill = $officialCatalog.CreateSkill(12860, 0, 0, 1, 1, 0, $null)
$idleActor.Skills.Add($coolingSkill)
$idleActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($coolingSkill, 3))
$idleEnemy = New-WaveUnit 2 2 4 1 1000
$idleCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$idleCheckpoint.StageId = 22001; $idleCheckpoint.RandomSeed = 19
$idleCheckpoint.Units.Add($idleActor); $idleCheckpoint.Units.Add($idleEnemy)
$idleCheckpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$idleCheckpoint.Grid.Width = 8; $idleCheckpoint.Grid.Height = 5
$idleRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $idleCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
    $officialCatalog, $skillItems)
$idleStartX = $idleActor.X; $idleStartY = $idleActor.Y
$idleMaximumReachableDistance = 0
for ($x = [Math]::Max(0, $idleStartX - $idleMoveDist); $x -le [Math]::Min($idleCheckpoint.Grid.Width - 1, $idleStartX + $idleMoveDist); $x++) {
    for ($y = [Math]::Max(0, $idleStartY - $idleMoveDist); $y -le [Math]::Min($idleCheckpoint.Grid.Height - 1, $idleStartY + $idleMoveDist); $y++) {
        if ([Math]::Abs($x - $idleStartX) + [Math]::Abs($y - $idleStartY) -gt $idleMoveDist) { continue }
        $distance = [Math]::Abs($x - $idleEnemy.X) + [Math]::Abs($y - $idleEnemy.Y)
        $idleMaximumReachableDistance = [Math]::Max($idleMaximumReachableDistance, $distance)
    }
}
$cooldownBeforeIdle = [Zjcs.Combat.Official.FightSkillState]::new($idleActor.SkillState).LeftCooldown(0)
$idleStep = $idleRunner.Step(0, 1)
$cooldownAfterIdle = [Zjcs.Combat.Official.FightSkillState]::new($idleActor.SkillState).LeftCooldown(0)
$idleFinalDistance = [Math]::Abs($idleActor.X - $idleEnemy.X) + [Math]::Abs($idleActor.Y - $idleEnemy.Y)
Assert-Combat ($idleStep.Event.SkillCasts.Count -eq 0 -and $idleStep.Event.DamageEvents.Count -eq 0 -and $idleStep.Event.Movements.Count -eq 1 -and $idleFinalDistance -eq $idleMaximumReachableDistance -and $idleEnemy.Hp -eq 1000 -and $idleActor.SkillState.Round -eq 1 -and $idleCheckpoint.Counter -eq 1 -and $cooldownAfterIdle -lt $cooldownBeforeIdle) 'No-release turn did not choose the farthest reachable position, advance its round/cooldown and avoid inventing an attack'

$restrictedActor = New-WaveUnit 1 1 1 1 1000
$restrictedActor.MoveMask = 4
$restrictedActor.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{
    Id = $formula.PropertyId('MoveDist'); Value = 1
})
$restrictedSkill = $officialCatalog.CreateSkill(12860, 0, 0, 1, 1, 0, $null)
$restrictedActor.Skills.Add($restrictedSkill)
$restrictedActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($restrictedSkill, 3))
$restrictStatus = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$restrictStatus.ActionType = 51
$restrictedActor.Statuses.Add($restrictStatus)
$restrictedEnemy = New-WaveUnit 2 2 4 1 1000
$restrictedCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$restrictedCheckpoint.StageId = 22001; $restrictedCheckpoint.RandomSeed = 20
$restrictedCheckpoint.Units.Add($restrictedActor); $restrictedCheckpoint.Units.Add($restrictedEnemy)
$restrictedCheckpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$restrictedCheckpoint.Grid.Width = 8; $restrictedCheckpoint.Grid.Height = 5
$restrictedRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $restrictedCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
    $officialCatalog, $skillItems)
$restrictedStep = $restrictedRunner.Step(0, 1)
Assert-Combat ($restrictedStep.Event.SkillCasts.Count -eq 0 -and $restrictedStep.Event.DamageEvents.Count -eq 0 -and $restrictedStep.Event.Movements.Count -eq 0 -and $restrictedActor.SkillState.Round -eq 1 -and $restrictedCheckpoint.Counter -eq 1) 'Restrict did not hold the no-skill actor in place while advancing its round'

$passiveSkill = $skillItems.Required(10128)
Assert-Combat ($passiveSkill.PassiveStatusIds.Count -eq 1 -and $passiveSkill.PassiveStatusIds[0] -eq 25160) 'Pinned passive skill status mapping mismatch'
$passiveOwner = New-WaveUnit 1 1 0 0 1000
$passiveBindings = [System.Collections.Generic.List[Zjcs.Combat.Official.FightOfficialPassiveSkillBinding]]::new()
$passiveBindings.Add([Zjcs.Combat.Official.FightOfficialPassiveSkillBinding]@{
    ItemId = 10128; Rank = 2; Level = 3; SubRank = 1
})
$passiveStatuses = $officialCatalog.CreatePassiveStatusBindings($passiveOwner, $skillItems,
    $passiveBindings)
Assert-Combat ($passiveStatuses.Count -eq 1 -and $passiveStatuses[0].ClassId -eq 25160 -and $passiveStatuses[0].CreatorId -eq 1 -and $passiveStatuses[0].ApplyingSkillItemId -eq 10128 -and $passiveStatuses[0].SkillRank -eq 2 -and $passiveStatuses[0].SkillLevel -eq 3) 'Passive skill did not produce an attributed initial status binding'
$officialCatalog.AssembleUnit($passiveOwner, $null, $passiveStatuses)
$passiveEnemy = New-WaveUnit 2 2 1 0 1000
$passiveCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$passiveCheckpoint.StageId = 22001; $passiveCheckpoint.RandomSeed = 18
$passiveCheckpoint.Units.Add($passiveOwner); $passiveCheckpoint.Units.Add($passiveEnemy)
$passiveRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $passiveCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
    $officialCatalog, $skillItems)
Assert-Combat ($passiveOwner.Statuses.Count -eq 1 -and $passiveOwner.Statuses[0].ClassId -eq 25160 -and $passiveOwner.Statuses[0].StartRound -eq 0) 'Equipped passive status was not initialized at battle start'

# HP accumulated damage passives retain real damage across healing and release their configured skill.
$accumulatingOwner = New-WaveUnit 9 2 0 0 1000
$accumulatingStatus = $officialCatalog.CreateStatus(25185, 9, 9, 1)
$accumulatingOwner.Statuses.Add($accumulatingStatus)
$accumulatingOwner.Hp = 900
$accumulatedSkills = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleStatusStateTrigger]]::new()
$accumulationRandom = [Zjcs.Combat.Official.FightRandomStream]::new(29)
$firstAccumulation = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyAccumulatedDamage(
    $accumulatingOwner, 100, $officialCatalog, $accumulationRandom, $accumulatedSkills)
$accumulatingOwner.Hp = 1000
$secondAccumulation = [Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyAccumulatedDamage(
    $accumulatingOwner, 50, $officialCatalog, $accumulationRandom, $accumulatedSkills)
Assert-Combat ($firstAccumulation -eq 0 -and $secondAccumulation -eq 1 -and $accumulatingStatus.HpAccTriggeredCount -eq 1 -and $accumulatingStatus.HpAccDamageAccumulated -eq 0 -and $accumulatedSkills.Count -eq 1 -and $accumulatedSkills[0].EntityId -eq 58170 -and $accumulatedSkills[0].TargetPos -eq 1) "HP accumulated damage trigger mismatch (first=$firstAccumulation second=$secondAccumulation count=$($accumulatingStatus.HpAccTriggeredCount) accumulated=$($accumulatingStatus.HpAccDamageAccumulated) skills=$($accumulatedSkills.Count))"
$accumulatingStatus.HpAccMaxCount = 1
[Zjcs.Combat.Official.FightStatusTriggerRuntime]::ApplyAccumulatedDamage(
    $accumulatingOwner, 500, $officialCatalog, $accumulationRandom, $accumulatedSkills) | Out-Null
Assert-Combat ($accumulatingStatus.HpAccTriggeredCount -eq 1 -and $accumulatedSkills.Count -eq 1) 'HP accumulated damage trigger exceeded its configured count'

$accumulationPlayer = New-WaveUnit 1 1 0 0 1000000
$accumulationPlayer.Turn.Speed = 1000
$accumulationSkill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$accumulationSkill.Slot = 0; $accumulationSkill.EntityId = 980; $accumulationSkill.ItemId = 981
$accumulationSkill.TargetType = 1; $accumulationSkill.RangeVerified = $true
$accumulationSkill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(2, 0))
$accumulationSkill.Hits.Add([Zjcs.Combat.Official.FightBattleHitDefinition]@{
    EntityId = 982; DamageId = 983; DamageType = 8; HasCustomDamage = $true; CustomDamage = 400
})
$accumulationPlayer.Skills.Add($accumulationSkill)
$accumulationPlayer.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
$accumulationBoss = New-WaveUnit 2 2 2 0 1000
$accumulationBoss.Turn.Speed = 100
$accumulationBoss.Level = 1; $accumulationBoss.SubRank = 1
$battleAccumulationStatus = $officialCatalog.CreateStatus(25185, 2, 2, 1)
$accumulationBoss.Statuses.Add($battleAccumulationStatus)
$accumulationCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$accumulationCheckpoint.StageId = 22001; $accumulationCheckpoint.RandomSeed = 31
$accumulationCheckpoint.Units.Add($accumulationPlayer); $accumulationCheckpoint.Units.Add($accumulationBoss)
$accumulationBattle = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $accumulationCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
    $officialCatalog, $skillItems)
$accumulationStep = $accumulationBattle.Step(0, 1)
$accumulationCasts = @($accumulationStep.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 58170 })
$accumulationCastSummary = @($accumulationCasts | ForEach-Object { '{0}:{1}' -f $_.SkillEntityId, $_.ActorId }) -join ','
Assert-Combat ($accumulationStep.Event.ActorId -eq 1 -and $accumulationCasts.Count -eq 2 -and @($accumulationCasts | Where-Object { $_.ActorId -ne 2 }).Count -eq 0 -and $battleAccumulationStatus.HpAccTriggeredCount -eq 2 -and $battleAccumulationStatus.HpAccDamageAccumulated -eq 100) "HP accumulated damage did not execute both official trigger skills (actor=$($accumulationStep.Event.ActorId) casts=$accumulationCastSummary bossHp=$($accumulationBoss.Hp) triggered=$($battleAccumulationStatus.HpAccTriggeredCount) accumulated=$($battleAccumulationStatus.HpAccDamageAccumulated))"

$summonSkill = $officialCatalog.CreateSkill(58140, 0, 14722, 1, 1, 0, $null)
$summonHit = $summonSkill.HitGraphEntities | Where-Object { $_.EntityId -eq 58141 } | Select-Object -First 1
Assert-Combat ($summonSkill.SummonCountLimit -eq 6 -and $null -ne $summonHit -and $summonHit.IsSummon -and $summonHit.SummonPositions.Count -eq 2 -and $summonHit.SummonPools.Count -eq 2) 'Official hatch summon configuration was not assembled'
Assert-Combat ($officialCatalog.MonsterTemplateId(3907) -eq 3285 -and $officialCatalog.MonsterTemplateId(3908) -eq 3286) 'Official summon monster templates did not resolve'
$alternateSummonSkill = $officialCatalog.CreateSkill(70120, 0, 18012, 1, 1, 0, $null)
$alternateSummonHit = $alternateSummonSkill.HitGraphEntities | Where-Object { $_.EntityId -eq 70121 } | Select-Object -First 1
Assert-Combat ($alternateSummonSkill.SummonCountLimit -eq 6 -and $null -ne $alternateSummonHit -and $alternateSummonHit.SummonPools.Count -eq 2 -and @($alternateSummonHit.SummonPools | Where-Object { $_.MonsterId -notin @(3907,3908) -or $_.MaxSummonCount -ne 3 }).Count -eq 0) 'Alternate hatch summon pools differ from the fixed three-per-type configuration'
$summonPlayer = New-WaveUnit 1 1 0 0 1000
$summonPlayer.Turn.Speed = 100
$summonActor = New-WaveUnit 2 2 10 10 10000
$summonActor.MonsterGroupNo = 0; $summonActor.Level = 1; $summonActor.SubRank = 1; $summonActor.Turn.Speed = 1000
$summonActor.Skills.Add($summonSkill)
$summonActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($summonSkill, 0))
$summonCheckpoint = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$summonCheckpoint.StageId = 22001; $summonCheckpoint.RandomSeed = 17
$summonCheckpoint.Units.Add($summonPlayer); $summonCheckpoint.Units.Add($summonActor)
$summonCheckpoint.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new()
$summonCheckpoint.Grid.Width = 20; $summonCheckpoint.Grid.Height = 20
$summonRunner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
    $summonCheckpoint, $formula, [Zjcs.Combat.Official.FightDamageSettlement]::new(),
    $officialCatalog, $skillItems)
$summonStep = $summonRunner.Step(0, 1)
$summonedUnits = @($summonCheckpoint.Units | Where-Object { $_.MasterId -eq 2 -and $_.MonsterId -in @(3907,3908) })
Assert-Combat ($summonStep.Event.SkillCasts.Count -eq 1 -and $summonStep.Event.SpawnedUnitIds.Count -eq 2 -and $summonedUnits.Count -eq 2) 'Official summon skill did not add both configured summons to the checkpoint'
Assert-Combat (@($summonedUnits | Where-Object { $_.MonsterTemplateId -notin @(3285,3286) }).Count -eq 0) 'Summoned units did not retain their official model template ids'
Assert-Combat (@($summonedUnits | Where-Object { $_.Statuses.ClassId -notcontains 24001 }).Count -eq 0) 'Summoned units did not receive their configured owner-death status'
$turnIds = @($summonCheckpoint.Timeline.Entries | ForEach-Object { $_.Id })
Assert-Combat (@($summonedUnits | Where-Object { $turnIds -notcontains $_.Id }).Count -eq 0) 'Summoned units were not registered in the turn queue'
[Zjcs.Combat.Official.FightBattleCheckpointRules]::Validate($summonCheckpoint, $true)

$repairActor = New-WaveUnit 20 2 10 10 1000
$repairSkill = $officialCatalog.CreateSkill(18480, 0, 14267, 1, 1, 1, $null)
$repairActor.Skills.Add($repairSkill)
$repairActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($repairSkill, 0))
$repairStatus = $officialCatalog.CreateStatus(24314, $repairActor.Id, $repairActor.Id, 1,
    14267, 14267, 1, 1, 1, $repairActor.Properties)
Assert-Combat ($repairStatus.SkillConditionRulesSupported -and $repairStatus.SummonConditionPools.Count -eq 3 -and $repairStatus.ConditionalSkills.Count -gt 0) 'Official tentacle repair summon condition was not assembled'
$repairActor.Statuses.Add($repairStatus)
$repairUnits = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$repairUnits.Add($repairActor)
$repairUnits.Add((New-WaveUnit 21 1 0 0 1000))
$repairSummonIds = @(3265, 3266, 3267)
for ($index = 0; $index -lt $repairSummonIds.Count; $index++) {
    $tentacle = New-WaveUnit (30 + $index) 2 (3 + $index) 7 100
    $tentacle.MasterId = $repairActor.Id; $tentacle.MonsterId = $repairSummonIds[$index]
    $repairUnits.Add($tentacle)
}
$repairReady = [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill(
    $repairActor, $repairUnits, 1)
Assert-Combat ($null -ne $repairReady -and $repairReady.EntityId -eq 18480) 'Tentacle repair skill did not release below four living configured summons'
$fourthTentacle = New-WaveUnit 33 2 6 7 100
$fourthTentacle.MasterId = $repairActor.Id; $fourthTentacle.MonsterId = 3265
$repairUnits.Add($fourthTentacle)
$repairBlocked = [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill(
    $repairActor, $repairUnits, 1)
Assert-Combat ($null -eq $repairBlocked) 'Tentacle repair skill remained available at four living configured summons'

$cappedActor = New-WaveUnit 2 2 10 10 10000
$cappedActor.Skills.Add($summonSkill)
$cappedActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($summonSkill, 0))
$cappedUnits = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$cappedUnits.Add($cappedActor)
for ($index = 0; $index -lt 3; $index++) {
    $ownedSummon = New-WaveUnit (10 + $index) 2 (2 + $index) 2 10
    $ownedSummon.MasterId = 2; $ownedSummon.MonsterId = 3907
    $cappedUnits.Add($ownedSummon)
}
for ($index = 0; $index -lt 3; $index++) {
    $ownedSummon = New-WaveUnit (20 + $index) 2 (5 + $index) 2 10
    $ownedSummon.MasterId = 2; $ownedSummon.MonsterId = 3908
    $cappedUnits.Add($ownedSummon)
}
$blockedSummonSkill = [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill(
    $cappedActor, $cappedUnits, 1)
Assert-Combat ($null -eq $blockedSummonSkill) 'Conditional summon skill remained available at its six-summon cap'

$otherSummonActor = New-WaveUnit 40 2 10 10 10000
$otherSummonActor.Skills.Add($summonSkill)
$otherSummonActor.SkillState.Slots.Add($officialCatalog.CreateSkillSlot($summonSkill, 0))
$otherSummonUnits = [System.Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$otherSummonUnits.Add($otherSummonActor)
for ($index = 0; $index -lt 6; $index++) {
    $unrelatedSummon = New-WaveUnit (50 + $index) 2 (2 + $index) 3 10
    $unrelatedSummon.MasterId = $otherSummonActor.Id; $unrelatedSummon.MonsterId = 3265
    $otherSummonUnits.Add($unrelatedSummon)
}
$unrelatedPoolReady = [Zjcs.Combat.Official.FightBattleTargeting]::FindReadySkill(
    $otherSummonActor, $otherSummonUnits, 1)
Assert-Combat ($null -ne $unrelatedPoolReady -and $unrelatedPoolReady.EntityId -eq 58140) 'Unrelated summon types incorrectly consumed the hatch skill pools'

Write-Output "Unity combat core checks passed: $($reference.cases.Count) pinned formula vectors, official catalog loading and digest validation, AI priority groups and filters, skill/status assembly, HP accumulated damage triggers, no-release turn progression, passive status initialization, status damage reduction, shield and skill cost execution, official chained hit graph, status falloff, battle victory, checkpoint replay, random stream, timeline, targeting, multi-target falloff, cross-wave spawn selection, official summon execution/caps and pool-specific release, and conditional tentacle repair."

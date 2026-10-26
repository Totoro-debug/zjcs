using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public enum FightBattleShieldKind
    {
        None,
        Persistent,
        Disposable,
        Invincible,
        Counted
    }

    [Serializable]
    public sealed class FightBattleResilienceInfo
    {
        public List<int> DisappearStatusIds = new List<int>();
        public int DisappearSkillId;
        public int SkillMaxHpPercent;
        public int SkillAtkPercent;
        public int ResetRound;
    }

    [Serializable]
    public sealed class FightBattleUnitState
    {
        public int Id;
        public int MasterId;
        public int Side;
        public int CharacterType;
        public int MonsterId;
        public int NpcClassId;
        public int MonsterTemplateId;
        public int MonsterGroupNo;
        public int AiType = -1;
        public int AiPreviousTargetId;
        public int MonsterType;
        public int MaxHpPlies = 1;
        public int Level;
        public int SubRank;
        public int Profession;
        public int Wave;
        public int SkillCooldownChangeAtStart;
        public int MoveDist;
        public int MoveMask = 4;
        public bool Targetable = true;
        public int X;
        public int Y;
        public int Direction;
        public long Hp;
        public long MaxHp;
        public long LastMaxHp;
        public List<FightPropertyValue> BaseProperties;
        public long Resilience;
        public long MaxResilience;
        public int BrokenRound = -1;
        public bool Ended;
        public List<FightPropertyValue> Properties = new List<FightPropertyValue>();
        public List<FightBattleSkillDefinition> Skills = new List<FightBattleSkillDefinition>();
        public FightSkillAgentState SkillState = new FightSkillAgentState();
        public FightTurnUnit Turn = new FightTurnUnit();
        public List<FightBattleStatusState> Statuses = new List<FightBattleStatusState>();
        public List<FightBattleStatusApplicationEvent> PendingStatusApplications = new List<FightBattleStatusApplicationEvent>();
        public List<FightBattleStatusState> EndedAccumulatedDamageStatuses = new List<FightBattleStatusState>();
        public List<FightBattleFalloffCounter> FalloffCounts = new List<FightBattleFalloffCounter>();
        public List<FightGridPoint> BodyOffsets = new List<FightGridPoint>
        {
            new FightGridPoint(0, 0)
        };
    }

    [Serializable]
    public sealed class FightBattleStatusApplicationEvent
    {
        public int CreatorId;
        public int RootSkillId;
        public int ApplyingSkillItemId;
        public int StatusType;
    }

    [Serializable]
    public sealed class FightPropertyValue
    {
        public int Id;
        public long Value;
    }

    [Serializable]
    public sealed class FightBattleStatusStateTrigger
    {
        public int EntityId;
        public float BasePercent = 1f;
        public bool AffectedByProp;
        public int Source;
        public int Target;
        public bool UseAI;
        public int TargetPos;
        public bool StackedCountToDamageFactor;
    }

    [Serializable]
    public sealed class FightBattleStatusSkillInvocation
    {
        public FightBattleStatusStateTrigger Trigger;
        public int SourceUnitId;
        public int TargetUnitId;
        public int TargetPositionUnitId;
    }

    [Serializable]
    public sealed class FightBattleStatusState
    {
        public int InstanceId;
        public int ClassId;
        public int StatusType;
        public int ActionType;
        public bool HasBackToLifeComponent;
        // Official FightStatusComponentInfo base fields. DurationRound < 0 means infinite.
        public int DurationRound;
        public int CurDurationRound;
        public int StartRound;
        public int RoundTarget;
        public int RoundUpdateTiming;
        public bool RemoveAtRoundTargetDestroy;
        public int ActionBehOfSameStatus;
        public bool CanDispersed;
        public bool IsAddDurationRound;
        public int MaxAddDurationRound = 100;
        public bool IsResetDurationWhenStack;
        public bool ScalePropWhenChangeStacked;
        public bool ApplyIgnoreResilience;
        public bool ForbiddenOnApplicatorDie;
        public bool AiMoreHit;
        public bool HasCreatorDamageBoost;
        public bool FollowRoundUpdateTimingOnGlobalRound;
        public bool IsFixedProp;
        public int StackCount = 1;
        public int AddStackedCount = 1;
        public int AddStackedRandomMinCount = 1;
        public int AddStackedRandomMaxCount = 1;
        public int MaxStackedCount;
        public bool IsOpenStack;
        public int OwnerId;
        public int CreatorId;
        public int RootSkillId;
        public int ApplyingSkillItemId;
        public int FalloffTarget;
        public int DamageId;
        public int[] RootSkillIds = Array.Empty<int>();
        public int SkillEntityClassId;
        public bool IsCheckSkillTrigger;
        public bool RemoveOnSourceSkillStart;
        public List<FightBattleSummonPool> SummonConditionPools =
            new List<FightBattleSummonPool>();
        public List<FightBattleStatusSkillCondition> ConditionalSkills =
            new List<FightBattleStatusSkillCondition>();
        public int NumOfStart = 1;
        public int MaxFalloffCount = -1;
        public int FalloffPercent;
        public double Rate = 1.0;
        public int Scope;
        public List<FightBattleFalloffCounter> Counts = new List<FightBattleFalloffCounter>();
        // Official FightStatusHpDecreaseUnitComponent: trigger statuses when the owner HP ratio
        // stays at or below UnitHpPer, up to MaxCount times.
        public float HpTriggerPer = -1f;
        public int HpTriggerMaxCount = -1;
        public int HpTriggeredCount;
        public bool HpTriggerAtStart;
        public List<int> HpTriggerStatuses = new List<int>();
        public List<int> HpTriggerSkills = new List<int>();
        public bool HasRoundStartTrigger;
        public bool RoundStartRulesSupported;
        public int RoundStartListenTarget;
        public int RoundStartDelayRound;
        public float RoundStartRate = 1f;
        public int RoundStartMaxCount = -1;
        public int RoundStartMeetCount = 1;
        public int RoundStartTriggeredCount;
        public bool RoundStartTryAtStart;
        public List<FightBattleStatusStateTrigger> RoundStartSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasRoundEndTrigger;
        public bool RoundEndRulesSupported;
        public int RoundEndListenTarget;
        public int RoundEndMaxCount = -1;
        public int RoundEndTriggeredCount;
        public bool RoundEndAutoRemove;
        public List<FightBattleStatusStateTrigger> RoundEndSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasSkillStartTrigger;
        public bool SkillStartRulesSupported;
        public int SkillStartSource;
        public int SkillStartMaxCount = -1;
        public int SkillStartTriggeredCount;
        public List<int> SkillStartActionTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> SkillStartSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasSkillEndTrigger;
        public bool SkillEndRulesSupported;
        public int SkillEndMetCount = 1;
        public int SkillEndMetCountProgress;
        public int SkillEndTriggeredCount;
        public List<int> SkillEndFilteredActionTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> SkillEndStatuses =
            new List<FightBattleStatusStateTrigger>();
        // Official FightStatusHpAccDecreaseComponent. Damage is accumulated independently of cures
        // per the confirmed offline rule; the configured threshold is consumed on each trigger.
        public bool HasHpAccDecrease;
        public FightBattleStatusAccumulatedDamage AccumulatedDamage;
        public long HpAccUnitHp = -1;
        public float HpAccUnitHpPercentage = -1f;
        public bool HpAccResetAtRoundEnd;
        public int HpAccMaxCount = -1;
        public bool HpAccUpdateHpPerOnCure = true;
        public long HpAccDamageAccumulated;
        public int HpAccTriggeredCount;
        public List<FightBattleStatusStateTrigger> HpAccTriggerStatuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> HpAccTriggerSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasUnflinchingOnDamageComponent;
        public float UnflinchingOnDamageRate = 1f;
        public bool HasSkillCooldownModify;
        public int SkillCooldownModify;
        public bool SkillCooldownModifyIsRandom;
        public int SkillCooldownModifyCount = -1;
        public int SkillCooldownModifyOffset;
        public bool SkillCooldownModifyInitialized;
        public bool SkillCooldownModifyPending;
        public int SkillCooldownActionPendingCount;
        public List<FightBattleStatusStateTrigger> SkillCooldownModifySkills =
            new List<FightBattleStatusStateTrigger>();
        public bool SkillCooldownModifySkillsPending;
        public bool HasPermanentSkillCooldownReduction;
        public int PermanentSkillCooldownReduction;
        public bool PermanentSkillCooldownReductionApplied;
        public bool SkillConditionRulesSupported = true;
        public List<FightBattleSkillConditionRule> SkillConditionRules =
            new List<FightBattleSkillConditionRule>();
        public int SourceSkillRank = 1;
        public int SourceSkillLevel = 1;
        public int SourceSubRank;
        public bool PropertyDefinitionResolved;
        public bool HasPropertyComponent;
        public List<FightPropertyValue> SourceProperties;
        public List<FightBattleStatusPropertyTransition> PropertyTransitions =
            new List<FightBattleStatusPropertyTransition>();
        public List<FightPropertyValue> ResolvedProperties;
        public bool HasDamageCustomProps;
        public float DamageCustomPropsRate = 1f;
        public float DamageCustomPropsDmgGreaterMaxHpPer = -1f;
        public int DamageCustomPropsMaxCount = -1;
        public bool DamageCustomPropsAutoRemove = true;
        public bool DamageCustomPropsCheckSkillTrigger = true;
        public int DamageCustomPropsPropertyId;
        public int DamageCustomPropsPhase;
        public int DamageCustomPropsTriggeredCount;
        public FightBattleStatusDamageReducePerInfo DamageReducePer;
        public FightBattleShieldKind ShieldKind;
        public float ShieldRate = 1f;
        public float ShieldDamageGreaterMaxHpPer = -1f;
        public int ShieldDestroyedSkillId;
        public int ShieldInitialCount = -1;
        public int ShieldRemainCount = -1;
        public bool ShieldKeepRemainValue;
        public bool ShieldIgnoreValue;
        public bool ShieldUsesCustomValue;
        public long CustomShieldValue;
        public long ShieldValue;
        public long ShieldRemainValue;
        public bool ShieldInitialized;
        // Set only by a successful new lifecycle application, never inferred from a restored shield.
        public bool ShieldGrantPending;
    }

    [Serializable]
    public sealed class FightBattleStatusPropertyTransition
    {
        public int PropertyId;
        public int TargetType;
        public int TargetPropertyId;
        public int AdditionPropertyId;
        public int FixedAdditionPropertyId;
    }

    [Serializable]
    public sealed class FightBattleSummonPool
    {
        public int MonsterId;
        public int MaxSummonCount;
        public List<int> StatusIds = new List<int>();
    }

    [Serializable]
    public sealed class FightBattleStatusSkillCondition
    {
        public int SkillEntityId;
        public bool IsActive;
        public bool IsNoMeetSetNegation;
    }

    [Serializable]
    public sealed class FightBattleSkillConditionRule
    {
        public int ConditionType;
        public float HpPerValue = -1f;
        public int Round = -1;
        public int ContrastiveRelation;
        public int RoundTargetType;
        public int SummonId;
        public int Count = -1;
        public int GlobalRound = -1;
        public bool DurationRound = true;
    }

    [Serializable]
    public sealed class FightBattleFalloffCounter
    {
        public int CastId;
        public int StatusClassId;
        public int Count;
    }

    public enum FightSkillProfessionAvailability
    {
        Unknown,
        Available,
        Mismatch
    }

    [Serializable]
    public sealed class FightBattleSkillDefinition
    {
        public int Slot;
        public int ItemId;
        public int EntityId;
        public int Rank;
        public int Level;
        public int SubRank;
        public FightSkillProfessionAvailability ProfessionAvailability;
        public int SkillType;
        public int ActionType;
        public int HitTargetActionType;
        public List<int> BlockingSourceActionTypes = new List<int>();
        public List<int> BlockingTargetActionTypes = new List<int>();
        public int SkillTargetType;
        public int ElementType;
        public bool NeedConditionStatusMeetToRelease;
        public int SummonCountLimit = -1;
        public List<int> SummonCountMonsterIds = new List<int>();
        public int RootEntityId;
        public int LimitedTimes = -1;
        public bool ResetCooldownAtStart;
        public int CostType;
        public long SkillCost;
        public bool MustHaveValidCost;
        public bool CanTriggerChild = true;
        public List<int> CanTriggerStatusEntityIds = new List<int>();
        public bool ResetReleasedTimeOnNextWave;
        public int FalloffCastId;
        public List<FightBattleFalloffCounter> FalloffCounts = new List<FightBattleFalloffCounter>();
        public List<int> HitGraphRootEntityIds = new List<int>();
        public List<FightBattleHitRoot> HitGraphRoots = new List<FightBattleHitRoot>();
        public int HitGraphRootEntityId;
        // Official FightSkillComponentInfo action/lifetime durations (seconds, -1 = never ends by
        // itself; the "AllEnded"/"AllRelevantEnded" values are relative to the last hit/move end).
        public float ActionDuration = -1f;
        public float AllEndedActionDuration = -1f;
        public float AllRelevantEndedActionDuration = -1f;
        public float Duration = -1f;
        public float AllEndedDuration = -1f;
        public float AllRelevantEndedDuration = -1f;
        public List<FightBattleHitEntity> HitGraphEntities = new List<FightBattleHitEntity>();
        public bool HasCustomDamage;
        public long CustomDamage;
        public int TargetType = 1;
        public List<FightGridPoint> Range = new List<FightGridPoint>();
        public bool IsAI;
        public int AiPriorityId = -1;
        public int LastAiPriorityId = -1;
        public List<int> AiPriorityTypes = new List<int>();
        public List<int> LastAiPriorityTypes = new List<int>();
        public bool UseLastAiPriority;
        public int PriorityProperty;
        public bool PriorityFindMax = true;
        public int PriorityCharacterType = -1;
        public bool ReleaseWhileRidicule;
        public bool ReleaseWhileConfusion;
        public List<int> IgnoreStatusIds = new List<int>();
        public List<int> HpLowerCharacterTypeFilter = new List<int>();
        public List<List<int>> PriorityCharacterTypeGroups = new List<List<int>>();
        public bool RangeVerified = true;
        public bool HasCustomCure;
        public long CustomCure;
        public List<FightPropertyValue> Properties = new List<FightPropertyValue>();
        public List<FightBattleHitDefinition> Hits = new List<FightBattleHitDefinition>();
        // Official FightSKillMoveCfg entries from FightSkillComponent.SourceMoveList (sorted by
        // Delay). The executor evaluates the fixed target-distance condition; unknown types fail closed.
        public List<FightBattleSkillMove> SourceMoves = new List<FightBattleSkillMove>();
        // Official FightSkillExtSkillComponent: extra skills released when the action ends.
        public List<FightBattleSkillExtra> ExtraSkills = new List<FightBattleSkillExtra>();
        // Official FightSkillReplayComponent: replay the skill when the owner holds a configured
        // status stack count (optionally consuming it).
        public bool HasReplay;
        public int ReplayStatusId;
        public int ReplayStackCount;
        public int ReplayRelation;
        public bool ReplayConsumeStack;
        public int ReplayConsumeCount;
    }

    [Serializable]
    public sealed class FightBattleSkillExtra
    {
        public int SkillId;
        public float Rate = 1f;
    }

    [Serializable]
    public sealed class FightBattleSkillMove
    {
        public int MoveBehaviourId;
        public float Delay;
        public float MoveDuration = -1f;
        public float MoveSpeed = -1f;
        public int OffsetX;
        public int OffsetY;
        public bool TowardsTargetPos;
        public int Direction;
        public int Distance = -1;
        public float BasePercent = 1f;
        public bool AffectedByProp;
        public bool CancelDiagonally;
        public bool HasConditions;
        public List<FightBattleSkillMoveCondition> Conditions = new List<FightBattleSkillMoveCondition>();
    }

    [Serializable]
    public sealed class FightBattleSkillMoveCondition
    {
        public string Type;
        public int Distance;
    }

    [Serializable]
    public sealed class FightBattleHitDefinition
    {
        public int EntityId;
        public int DamageId;
        public int DamageType;
        public int BasicProperty;
        public int DamageProperty;
        public int FixedDamageProperty;
        public long DamageParam;
        public int TargetMode;
        public int RandomCount = 1;
        public int RandomTargetGroupId;
        public int RandomTargetAnchor;
        public bool RandomTargetNotSameTarget;
        public bool RandomTargetAllowEmptyScope;
        public bool RandomTargetNotSameEmptyScope;
        public bool RandomTargetNotAllowHitEmpty;
        public bool RandomTargetNotSameGrid;
        public bool RandomTargetFilterOutOfGroundScope = true;
        public List<FightGridPoint> RandomTargetScope = new List<FightGridPoint>();
        // Official ActionType from FightHitBaseComponentInfo.DamageActionType: 4 Damage, 5 Cure,
        // 0 None. Synthetic definitions default to Damage.
        public int ActionType = 4;
        public float Delay;
        public float DelayDistFactor;
        public int Anchor;
        public bool IgnoreAICount;
        public bool TrueDamage;
        public bool BreakResilience;
        // Official HitScopCfg.Pos offsets: the damage area around the hit anchor. Empty means the
        // hit only applies to the selected target(s).
        public List<FightGridPoint> Scope = new List<FightGridPoint>();
        public bool AllowEmptyScope;
        // Official FightDamageComponent.StatusList entries applied when this hit lands.
        public List<FightBattleStatusApplication> AppliedStatuses = new List<FightBattleStatusApplication>();
        public bool StatusListRandomOne;
        // Official FightHitExtHemophagiaComponent.HemophagiaPropType (0 = none).
        public int HemophagiaProperty;
        // Official FightDamageComponent.ChildSkillCfg: child skill triggered on the damaged target.
        public int ChildSkillId;
        public float ChildSkillRate = 1f;
        public float ChildSkillLessThanHpPer = -1f;
        // Official FightDamageComponent.DisperseStatus (status types, count, chance, random pick).
        public float DisperseChance;
        public bool DisperseAffectedByProp;
        public List<int> DisperseStatusTypes = new List<int>();
        public int DisperseCount;
        public bool DisperseRandom;
        // Official FightDamageComponent.MoveCfg: displacement applied to the damaged unit.
        public FightBattleSkillMove HitMove;
        public bool HasCustomDamage;
        public long CustomDamage;
        public double DamageRate = 1.0;
        public bool UseDamageRate;
        public bool IgnoreShield;
        public bool HasFixedDamage;
        public bool IsSummon;
        public bool SummonIsRandom;
        public bool SummonImmediateRound;
        public bool SummonInheritProps;
        public int SummonReferenceType;
        public int SummonLevelType;
        public int SummonLifespanStatusId;
        public List<FightBattleSummonPool> SummonPools = new List<FightBattleSummonPool>();
        public List<int> SummonStatusIds = new List<int>();
        public List<FightGridPoint> SummonPositions = new List<FightGridPoint>();
    }

    [Serializable]
    public sealed class FightBattleHitEntity
    {
        public int EntityId;
        public int DamageId;
        public bool HasFixedDamage;
        public int DamageType;
        public int BasicProperty = 1;
        public int DamageProperty;
        public int FixedDamageProperty;
        public long DamageParam;
        public List<int> ChainedHits = new List<int>();
        public List<int> CustomTargetHits = new List<int>();
        public List<int> RandomTargetHits = new List<int>();
        public int MiniHitTargetCount = 1;
        public bool NotSameTarget;
        public bool NotSameEmptyScope;
        public bool NotAllowHitEmpty;
        public bool NotSameGrid;
        public bool FilterOutOfGroundScope = true;
        // Reference configuration of the edge that introduced this hit node (HitCfg.Delay and
        // HitCfg.ActOnSouce anchor). The same entity can be referenced from several parents; the
        // first reference wins for the shared node.
        public float Delay;
        public float DelayDistFactor;
        public int Anchor;
        public bool IgnoreAICount;
        public bool TrueDamage;
        public bool BreakResilience;
        public int ActionType = 4;
        public List<FightGridPoint> Scope = new List<FightGridPoint>();
        public bool AllowEmptyScope;
        public bool IgnoreShield;
        public bool IsSummon;
        public bool SummonIsRandom;
        public bool SummonImmediateRound;
        public bool SummonInheritProps;
        public int SummonReferenceType;
        public int SummonLevelType;
        public int SummonLifespanStatusId;
        public List<FightBattleSummonPool> SummonPools = new List<FightBattleSummonPool>();
        public List<int> SummonStatusIds = new List<int>();
        public List<FightGridPoint> SummonPositions = new List<FightGridPoint>();
        public List<FightBattleStatusApplication> AppliedStatuses = new List<FightBattleStatusApplication>();
        public bool StatusListRandomOne;
        public int HemophagiaProperty;
        public int ChildSkillId;
        public float ChildSkillRate = 1f;
        public float ChildSkillLessThanHpPer = -1f;
        public float DisperseChance;
        public bool DisperseAffectedByProp;
        public List<int> DisperseStatusTypes = new List<int>();
        public int DisperseCount;
        public bool DisperseRandom;
        public FightBattleSkillMove HitMove;
    }

    [Serializable]
    public sealed class FightBattleStatusApplication
    {
        public int StatusId;
        // 1 = skill source, 2 = damage target (official DamageStatusCfg.ETargetType).
        public int TargetType = 2;
        public float Chance = 1f;
        public bool AffectedByProp = true;
        public List<FightBattleStatusRateOverride> CharacterTypeRateOverrides =
            new List<FightBattleStatusRateOverride>();
        public float LessThanHpPer = -1f;
        public bool SkillCostHp;
        public float DamageCostHpPer = -1f;
        public long CustomShieldValue;
    }

    [Serializable]
    public sealed class FightBattleStatusRateOverride
    {
        public int CharacterType;
        public float Rate;
        public bool AffectedByProp;
    }

    [Serializable]
    public sealed class FightBattleHitRoot
    {
        public int EntityId;
        public float Delay;
        public int Anchor;
        public bool IgnoreAICount;
    }

    [Serializable]
    public sealed class FightBattleWaveState
    {
        public int Index;
        public List<int> UnitIds = new List<int>();
        public bool Spawned;
    }

    [Serializable]
    public sealed class FightBattleOutcome
    {
        public bool Finished;
        public bool Won;
        public int Reason;
        public int Round;
        public long PlayerHp;
        public long EnemyHp;
        public long EnemyMaxHp;
    }

    [Serializable]
    public sealed class FightBattleStatusGroup
    {
        public int Key;
        public List<int> Status = new List<int>();
    }

    /// <summary>
    /// Official <c>fight_stage_status</c> row: which statuses the battle stage applies to the stage
    /// itself, to each character type, to target ids, to professions, and every N global rounds.
    /// </summary>
    [Serializable]
    public sealed class FightBattleStageStatusConfig
    {
        public int ClassId;
        public List<int> Status = new List<int>();
        public List<FightBattleStatusGroup> CharacterStatus = new List<FightBattleStatusGroup>();
        public List<int> GlobalRoundStatus = new List<int>();
        public List<FightBattleStatusGroup> FactionGlobalRoundStatus = new List<FightBattleStatusGroup>();
        public List<int> TargetIdStatus = new List<int>();
        public List<FightBattleStatusGroup> ProfessionStatus = new List<FightBattleStatusGroup>();
        public float GlobalRoundStatusDelay;
        public bool ReleaseEveryGlobalRound;
        public List<int> ViewStatusBlackList = new List<int>();
    }

    [Serializable]
    public sealed class FightBattleCheckpoint
    {
        public const int CurrentSchema = 1;

        public int Schema = CurrentSchema;
        public int StageId;
        public int StageType;
        public bool IsPvp;
        public int Mode;
        public int MaxRound = 25;
        // Official StageInfo player-round limit (IsPlayerRoundLimit/PlayerRoundLimit). Zero or a
        // disabled flag keeps the global MaxRound as the only limit.
        public bool IsPlayerRoundLimit;
        public int PlayerRoundLimit;
        public int Sequence;
        public int Counter;
        public int RandomSeed;
        public int DamageEventSerial;
        public int FalloffSerial;
        public int StatusInstanceSerial;
        public int SummonSerial;
        public int WaveIndex;
        public bool Finished;
        public List<FightBattleUnitState> Units = new List<FightBattleUnitState>();
        public List<FightBattleWaveState> Waves = new List<FightBattleWaveState>();
        public FightBattleStageStatusConfig StageStatus;
        public FightBattleGrid Grid;
        public FightTurnQueueState Timeline = new FightTurnQueueState();
        public FightBattleOutcome Outcome = new FightBattleOutcome();
        public FightBattleStatistics Statistics = new FightBattleStatistics();
        public List<FightBattleStepReceipt> Receipts = new List<FightBattleStepReceipt>();
    }

    [Serializable]
    public sealed class FightBattleStepReceipt
    {
        public int Sequence;
        public int Counter;
        public FightBattleStepEvent Event;
    }

    [Serializable]
    public sealed class FightBattleStepEvent
    {
        public int ActorId;
        public int ActorRound;
        public int GlobalRound;
        public int Wave;
        public int SkillEntityId;
        public int SkillItemId;
        public int Kind;
        // Presentation pacing resolved from the official skill action/lifetime configuration and
        // the configured hit delays. Negative action duration means the skill never ends by itself.
        public float ActionDuration = -1f;
        public float Duration = -1f;
        public List<int> TargetIds = new List<int>();
        public List<FightBattleHpChange> HpChanges = new List<FightBattleHpChange>();
        public List<FightBattleDamageEvent> DamageEvents = new List<FightBattleDamageEvent>();
        public List<FightBattleMovementEvent> Movements = new List<FightBattleMovementEvent>();
        public List<FightBattleSkillCastEvent> SkillCasts = new List<FightBattleSkillCastEvent>();
        public List<FightBattleStatusCustomActionEvent> StatusCustomActions =
            new List<FightBattleStatusCustomActionEvent>();
        public List<int> SpawnedUnitIds = new List<int>();
        // One notification per successful disperse operation; ordinary expiration is not a disperse.
        public List<int> DispersedUnitIds = new List<int>();
    }

    [Serializable]
    public sealed class FightBattleStatusCustomActionEvent
    {
        public int UnitId;
        public int Action;
    }

    [Serializable]
    public sealed class FightBattleMovementEvent
    {
        public int UnitId;
        public float Delay;
        public float MoveDuration = -1f;
        public float MoveSpeed = -1f;
        public List<FightGridPoint> Path = new List<FightGridPoint>();
    }

    [Serializable]
    public sealed class FightBattleSkillCastEvent
    {
        public int ActorId;
        public int Slot;
        public int SkillEntityId;
        public int SkillItemId;
        public bool IsReplay;
        public int ReplayType; // Official EReplaySkillType: Normal=0, Extra=1, ExtraWithTip=2.
        public List<int> TargetIds = new List<int>();
    }

    [Serializable]
    public sealed class FightBattleHpChange
    {
        public int UnitId;
        public long Before;
        public long After;
        public long MaxHp;
    }

    public static class FightBattleCheckpointRules
    {
        public static void Validate(FightBattleCheckpoint checkpoint, bool requireSpawnState = false)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.Schema != FightBattleCheckpoint.CurrentSchema)
                throw new ArgumentException("Unsupported fight checkpoint schema.", nameof(checkpoint));
            if (checkpoint.StageId <= 0 || checkpoint.MaxRound <= 0 || checkpoint.Sequence < 0
                || checkpoint.Counter < 0 || checkpoint.DamageEventSerial < 0 || checkpoint.RandomSeed < 1
                || checkpoint.RandomSeed >= FightRandomStream.Modulus || checkpoint.FalloffSerial < 0
                || checkpoint.StatusInstanceSerial < 0 || checkpoint.SummonSerial < 0
                || checkpoint.PlayerRoundLimit < 0
                || checkpoint.WaveIndex < 0 || checkpoint.WaveIndex >= Math.Max(1, checkpoint.Waves?.Count ?? 0))
                throw new ArgumentException("Fight checkpoint header is invalid.", nameof(checkpoint));
            if (checkpoint.Units == null || checkpoint.Waves == null || checkpoint.Timeline == null
                || checkpoint.Outcome == null || checkpoint.Receipts == null)
                throw new ArgumentException("Fight checkpoint is missing required state.", nameof(checkpoint));
            if (checkpoint.Timeline.Entries == null || checkpoint.Timeline.PendingChanges == null
                || checkpoint.Timeline.Round < 0 || checkpoint.Timeline.Current < 0
                || checkpoint.Timeline.Acting < 0 || checkpoint.Outcome.PlayerHp < 0
                || checkpoint.Outcome.EnemyHp < 0 || checkpoint.Outcome.EnemyMaxHp < 0
                || checkpoint.Outcome.EnemyHp > checkpoint.Outcome.EnemyMaxHp
                || checkpoint.Outcome.Finished != checkpoint.Finished)
                throw new ArgumentException("Fight checkpoint timeline or outcome is invalid.", nameof(checkpoint));
            FightBattleStageStatusConfig stageStatus = checkpoint.StageStatus;
            if (stageStatus != null)
            {
                if (stageStatus.ClassId < 0 || stageStatus.GlobalRoundStatusDelay < 0f
                    || stageStatus.Status == null || stageStatus.GlobalRoundStatus == null
                    || stageStatus.TargetIdStatus == null || stageStatus.ViewStatusBlackList == null
                    || stageStatus.CharacterStatus == null || stageStatus.FactionGlobalRoundStatus == null
                    || stageStatus.ProfessionStatus == null)
                    throw new ArgumentException("Fight checkpoint stage status is invalid.", nameof(checkpoint));
                foreach (FightBattleStatusGroup group in stageStatus.CharacterStatus)
                    if (group == null || group.Status == null)
                        throw new ArgumentException("Fight checkpoint stage character status is invalid.",
                            nameof(checkpoint));
                foreach (FightBattleStatusGroup group in stageStatus.FactionGlobalRoundStatus)
                    if (group == null || group.Status == null)
                        throw new ArgumentException("Fight checkpoint stage faction status is invalid.",
                            nameof(checkpoint));
                foreach (FightBattleStatusGroup group in stageStatus.ProfessionStatus)
                    if (group == null || group.Status == null)
                        throw new ArgumentException("Fight checkpoint stage profession status is invalid.",
                            nameof(checkpoint));
            }
            // Statistics were added after the first checkpoints existed; treat missing collections as
            // an empty accumulator instead of rejecting an otherwise valid running battle.
            if (checkpoint.Statistics == null) checkpoint.Statistics = new FightBattleStatistics();
            if (checkpoint.Statistics.Units == null)
                checkpoint.Statistics.Units = new List<FightBattleUnitStatistics>();
            if (checkpoint.Statistics.Skills == null)
                checkpoint.Statistics.Skills = new List<FightBattleSkillStatistics>();

            var ids = new HashSet<int>();
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                if (unit == null || unit.Id <= 0 || !ids.Add(unit.Id) || unit.Hp < 0
                    || unit.MaxHp < 0 || unit.Hp > unit.MaxHp || unit.MaxHpPlies < 0 || unit.MaxResilience < 0
                    || unit.Resilience < 0 || unit.Resilience > unit.MaxResilience
                    || unit.Properties == null || unit.Skills == null || unit.SkillState == null || unit.Turn == null
                    || unit.Statuses == null || unit.FalloffCounts == null)
                    throw new ArgumentException("Fight checkpoint contains an invalid unit.", nameof(checkpoint));
                if (unit.Turn.Id != unit.Id || unit.Turn.MasterId != unit.MasterId)
                    throw new ArgumentException("Fight turn state does not match its unit.", nameof(checkpoint));
                if (unit.Turn.RoundOffset < 0 || unit.Turn.ActionRound < 0)
                    throw new ArgumentException("Fight unit round state is invalid.", nameof(checkpoint));
                foreach (FightBattleSkillDefinition skill in unit.Skills)
                    if (skill != null && (skill.Properties == null || skill.Hits == null
                        || skill.FalloffCounts == null || skill.HitGraphRootEntityIds == null
                        || skill.HitGraphEntities == null || skill.Range == null))
                        throw new ArgumentException("Fight checkpoint contains an incomplete skill definition.", nameof(checkpoint));
            }

            var waveUnitIds = new HashSet<int>();
            var unitById = new Dictionary<int, FightBattleUnitState>();
            foreach (FightBattleUnitState unit in checkpoint.Units) unitById[unit.Id] = unit;
            for (int i = 0; i < checkpoint.Waves.Count; i++)
            {
                FightBattleWaveState wave = checkpoint.Waves[i];
                if (wave == null || wave.UnitIds == null)
                    throw new ArgumentException("Fight checkpoint contains an invalid wave.", nameof(checkpoint));
                foreach (int id in wave.UnitIds)
                    if (!ids.Contains(id) || !waveUnitIds.Add(id))
                        throw new ArgumentException("Fight wave contains an unknown or duplicate unit.", nameof(checkpoint));
                if (i <= checkpoint.WaveIndex || !requireSpawnState) continue;
                if (wave.Spawned)
                    throw new ArgumentException(
                        "Fight checkpoint marks a future wave as already spawned.", nameof(checkpoint));
                foreach (int id in wave.UnitIds)
                    if (!unitById[id].Ended)
                        throw new ArgumentException(
                            "Fight checkpoint contains an unspawned unit that is still selectable.",
                            nameof(checkpoint));
            }

            var entryIds = new HashSet<int>();
            long previousNext = -1;
            if (checkpoint.Timeline.Acting > 0 && !ids.Contains(checkpoint.Timeline.Acting))
                throw new ArgumentException("Fight timeline acting unit is unknown.", nameof(checkpoint));
            foreach (FightTurnEntry entry in checkpoint.Timeline.Entries)
            {
                if (entry == null || !ids.Contains(entry.Id) || !entryIds.Add(entry.Id)
                    || entry.Pre < 0 || entry.Next < entry.Pre || entry.Next < previousNext)
                    throw new ArgumentException("Fight timeline contains an invalid or unsorted entry.", nameof(checkpoint));
                previousNext = entry.Next;
            }
            foreach (FightRoundIntervalChange change in checkpoint.Timeline.PendingChanges)
                if (change == null || !ids.Contains(change.UnitId)
                    || float.IsNaN(change.Percent) || float.IsInfinity(change.Percent))
                    throw new ArgumentException("Fight timeline contains an invalid pending change.", nameof(checkpoint));

            if (checkpoint.Counter != checkpoint.Receipts.Count)
                throw new ArgumentException("Fight checkpoint step receipt count is inconsistent.", nameof(checkpoint));
            for (int i = 0; i < checkpoint.Receipts.Count; i++)
            {
                FightBattleStepReceipt receipt = checkpoint.Receipts[i];
                if (receipt == null || receipt.Event == null || receipt.Sequence != checkpoint.Sequence
                    || receipt.Counter != i + 1)
                    throw new ArgumentException("Fight checkpoint contains an invalid step receipt.", nameof(checkpoint));
            }
        }
    }
}

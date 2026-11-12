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
        public int RoleEntityId;
        public int MonsterTemplateId;
        public int MonsterGroupNo;
        public int AiType = -1;
        public int AiPreviousTargetId;
        public int MonsterType;
        public int MaxHpPlies = 1;
        public int Level;
        public int SubRank;
        public int Profession;
        // Immutable entry score for the confirmed offline revival target ordering.
        public long EntryCombatRating;
        public int Wave;
        public int SkillCooldownChangeAtStart;
        public int MoveDist;
        public int MoveMask = 4;
        public bool Targetable = true;
        public int X;
        public int Y;
        public int Direction;
        // Explicit validity retains the existing initial facing for older checkpoints.
        public bool HasFlipX;
        public bool FlipX;
        public long Hp;
        public long MaxHp;
        public long LastMaxHp;
        public List<FightPropertyValue> BaseProperties;
        public long Resilience;
        public long MaxResilience;
        public int BrokenRound = -1;
        public bool Ended;
        // Persist lifecycle receipt: restoring a checkpoint must not release an ended
        // summon's removal skill for a second time.
        public bool HitSummonRemoveProcessed;
        public bool HitSummonHasLifeDuration;
        public long LifeReductionStatusSerial;
        public FightVoodooDollLink VoodooDoll;
        // JsonUtility materializes null inline classes as empty objects. A real
        // substitute always has a positive linked target identity.
        public bool IsVoodooDoll => VoodooDoll != null && VoodooDoll.TargetId > 0;
        public List<FightPropertyValue> Properties = new List<FightPropertyValue>();
        public List<FightBattleSkillDefinition> Skills = new List<FightBattleSkillDefinition>();
        // Original report items retain rank/level even after their passive status expires.
        public List<FightBattleSkillDefinition> ReportSkillSnapshots = new List<FightBattleSkillDefinition>();
        public FightSkillAgentState SkillState = new FightSkillAgentState();
        public FightTurnUnit Turn = new FightTurnUnit();
        public List<FightBattleStatusState> Statuses = new List<FightBattleStatusState>();
        // Runtime-only subscription, scoped to an executor step. Checkpoints contain public fields.
        internal Action<FightBattleUnitState, FightBattleStatusState> StatusStarted;
        internal Action<FightBattleUnitState, FightBattleStatusState> StatusEnded;
        [NonSerialized] internal Action<FightBattleUnitState> HpUpdated;
        [NonSerialized] internal Action<FightBattleUnitState, long> HpIncreased;
        internal Action<FightBattleUnitState, FightBattleStatusState, bool> StatusForbiddenChanged;
        internal Func<FightBattleStatusState, int> StatusRoundResolver;
        internal Func<FightBattleStatusState, int> RoundStartClockResolver;
        // Official FightRolePetTransferEntity state. A player unit owns the transfer state while
        // the configured pet skill is released at the owner's action end.
        public FightBattlePetEnergyState PetEnergy = new FightBattlePetEnergyState();
        public List<FightBattleStatusApplicationEvent> PendingStatusApplications = new List<FightBattleStatusApplicationEvent>();
        public List<FightBattleStatusState> EndedAccumulatedDamageStatuses = new List<FightBattleStatusState>();
        public List<FightBattleFalloffCounter> FalloffCounts = new List<FightBattleFalloffCounter>();
        public List<FightGridPoint> BodyOffsets = new List<FightGridPoint>
        {
            new FightGridPoint(0, 0)
        };

        // Position probes share read-only combat inputs; they never settle an action or status.
        internal FightBattleUnitState ProbeAt(FightGridPoint position)
        {
            var probe = (FightBattleUnitState)MemberwiseClone();
            probe.X = position.X;
            probe.Y = position.Y;
            probe.HasFlipX = true;
            probe.FlipX = position.X == X ? (HasFlipX ? FlipX : Direction == 1) : position.X < X;
            int dx = position.X - X, dy = position.Y - Y;
            if (dx != 0 || dy != 0)
                probe.Direction = Math.Abs(dx) >= Math.Abs(dy)
                    ? dx < 0 ? 1 : 3 : dy > 0 ? 2 : 0;
            return probe;
        }
    }

    [Serializable]
    public sealed class FightBattlePetEnergyState
    {
        public bool Enabled;
        public int OwnerUnitId;
        public int PetUnitId;
        public int PetClassId;
        public int PetEntityClassId;
        public int PetEvolutionPhase;
        public int SkillEntityId;
        public int SkillItemId;
        public int SkillRank = 1;
        public int SkillLevel = 1;
        public int SkillSubRank;
        public List<FightPropertyValue> SkillProperties = new List<FightPropertyValue>();
        // The transfer caster and configured FightHitSummonPetCfg entity both use
        // these assembled pet properties and saved active/passive skills.
        public List<FightPropertyValue> EntityProperties = new List<FightPropertyValue>();
        public List<FightBattleSkillDefinition> EntityActiveSkills = new List<FightBattleSkillDefinition>();
        public List<int> EntityPassiveStatusIds = new List<int>();
        public double CurrentEnergy;
        public double InitialEnergy;
        public double NeedEnergy;
        public double NaturalRecoverEnergy;
        public double MasterLoseHpRecoverEnergy;
        public double MasterLoseHpTriggerRecover = 0.01d;
        public double OwnerDamageAccumulator;
        public bool Initialized;
        public bool SupportSkillsApplied;
        // Support has its own CD/count clock; it never occupies the owner's skill slots.
        public List<FightBattleSkillDefinition> SupportSkills = new List<FightBattleSkillDefinition>();
        public FightSkillAgentState SupportSkillState = new FightSkillAgentState();
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
    public sealed class FightBattleStatusPropTarget
    {
        public bool IsEnemy;
        public int[] RoleTypes;
        public bool IncludeApplicator;
        public int PropertyId;
        public bool Highest;
        public bool SupportCustomFilter;

        public FightBattleStatusPropTarget Copy()
        {
            var result = (FightBattleStatusPropTarget)MemberwiseClone();
            result.RoleTypes = RoleTypes == null ? null : (int[])RoleTypes.Clone();
            return result;
        }
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
        public int SourceSkillItemId;
        public int SourceSkillRank;
        public int SourceSkillLevel;
        public int SourceSubRank;
        public int ApplyTarget;
        public FightBattleStatusPropTarget PropTarget;
        public int[] TargetWithoutStatusIds;
        public List<FightStatusReplacementValue> StatusReplacementProperties;
        public List<FightStatusPropertyScale> BlessPropertyScales;

        public FightBattleStatusOrigin Origin;

        public FightBattleStatusStateTrigger Copy()
        {
            var result = (FightBattleStatusStateTrigger)MemberwiseClone();
            result.Origin = Origin?.Copy();
            result.PropTarget = PropTarget?.Copy();
            result.TargetWithoutStatusIds = TargetWithoutStatusIds == null ? null : (int[])TargetWithoutStatusIds.Clone();
            result.StatusReplacementProperties = FightStatusPropertyModifiers.Copy(StatusReplacementProperties);
            result.BlessPropertyScales = FightStatusPropertyModifiers.Copy(BlessPropertyScales);
            return result;
        }

        public FightBattleStatusStateTrigger WithSource(FightBattleStatusState status)
        {
            var result = (FightBattleStatusStateTrigger)MemberwiseClone();
            result.Origin = FightBattleStatusOrigin.Resolve(status).Copy();
            result.SourceSkillItemId = FightBattleStatusReportSource.Resolve(status).SkillItemId;
            result.SourceSkillRank = status.SourceSkillRank;
            result.SourceSkillLevel = status.SourceSkillLevel;
            result.SourceSubRank = status.SourceSubRank;
            result.ApplyTarget = ApplyTarget;
            result.PropTarget = PropTarget?.Copy();
            result.TargetWithoutStatusIds = TargetWithoutStatusIds == null
                ? null : (int[])TargetWithoutStatusIds.Clone();
            result.StatusReplacementProperties = FightStatusPropertyModifiers.Copy(StatusReplacementProperties);
            result.BlessPropertyScales = FightStatusPropertyModifiers.Copy(status.BlessPropertyScales);
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusSkillInvocation
    {
        public FightBattleStatusStateTrigger Trigger;
        public int SourceUnitId;
        public int TargetUnitId;
        public int TargetPositionUnitId;
        public float Delay;
        public bool AllowDeadSource;
        public bool AllowDeadTarget;
        public bool Sequential;
        public int SourceRootSkillId;
        // Legacy threshold SkillId/SkillList without explicit source/target uses the skill AI.
        public bool SelectTargets;
        public bool UseTargetPositionWithSelection;
        // Capture at trigger time; later stacking must not change an already collected cast.
        public double DamageFactor = 1.0;
        public bool HasCustomCure;
        public long CustomCure;
        // Runtime-only hit creation snapshot; ordinary status invocations use
        // their existing live unit position references.
        internal FightGridPoint? HitOrigin;
        internal int HitFacing;
        internal bool HitFlipX;
    }

    [Serializable]
    public sealed class FightBattleStatusOrigin
    {
        public const int Unit = 0, Equipment = 1, Suit = 2;
        public bool IsSet;
        public int UnitId;
        public int EntityKind;
        public long EntityId;
        public int SkillItemId;
        public int SkillEntityId;

        public FightBattleStatusOrigin Copy() => (FightBattleStatusOrigin)MemberwiseClone();

        public static FightBattleStatusOrigin Resolve(FightBattleStatusState status)
        {
            return status.Origin != null && status.Origin.IsSet ? status.Origin : new FightBattleStatusOrigin
            {
                IsSet = true, UnitId = status.CreatorId, EntityId = status.CreatorId,
                SkillItemId = status.ApplyingSkillItemId, SkillEntityId = status.RootSkillId
            };
        }

        public static FightBattleStatusOrigin Resolve(FightBattleSkillDefinition skill, int casterId)
        {
            return skill?.StatusOrigin != null && skill.StatusOrigin.IsSet ? skill.StatusOrigin : new FightBattleStatusOrigin
            {
                IsSet = true, UnitId = casterId, EntityId = casterId,
                SkillItemId = skill?.UseItemId > 0 ? skill.UseItemId : skill?.ItemId ?? 0,
                SkillEntityId = skill?.RootEntityId > 0 ? skill.RootEntityId : skill?.EntityId ?? 0
            };
        }

        public static bool Matches(FightBattleStatusOrigin left, FightBattleStatusOrigin right)
        {
            if (left == null || right == null || !left.IsSet || !right.IsSet
                || left.UnitId <= 0 || left.UnitId != right.UnitId
                || left.EntityKind != right.EntityKind || left.EntityId <= 0 || left.EntityId != right.EntityId)
                return false;
            if (left.SkillItemId > 0 && right.SkillItemId > 0) return left.SkillItemId == right.SkillItemId;
            return left.SkillEntityId > 0 && left.SkillEntityId == right.SkillEntityId;
        }
    }

    // The original status component graph is data driven. These compact runtime records keep
    // component parameters with the status instance so checkpoints and refreshed applications
    // retain the same trigger counters and nested skill/status configurations.
    [Serializable]
    public sealed class FightBattleStatusComponentState
    {
        public long[] GameplayTags;
        public long LifeReductionIdentity;
        public FightBattleStatusLifeReductionState LifeReduction;
        public FightBattleStatusHitScopeState HitScope;
        public FightBattleStatusDarkHitState DarkHit;
        public FightBattleStatusHitCustomCureState HitCustomCure;
        public FightBattleStatusMoveScopeState MoveScope;
        public FightBattleStatusSecondsState Seconds;
        public FightBattleStatusCureReduceState CureReduce;
        public FightBattleStatusHitSkillComponentState HitSkill;
        public FightBattleStatusDamageSkillComponentState DamageSkill;
        public FightBattleStatusHitApplyDamageComponentState HitApplyDamage;
        public FightBattleStatusHitDmgAddPerComponentState HitDmgAddPer;
        public FightBattleStatusDamageGetTypeComponentState DamageGetType;
        public FightBattleStatusDamageReduceAbsoluteComponentState DamageReduceAbsolute;
        public FightBattleStatusHpUnitComponentState HpIncreaseUnit;
        public FightBattleStatusRoundIntervalComponentState RoundInterval;
        public FightBattleStatusStartComponentState Start;
        public FightBattleStatusStackChangeComponentState StackChange;
        public FightBattleStatusHitSummonComponentState HitSummon;

        public FightBattleStatusComponentState Copy()
        {
            return new FightBattleStatusComponentState
            {
                GameplayTags = GameplayTags == null ? null : (long[])GameplayTags.Clone(),
                LifeReductionIdentity = LifeReductionIdentity,
                LifeReduction = LifeReduction?.Copy(),
                HitScope = HitScope?.Copy(),
                DarkHit = DarkHit?.Copy(),
                HitCustomCure = HitCustomCure?.Copy(),
                MoveScope = MoveScope?.Copy(),
                Seconds = Seconds?.Copy(),
                CureReduce = CureReduce?.Copy(),
                HitSkill = HitSkill?.Copy(),
                DamageSkill = DamageSkill?.Copy(),
                HitApplyDamage = HitApplyDamage?.Copy(),
                HitDmgAddPer = HitDmgAddPer?.Copy(),
                DamageGetType = DamageGetType?.Copy(),
                DamageReduceAbsolute = DamageReduceAbsolute?.Copy(),
                HpIncreaseUnit = HpIncreaseUnit?.Copy(),
                RoundInterval = RoundInterval?.Copy(),
                Start = Start?.Copy(),
                StackChange = StackChange?.Copy(),
                HitSummon = HitSummon?.Copy()
            };
        }
    }

    [Serializable]
    public sealed class FightBattleStatusHitSkillComponentState
    {
        public int HitSourceType;
        public float Rate;
        public bool Block;
        public bool Crit;
        public bool Damage = true;
        public bool Cure;
        public bool IsTrueDamage;
        public bool IsApplyOneHit;
        public List<int> HitSkillTypes = new List<int>();
        public List<int> TargetActionTypes = new List<int>();
        public List<int> HitSkillElementTypes = new List<int>();
        public List<int> HitSkillEntityClassIds = new List<int>();
        public int ConditionCount = 1;
        public int MaxInvokeCount = -1;
        public bool AutoRemove;
        public int RoundResetCount = -1;
        public int ConditionProgress;
        public int InvokeCount;
        public int SkillId;
        public bool IsSkillTargetPos = true;
        public int TargetStatusId;
        public List<int> DisperseActionTypes = new List<int>();
        public int StatusId;
        public bool IsOverflow;
        public float StatusCustomParamByOverflow;
        public List<FightBattleStatusStateTrigger> SkillTriggers = new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> StatusTriggers = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusHitSkillComponentState Copy()
        {
            var result = (FightBattleStatusHitSkillComponentState)MemberwiseClone();
            result.HitSkillTypes = new List<int>(HitSkillTypes ?? new List<int>());
            result.TargetActionTypes = new List<int>(TargetActionTypes ?? new List<int>());
            result.HitSkillElementTypes = new List<int>(HitSkillElementTypes ?? new List<int>());
            result.HitSkillEntityClassIds = new List<int>(HitSkillEntityClassIds ?? new List<int>());
            result.DisperseActionTypes = new List<int>(DisperseActionTypes ?? new List<int>());
            result.SkillTriggers = new List<FightBattleStatusStateTrigger>(SkillTriggers ?? new List<FightBattleStatusStateTrigger>());
            result.StatusTriggers = new List<FightBattleStatusStateTrigger>(StatusTriggers ?? new List<FightBattleStatusStateTrigger>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusDamageSkillComponentState
    {
        public float Rate;
        public bool Block;
        public bool Crit;
        public bool Damage = true;
        public bool Cure;
        public int RangeCondition;
        public int SkillTargetType = 1;
        public int ConditionCount = 1;
        public int MaxCount = -1;
        public bool AutoDestroy;
        public float GreaterThanMaxHpPer = -1f;
        public int DamageMove;
        public int SkillId;
        public int SourceStatusId;
        public int StatusId;
        public float SkillCustomDamageByHpChangeValue;
        public int EachRoundMaxCount = -1;
        public int ConditionProgress;
        public int RoundTriggeredCount;
        public bool IsAttackerTarget;
        public List<int> ElementTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> SkillTriggers = new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> StatusTriggers = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusDamageSkillComponentState Copy()
        {
            var result = (FightBattleStatusDamageSkillComponentState)MemberwiseClone();
            result.ElementTypes = new List<int>(ElementTypes ?? new List<int>());
            result.SkillTriggers = new List<FightBattleStatusStateTrigger>(SkillTriggers ?? new List<FightBattleStatusStateTrigger>());
            result.StatusTriggers = new List<FightBattleStatusStateTrigger>(StatusTriggers ?? new List<FightBattleStatusStateTrigger>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleGridItemState
    {
        public int Id;
        public int GridItemClassId;
        public int EntityClassId;
        public int OwnerId;
        public int X;
        public int Y;
        public bool Ended;
        // Grid items have a status/property actor, but no HP-based turn or victory slot.
        public FightBattleUnitState Runtime;
        // JsonUtility may materialize an absent inline class; legacy count-only entries
        // must not acquire a fabricated grid actor on load.
        public bool HasRuntime;
        public int ReferenceUnitId;
        public float CreationDelay;
        [NonSerialized] internal Action<FightBattleGridItemState> ViewRemoved;
    }

    [Serializable]
    public sealed class FightBattleGridSummonCell
    {
        public FightGridPoint ScopePosition;
        public FightGridPoint Position;
        public float Rate = 1f;
        public float Delay;
    }

    [Serializable]
    public sealed class FightBattleEntityCountConfig
    {
        public int EntityClassId;
        public int MaxCount;
        public int ResultOffset;
        public List<FightGridPoint> Range = new List<FightGridPoint>();

        public FightBattleEntityCountConfig Copy()
        {
            var result = (FightBattleEntityCountConfig)MemberwiseClone();
            result.Range = new List<FightGridPoint>(Range);
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusHitApplyDamageComponentState
    {
        public bool DefinitionResolved;
        public bool HasUnsupportedFilter;
        public bool HasEntityCountScale;
        public FightBattleEntityCountConfig EntityCountScale;
        public int DamageType;
        public bool IsCheckTargetShield;
        public int CharacterType;
        public List<int> TargetSkillEntityClassIds = new List<int>();
        public int ReducePercentPropertyId;
        public int AddPercentPropertyId;
        public int ScaleByHpType;
        public float HpDecreaseUnit;
        public float MaxHpScale;
        public bool ApplyTargetCharacterTypeIfSubstitute = true;
        public int ConsumeStatusId;
        public int ConsumeStackCount;
        public int ConsumeStackMinCount;
        public bool ConsumeScaleByStatusTypeCount;
        public bool RemoveFollowRootParent;
        public List<int> ConsumeTriggerSkillIds = new List<int>();
        public List<FightGameplayTagClause> SourceSkillTagQuery = new List<FightGameplayTagClause>();

        public FightBattleStatusHitApplyDamageComponentState Copy()
        {
            var result = (FightBattleStatusHitApplyDamageComponentState)MemberwiseClone();
            result.TargetSkillEntityClassIds = new List<int>(TargetSkillEntityClassIds ?? new List<int>());
            result.ConsumeTriggerSkillIds = new List<int>(ConsumeTriggerSkillIds ?? new List<int>());
            result.EntityCountScale = EntityCountScale?.Copy();
            result.SourceSkillTagQuery = new List<FightGameplayTagClause>();
            foreach(var clause in SourceSkillTagQuery ?? new List<FightGameplayTagClause>())
                result.SourceSkillTagQuery.Add(new FightGameplayTagClause {Type=clause.Type,Tags=(long[])clause.Tags.Clone()});
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusHitDmgAddPerComponentState
    {
        public bool DefinitionResolved;
        public bool IsApplyOneHit;
        public List<int> HitSkillTypes = new List<int>();
        public List<int> TargetActionTypes = new List<int>();
        public List<int> HitSkillElementTypes = new List<int>();
        public List<int> HitSkillEntityClassIds = new List<int>();
        public FightBattleStatusTypeCountFilter StatusFilter;
        public bool HasUnsupportedStatusLogicTags;
        public bool UpdateDurationSkillCountIfTrigger;
        public List<int> ConsumeTriggerSkillIds = new List<int>();
        public int LastResetRound = -1;
        public int HitSourceType;
        public float Rate = 1f;
        public bool Block;
        public bool Crit;
        public bool Damage = true;
        public bool Cure;
        public bool IsTrueDamage;
        public int ConditionCount = 1;
        public int MaxInvokeCount = -1;
        public bool AutoRemove;
        public int RoundResetCount = -1;
        public int ConditionProgress;
        public int InvokeCount;
        public int StatusTarget;
        public List<int> StatusTypes = new List<int>();
        public float TargetHpPercent = -1f;
        public int TargetHpComparison;
        public int DurationSkillCount = -1;
        public List<int> OnlySkillActions = new List<int>();
        public int OnlySkillType;
        public bool ScalePropByStatusTypeCount;
        public int ConsumeStatusId;
        public int ConsumeStackCount;
        public int ConsumeStackMinCount;
        public bool ConsumeScaleByStatusTypeCount;

        public FightBattleStatusHitDmgAddPerComponentState Copy()
        {
            var result = (FightBattleStatusHitDmgAddPerComponentState)MemberwiseClone();
            result.HitSkillTypes = new List<int>(HitSkillTypes ?? new List<int>());
            result.TargetActionTypes = new List<int>(TargetActionTypes ?? new List<int>());
            result.HitSkillElementTypes = new List<int>(HitSkillElementTypes ?? new List<int>());
            result.HitSkillEntityClassIds = new List<int>(HitSkillEntityClassIds ?? new List<int>());
            result.StatusFilter = StatusFilter?.Clone();
            result.ConsumeTriggerSkillIds = new List<int>(ConsumeTriggerSkillIds ?? new List<int>());
            result.StatusTypes = new List<int>(StatusTypes ?? new List<int>());
            result.OnlySkillActions = new List<int>(OnlySkillActions ?? new List<int>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusDamageGetTypeComponentState
    {
        public float Rate = 1f;
        public List<FightBattleStatusDamageElementRule> Elements = new List<FightBattleStatusDamageElementRule>();
        public int DamageType;
        public bool Value;
        public bool IsCheckSkillTrigger;
        public int MaxCount = -1;
        public int TriggeredCount;
        public List<int> FilterSkillIds = new List<int>();

        public FightBattleStatusDamageGetTypeComponentState Copy()
        {
            var result = (FightBattleStatusDamageGetTypeComponentState)MemberwiseClone();
            result.Elements = new List<FightBattleStatusDamageElementRule>();
            if (Elements != null)
                foreach (FightBattleStatusDamageElementRule entry in Elements)
                    result.Elements.Add(entry?.Copy());
            result.FilterSkillIds = new List<int>(FilterSkillIds ?? new List<int>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusDamageElementRule
    {
        public int ElementType;
        public int DamageType;
        public bool Value;
        public FightBattleStatusDamageElementRule Copy() => (FightBattleStatusDamageElementRule)MemberwiseClone();
    }

    [Serializable]
    public sealed class FightBattleStatusDamageReduceAbsoluteComponentState
    {
        public float Rate = 1f;
        public float DamageGreaterMaxHpPer = -1f;
        public int MaxCount = -1;
        public bool AutoRemove;
        public bool IsCheckSkillTrigger;
        public int TriggeredCount;
        public FightBattleStatusDamageReduceAbsoluteComponentState Copy() =>
            (FightBattleStatusDamageReduceAbsoluteComponentState)MemberwiseClone();
    }

    [Serializable]
    public sealed class FightBattleStatusHpUnitComponentState
    {
        public int TargetType;
        public float UnitHpPer = -1f;
        public int MaxCount = -1;
        public int TriggeredCount;
        // User-confirmed offline rule: actual restored HP is accumulated, including fractions
        // left by percentage thresholds. Initial HP and overheal never enter this balance.
        public double AccumulatedHp;
        public bool TryAtStart;
        public bool TriggerWhenHpLessThan0;
        public int InvokeMaxCountOnHpChange = -1;
        public List<FightBattleStatusStateTrigger> Statuses = new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> Skills = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusHpUnitComponentState Copy()
        {
            var result = (FightBattleStatusHpUnitComponentState)MemberwiseClone();
            result.Statuses = new List<FightBattleStatusStateTrigger>(Statuses ?? new List<FightBattleStatusStateTrigger>());
            result.Skills = new List<FightBattleStatusStateTrigger>(Skills ?? new List<FightBattleStatusStateTrigger>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusRoundIntervalComponentState
    {
        public bool DefinitionResolved;
        public float Rate = 1f;
        public float RoundIntervalPercent;
        public bool FastForward;
        public int TriggeredCount;
        public int LastTriggeredRound = -1;
        public bool HasUnsupportedFilter;
        public List<int> ExcludedApplyCharacterTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> SkillTriggers = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusRoundIntervalComponentState Copy()
        {
            var result = (FightBattleStatusRoundIntervalComponentState)MemberwiseClone();
            result.ExcludedApplyCharacterTypes = new List<int>(ExcludedApplyCharacterTypes ?? new List<int>());
            result.SkillTriggers = new List<FightBattleStatusStateTrigger>(SkillTriggers ?? new List<FightBattleStatusStateTrigger>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusStartComponentState
    {
        public List<FightBattleStatusStateTrigger> Statuses = new List<FightBattleStatusStateTrigger>();
        public int ForbiddenTarget;
        public int ForbiddenStatusId;
        public bool Forbidden;

        public FightBattleStatusStartComponentState Copy()
        {
            var result = (FightBattleStatusStartComponentState)MemberwiseClone();
            result.Statuses = new List<FightBattleStatusStateTrigger>(Statuses ?? new List<FightBattleStatusStateTrigger>());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusStackChangeComponentState
    {
        public List<FightBattleStatusStackChangeRule> Changes = new List<FightBattleStatusStackChangeRule>();
        public int MaxCount = -1;
        public int TriggeredCount;

        public FightBattleStatusStackChangeComponentState Copy()
        {
            var result = (FightBattleStatusStackChangeComponentState)MemberwiseClone();
            result.Changes = new List<FightBattleStatusStackChangeRule>();
            if (Changes != null)
                foreach (FightBattleStatusStackChangeRule change in Changes)
                    result.Changes.Add(change?.Copy());
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusStackChangeRule
    {
        public int StatusId;
        public int ChangeValue;
        public FightBattleStatusStackChangeRule Copy() => (FightBattleStatusStackChangeRule)MemberwiseClone();
    }

    [Serializable]
    public sealed class FightBattleStatusHitSummonComponentState
    {
        public float Rate = 1f;
        public int SummonStatusId;
        public int SkillId;
        public bool Create;
        public bool Remove;
        public int MaxCount = -1;
        public bool CheckSummonLifeDuration;
        public bool FilterSubstitute;
        public bool HasUnsupportedFilter;
        public bool DefinitionResolved;
        public bool HasSummonEntityTagFilter;
        public List<FightGameplayTagClause> SummonEntityTagQuery = new List<FightGameplayTagClause>();
        public int TriggeredCount;

        public FightBattleStatusHitSummonComponentState Copy()
        {
            var result = (FightBattleStatusHitSummonComponentState)MemberwiseClone();
            result.SummonEntityTagQuery = new List<FightGameplayTagClause>();
            if (SummonEntityTagQuery != null)
                foreach (var clause in SummonEntityTagQuery)
                    result.SummonEntityTagQuery.Add(new FightGameplayTagClause
                        { Type = clause.Type, Tags = (long[])clause.Tags.Clone() });
            return result;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusReportSource
    {
        // Unity may deserialize a null inline class as a default object; that is not an ancestry record.
        public bool IsSet;
        public int UnitId;
        public int SkillEntityId;
        public int SkillItemId;

        public FightBattleStatusReportSource Copy()
        {
            return new FightBattleStatusReportSource
            {
                IsSet = IsSet, UnitId = UnitId, SkillEntityId = SkillEntityId, SkillItemId = SkillItemId
            };
        }

        public static FightBattleStatusReportSource Resolve(FightBattleStatusState status)
        {
            // Missing legacy metadata can use only the direct source already stored in that save.
            return status.ReportSource != null && status.ReportSource.IsSet
                ? status.ReportSource : new FightBattleStatusReportSource
            {
                IsSet = true, UnitId = status.CreatorId, SkillEntityId = status.RootSkillId,
                SkillItemId = status.ApplyingSkillItemId
            };
        }
    }

    [Serializable]
    public sealed class FightBattleStatusState
    {
        public FightBattleStatusOrigin Origin;
        public FightBattleStatusComponentState Components = new FightBattleStatusComponentState();
        public bool RemoveApplyEntityOnEnd;
        public int InstanceId;
        public int ClassId;
        public int StatusType;
        public int ActionType;
        public bool HasBackToLifeComponent;
        public FightBattleStatusReplayState SkillReplay;
        // Official FightStatusComponentInfo base fields. DurationRound < 0 means infinite.
        public int DurationRound;
        public int CurDurationRound;
        public int StartRound;
        // Initial assembly may merge duration before its configured round clock exists.
        public bool DurationPreparedBeforeStart;
        public int RoundTarget;
        public int RoundUpdateTiming;
        public bool RemoveAtRoundTargetDestroy;
        public bool RemoveAtRoundTargetDie;
        public bool RemoveAtApplyEnded;
        public int ActionBehOfSameStatus;
        public bool CanDispersed;
        public bool IsAddDurationRound;
        public int MaxAddDurationRound = 100;
        public bool IsResetDurationWhenStack;
        public bool ScalePropWhenChangeStacked;
        public bool ApplyIgnoreResilience;
        public bool ForbiddenOnApplicatorDie;
        public int ForbiddenType;
        public bool AiMoreHit;
        public bool HasCreatorDamageBoost;
        public FightBattleStatusDamageProcessInfo DamageProcess;
        public bool FollowRoundUpdateTimingOnGlobalRound;
        public bool IsFixedProp;
        public int SharedStackId;
        public int SharedStackLimit;
        public int StackCount = 1;
        public int AddStackedCount = 1;
        public int AddStackedRandomMinCount = 1;
        public int AddStackedRandomMaxCount = 1;
        public int MaxStackedCount;
        public bool HasStackCountTrigger;
        public bool StackCountRulesSupported;
        public bool StackCountSkillsResolved;
        public bool StackCountStartChecked;
        public int StackCountThreshold;
        public int StackCountMaxTriggers = -1;
        public int StackCountTriggered;
        public bool StackCountRemoveOnMax;
        public List<FightBattleStatusStateTrigger> StackCountStatuses = new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> StackCountSkills = new List<FightBattleStatusStateTrigger>();
        [NonSerialized] internal Action<FightBattleStatusState> StackChanged;
        public bool IsOpenStack;
        public int OwnerId;
        public int CreatorId;
        public int RootSkillId;
        public int ApplyingSkillItemId;
        // Statistical ancestry is independent of gameplay creator, property snapshots and falloff.
        public FightBattleStatusReportSource ReportSource;
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
        public bool HpStartChecked;
        public List<int> HpTriggerStatuses = new List<int>();
        public List<int> HpTriggerSkills = new List<int>();
        public bool HasHpLessThan;
        public long HpLessThan = -1;
        public float HpLessThanPercentage = -1f;
        public bool HpLessThanInvokeOnDie;
        public bool HpLessThanTryAtStart;
        public int HpLessThanMaxCount = -1;
        public int HpLessThanTriggeredCount;
        public bool HpLessThanAutoDestroy;
        public bool HpLessThanCheckExistForbidden;
        public bool HpLessThanNotMeetRemoveStatus;
        public int HpLessThanTargetType;
        public bool HpLessThanTryAfterInvoke;
        public bool HpLessThanChangeStackResolved;
        public float HpLessThanChangeStackRate;
        public int HpLessThanChangeStackUnit;
        [NonSerialized] internal bool HpLessThanEvaluating;
        public bool HpLessThanReportSkillTriggerCount;
        public List<int> HpLessThanCharacterTypes = new List<int>();
        public List<int> HpLessThanDeactivatePassiveSkills = new List<int>();
        public List<int> HpLessThanActivatePassiveSkills = new List<int>();
        public List<FightBattleStatusStateTrigger> HpLessThanStatuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> HpLessThanSkills =
            new List<FightBattleStatusStateTrigger>();
        public List<FightStatusReplacementValue> HpLessThanReplacementProperties;
        public bool HasHpLimit;
        public int HpLimitLowerLimit;
        public int HpLimitMaxCount = -1;
        public int HpLimitTriggeredCount;
        public bool HpLimitAutoRemove;
        public bool HpLimitAutoRemoveImmediately;
        public int HpLimitExecuteOrder;
        public int HpLimitListenerType;
        public bool HpLimitPassTriggerTarget;
        public List<FightBattleStatusStateTrigger> HpLimitStatuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> HpLimitSkills =
            new List<FightBattleStatusStateTrigger>();
        public List<FightStatusReplacementValue> HpLimitReplacementProperties;
        public List<FightBattleStatusInvoke> HpLimitInvokes =
            new List<FightBattleStatusInvoke>();
        public bool HasRoundStartTrigger;
        public bool RoundStartRulesSupported;
        public int RoundStartListenTarget;
        public int RoundStartDelayRound;
        public int RoundStartDelayRoundType;
        public int RoundStartDelayStartRound = -1;
        public bool RoundStartDelayRulesResolved;
        public bool RoundStartFilterRulesResolved;
        public bool RoundStartForbiddenRulesResolved;
        public bool RoundStartTryAtNotForbidden;
        public int RoundStartNotForbiddenListenerId;
        public int RoundStartNotForbiddenRound = -1;
        public bool RoundStartRequiresFriendWithoutStatuses;
        public bool RoundStartFriendIncludeApplicator;
        public List<int> RoundStartFriendExcludedStatuses = new List<int>();
        public float RoundStartRate = 1f;
        public int RoundStartMaxCount = -1;
        public bool RoundStartMergeMaxCountOnStackRound;
        public int RoundStartStackRoundCount = 1;
        public bool RoundStartMergeRulesResolved;
        public int RoundStartMeetCount = 1;
        public int RoundStartMeetProgress;
        public bool RoundStartResetMeetCountOnInvoke;
        public bool RoundStartMeetRulesResolved;
        public bool RoundStartRandomOneStatus;
        public bool RoundStartRandomRulesResolved;
        public bool RoundStartSourceRulesResolved;
        public int RoundStartTriggeredCount;
        public bool RoundStartTryAtStart;
        public bool RoundStartStartChecked;
        public bool RoundStartDefinitionResolved;
        public bool RoundStartTargetRulesResolved;
        public bool RoundStartPropTargetRulesResolved;
        public int RoundStartPhase;
        public List<int> RoundStartExcludedTargetStatuses = new List<int>();
        public bool RoundStartRemoveWhenNotEnoughTimes;
        public List<FightBattleStatusStateTrigger> RoundStartSkills =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> RoundStartStatuses =
            new List<FightBattleStatusStateTrigger>();
        public bool HasRoundEndTrigger;
        public bool RoundEndRulesSupported;
        public int RoundEndListenTarget;
        public int RoundEndMaxCount = -1;
        public int RoundEndTriggeredCount;
        public bool RoundEndAutoRemove;
        public FightBattleEntityCountConfig RoundEndEntityCount;
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
        // Confirmed legacy StatusEntClassId binding uses the responding passive's cultivation.
        public int SkillEndLegacyStatusId;
        public int SkillEndMetCount = 1;
        public int SkillEndMetCountProgress;
        public int SkillEndTriggeredCount;
        public List<int> SkillEndFilteredActionTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> SkillEndStatuses =
            new List<FightBattleStatusStateTrigger>();
        public bool HasActionEndTrigger;
        public bool ActionEndRulesSupported;
        public bool ActionEndTargetTypeAllEnd;
        public float ActionEndRate = 1f;
        public int ActionEndTriggeredCount;
        public List<int> ActionEndActionTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> ActionEndSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasKillSkill;
        public bool KillSkillRulesSupported;
        public float KillSkillRate;
        public int KillSkillEntityId;
        public int KillSkillStatusEntityId;
        public bool KillSkillTargetDiePos;
        public bool KillSkillCustomOverflowDamage;
        public bool KillSkillUseCustomDamage;
        public int KillSkillMaxCount = -1;
        public bool KillSkillResetCountEveryRound;
        public int KillSkillTriggeredCount;
        public bool KillSkillScaleDamageByCharacterTypeOnPvp = true;
        public bool KillSkillFilterSelf;
        public List<int> KillSkillFilterClassIds = new List<int>();
        public bool HasMoveNear;
        public bool MoveNearRulesSupported;
        public float MoveNearRate;
        public int MoveNearSkillId;
        public List<FightGridPoint> MoveNearRange = new List<FightGridPoint>();
        public bool MoveNearTryAtRound;
        public bool MoveNearTryAtMoved;
        public bool MoveNearTryAtEnterRange;
        public bool MoveNearTryAtStandRound;
        public bool MoveNearTryAtStart;
        public bool MoveNearTryAtInRangeMove;
        public int MoveNearMaxCount = -1;
        public int MoveNearTargetType;
        public int MoveNearMaxCountEachRound = -1;
        public int MoveNearTriggeredCount;
        public int MoveNearRoundTriggeredCount;
        public bool MoveNearStartTriggered;
        public List<int> MoveNearCharacterTypes = new List<int>();
        public float MoveNearSkillTriggerDelayTime;
        public bool MoveNearNoTriggerIfRoleDie;
        public bool MoveNearOnlyFirstInPosition = true;
        public int MoveNearEnterRangeCauseFilter;
        public bool MoveNearSkillTriggerImmediately;
        public List<FightBattleStatusStateTrigger> MoveNearSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasMoveRange;
        public bool MoveRangeRulesSupported;
        public List<FightGridPoint> MoveRangeOffsets = new List<FightGridPoint>();
        public int MoveRangeTargetType;
        public bool MoveRangeExcludeDead;
        public List<int> MoveRangeCharacterTypes = new List<int>();
        public bool MoveRangeFindAllInCell;
        public List<int> MoveRangeFilterClassIds = new List<int>();
        public bool MoveRangeUnlimited;
        public int MoveRangeHandleType;
        public bool MoveRangeCheckEveryRound;
        public int MoveRangeTargetCount;
        public int MoveRangeMaxStack = -1;
        public List<FightBattleStatusStateTrigger> MoveRangeCloseStatuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> MoveRangeMeetStatuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> MoveRangeNotMeetStatuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> MoveRangeCountStatuses =
            new List<FightBattleStatusStateTrigger>();
        public int MoveRangeLastTargetCount = -1;
        public HashSet<int> MoveRangeInsideUnitIds = new HashSet<int>();
        public bool HasRoundCheckSkill;
        public bool RoundCheckSkillRulesSupported;
        public int RoundCheckSkillListenTarget;
        public List<FightBattleStatusStateTrigger> RoundCheckSkillSkills =
            new List<FightBattleStatusStateTrigger>();
        public bool HasActionStartTrigger;
        public bool ActionStartRulesSupported;
        public float ActionStartRate = 1f;
        public List<int> ActionStartActionTypes = new List<int>();
        public List<FightBattleStatusStateTrigger> ActionStartSkills =
            new List<FightBattleStatusStateTrigger>();
        public int ActionStartTriggeredCount;
        public bool HasRoleSummonCount;
        public bool RoleSummonCountRulesSupported;
        public bool RoleSummonCountUpdateStack;
        public int RoleSummonCountLimit;
        public FightBattleStatusStateTrigger RoleSummonCountStatus;
        public int RoleSummonCountLastCount = -1;
        public bool HasRoleDieTrigger;
        public bool RoleDieRulesSupported;
        public List<int> RoleDieCharacterTypes = new List<int>();
        public bool RoleDieTryAtRemove;
        public bool RoleDieTryHighestCombatRatingAtPlay;
        public bool RoleDieSameFaction = true;
        public int RoleDieMasterType;
        public bool RoleDieUniqueRole;
        public int RoleDieMaxCount = -1;
        public int RoleDieMeetCount = 1;
        public int RoleDieMeetProgress;
        public bool RoleDieResetCountEveryRound;
        public int RoleDieTriggeredCount;
        public HashSet<int> RoleDieSeenUnitIds = new HashSet<int>();
        public List<FightBattleStatusStateTrigger> RoleDieSkills =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> RoleDieStatuses =
            new List<FightBattleStatusStateTrigger>();
        // Official FightStatusConsumeStackSkillDmgScaleComponent. Matching skills consume the
        // configured status stacks and scale the skill's damage by ScaleValuePerStack.
        public bool HasConsumeStackSkillDamageScale;
        public bool ConsumeStackSkillDamageScaleRulesSupported;
        public long ConsumeStackSkillDamageScaleValuePerStack;
        public int ConsumeStackSkillDamageStatusId;
        public int ConsumeStackSkillDamageMinStackCount = 1;
        public int ConsumeStackSkillDamageConsumeCount = -1;
        public bool ConsumeStackSkillDamageScaleByStatusTypeCount;
        public bool ConsumeStackSkillDamageRemoveOnSkillEnd;
        public List<int> ConsumeStackSkillDamageTriggerSkillIds = new List<int>();
        public int ConsumeStackSkillDamageConsumedCount;
        public bool ConsumeStackSkillDamageTriggered;
        // Official FightStatusActionExistComponent. The generated status is tracked by source
        // instance so an independently applied status with the same ClassId is preserved.
        public bool HasActionExist;
        public bool ActionExistRulesSupported;
        public int ActionExistStatusId;
        public List<int> ActionExistConditions = new List<int>();
        public int ActionExistAppliedStatusInstanceId;
        public bool IsActionExistGenerated;
        public int ActionExistSourceInstanceId;
        // Official FightStatusConsumeStackCountComponent.
        public bool HasConsumeStackCount;
        public bool ConsumeStackCountRulesSupported;
        public int ConsumeStackCountTargetStatusId;
        public int ConsumeStackCountThreshold;
        public int ConsumeStackCountProgress;
        public List<FightBattleStatusStateTrigger> ConsumeStackCountStatuses =
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
        public List<FightStatusReplacementValue> ReplacementProperties;
        public List<FightStatusPropertyScale> BlessPropertyScales;
        public List<FightPropertyValue> StatusConfigProperties;
        public List<FightPropertyValue> StatusProperties;
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
        // Original FightStatusLogicComponent data is retained per status instance.  The APK
        // exposes only an empty client shell, so the offline executor must keep the exact
        // polymorphic graph and its persistent blackboard values in the checkpoint.
        public bool HasLogicComponent;
        public bool LogicDefinitionResolved;
        public bool LogicRulesSupported;
        public string LogicDefinitionJson;
        public bool LogicSelfInitialized;
        public bool LogicStartInitialized;
        public int LogicTriggeredCount;
        public List<FightBattleLogicBlackboardValue> LogicBlackboard =
            new List<FightBattleLogicBlackboardValue>();
    }

    [Serializable]
    public sealed class FightBattleLogicBlackboardValue
    {
        public int KeyId;
        public int ValueType;
        public long LongValue;
        public double DoubleValue;
        public bool BoolValue;
        public int EntityId;

        public FightBattleLogicBlackboardValue Copy() => (FightBattleLogicBlackboardValue)MemberwiseClone();
    }

    [Serializable]
    public sealed class FightBattleStatusInvoke
    {
        public int Type;
        public int Target;
        public int ChangeCooldown;
        public int TargetType;
        public int MaxCount = -1;
        public int[] CharacterEntityClassIds = Array.Empty<int>();
        public int[] CharacterTypes = Array.Empty<int>();
        public List<FightGridPoint> Range = new List<FightGridPoint>();
        public List<FightBattleStatusStateTrigger> Statuses =
            new List<FightBattleStatusStateTrigger>();
        public List<FightBattleStatusStateTrigger> Skills =
            new List<FightBattleStatusStateTrigger>();
    }

    [Serializable]
    public sealed class FightBattleStatusDamageProcessInfo
    {
        public int Target;
        public int ProcessPhase;
        public int DamageType;
        public List<int> SourceActionTypes = new List<int>();
        public int ReducePercentPropertyId;
        public int AddPercentPropertyId;
        public bool TriggerOnlyDamageFromApplicator;
        public bool UseHitEventToApplicator;
        public bool TriggerOnlyDamageFromCreatorAndSummons;
        public bool CheckSourceIsEnemy;
        public bool NoIndependentMultiplier;
        public bool Supported;

        public FightBattleStatusDamageProcessInfo Copy()
        {
            return new FightBattleStatusDamageProcessInfo
            {
                Target = Target,
                ProcessPhase = ProcessPhase,
                DamageType = DamageType,
                SourceActionTypes = SourceActionTypes == null
                    ? new List<int>() : new List<int>(SourceActionTypes),
                ReducePercentPropertyId = ReducePercentPropertyId,
                AddPercentPropertyId = AddPercentPropertyId,
                TriggerOnlyDamageFromApplicator = TriggerOnlyDamageFromApplicator,
                UseHitEventToApplicator = UseHitEventToApplicator,
                TriggerOnlyDamageFromCreatorAndSummons = TriggerOnlyDamageFromCreatorAndSummons,
                CheckSourceIsEnemy = CheckSourceIsEnemy,
                NoIndependentMultiplier = NoIndependentMultiplier,
                Supported = Supported
            };
        }
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
        public FightBattleStatusOrigin StatusOrigin;
        public int Slot;
        public int ItemId;
        public int UseItemId;
        public int SkillSkinId;
        public int EntityId;
        public int Rank;
        public int Level;
        public int SubRank;
        public List<FightStatusPropertyScale> BlessPropertyScales;
        public FightSkillProfessionAvailability ProfessionAvailability;
        public int SkillType;
        public int ActionType;
        public int HitTargetActionType;
        public long[] GameplayTags;
        // Preserve the original polymorphic skill graph separately from execution support.
        // Checkpoints are saved between atomic casts; these fields contain configuration only.
        public bool HasLogicComponent;
        public bool LogicDefinitionResolved;
        public string LogicDefinitionJson;
        public bool SequenceDefinitionResolved;
        public List<int> ComboSkills = new List<int>();
        public List<int> FallbackSkills = new List<int>();
        public List<FightGridPoint> FallbackRange = new List<FightGridPoint>();
        public bool MoveByTeleport;
        public List<int> BlockingSourceActionTypes = new List<int>();
        public List<int> BlockingTargetActionTypes = new List<int>();
        public int SkillTargetType;
        public bool FlipSource = true;
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
        public bool ReplayUsesRate;
        public bool ReplaySelectMatchingSlot;
        public List<int> ReplaySlotElementTypes = new List<int>();
        public double ReplayChance = 1d;
        public int ReplayType = 1;
        public List<int> ReplayReplacementEntities = new List<int>();
        public int ReplayMaxCountEachRound = -1;
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
        public bool AiMustMoveTo;
        public bool AIIgnore;
        public bool CanExpandOffset;
        public List<FightBattleSkillMoveCondition> Conditions = new List<FightBattleSkillMoveCondition>();
    }

    [Serializable]
    public sealed class FightBattleSkillMoveCondition
    {
        public string Type;
        public int Distance;
    }

    [Serializable]
    public sealed class FightBattleHitScopeTransform
    {
        public float RealCenterX, RealCenterY;
    }

    [Serializable]
    public sealed class FightBattleHitDefinition
    {
        public FightBattleStatusApplication AirCombo;
        // A root wrapper owns a real client entity without applying a damage action.
        public bool IsViewOnly;
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
        public FightBattleHitScopeTransform RandomTargetScopeTransform;
        // Official ActionType from FightHitBaseComponentInfo.DamageActionType: 4 Damage, 5 Cure,
        // 0 None. Synthetic definitions default to Damage.
        public int ActionType = 4;
        public float Delay;
        public float DelayDistFactor;
        public int Anchor;
        public bool IgnoreAICount;
        // Original flight offsets are evaluated against the selected hit entity grid,
        // independently of the bodies later damaged by its scope.
        public FightBattleHitTrajectory Trajectory;
        public bool TrueDamage;
        // FightHitBaseComponentInfo: accepts an external damage factor only when enabled.
        public bool UseDamageFactor;
        public bool BreakResilience;
        // Official HitScopCfg.Pos offsets: the damage area around the hit anchor. Empty means the
        // hit only applies to the selected target(s).
        public List<FightGridPoint> Scope = new List<FightGridPoint>();
        // Null preserves explicitly authored world offsets; catalog scopes are local coordinates.
        public FightBattleHitScopeTransform ScopeTransform;
        public List<FightBattleHitScopeEntry> ScopeEntries = new List<FightBattleHitScopeEntry>();
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
        public string SummonConfigType;
        public int SummonReferenceType;
        public int SummonGridItemClassId;
        public bool GridItemFindAdjacentIfBlocked;
        public List<FightBattleGridSummonCell> GridSummonCells = new List<FightBattleGridSummonCell>();
        public int SummonLevelType;
        public int SummonLifespanStatusId;
        public List<FightBattleSummonPool> SummonPools = new List<FightBattleSummonPool>();
        public List<int> SummonStatusIds = new List<int>();
        public List<FightGridPoint> SummonPositions = new List<FightGridPoint>();
        public FightBattleHitTriggerSkillState TriggerSkill;
        public float ExtApplyDamageTargetHpLessThan = -1f;
        public float ExtApplyDamageScale = 1f;
    }

    [Serializable]
    public sealed class FightBattleHitEntity
    {
        public FightBattleStatusApplication AirCombo;
        public int EntityId;
        public string HitComponentType;
        public FightBattleHitTrajectory Trajectory;
        public float DestroyDelay;
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
        // Every HitCfg is an occurrence, even when several edges refer to the same class.
        // Keep the ID lists above for older checkpoint/executor compatibility.
        public List<FightBattleHitLink> ChainedHitLinks = new List<FightBattleHitLink>();
        public List<FightBattleHitLink> CustomTargetHitLinks = new List<FightBattleHitLink>();
        public List<FightBattleHitLink> RandomTargetHitLinks = new List<FightBattleHitLink>();
        public bool IsChained;
        public bool Repeat;
        public bool Back;
        public bool PreferUnvisited;
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
        public bool UseDamageFactor;
        public bool BreakResilience;
        public int ActionType = 4;
        public List<FightGridPoint> Scope = new List<FightGridPoint>();
        // Original HitScopCfg entries, including their independent presentation/timing values.
        public List<FightBattleHitScopeEntry> ScopeEntries = new List<FightBattleHitScopeEntry>();
        public FightBattleHitScopeTransform ScopeTransform;
        public bool AllowEmptyScope;
        public bool IgnoreShield;
        public bool IsSummon;
        public bool SummonIsRandom;
        public bool SummonImmediateRound;
        public bool SummonInheritProps;
        public string SummonConfigType;
        public int SummonReferenceType;
        public int SummonGridItemClassId;
        public bool GridItemFindAdjacentIfBlocked;
        public List<FightBattleGridSummonCell> GridSummonCells = new List<FightBattleGridSummonCell>();
        public int SummonLevelType;
        public int SummonLifespanStatusId;
        public List<FightBattleSummonPool> SummonPools = new List<FightBattleSummonPool>();
        public List<int> SummonStatusIds = new List<int>();
        public List<FightGridPoint> SummonPositions = new List<FightGridPoint>();
        public FightBattleHitTriggerSkillState TriggerSkill;
        public float ExtApplyDamageTargetHpLessThan = -1f;
        public float ExtApplyDamageScale = 1f;
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
    public sealed class FightBattleHitLink
    {
        public int EntityId;
        public float Delay;
        public int Anchor;
        public bool IgnoreAICount;
    }

    [Serializable]
    public sealed class FightBattleHitTrajectory
    {
        public float OffsetX, OffsetY, OffsetZ;
        public float TargetOffsetX, TargetOffsetY, TargetOffsetZ;
        public bool IsUseCenterRotation;
        public float RotateCenterOffsetX, RotateCenterOffsetY, RotateCenterOffsetZ;
    }

    [Serializable]
    public sealed class FightBattleHitScopeEntry
    {
        public FightGridPoint Position;
        public float Delay;
        public float DamageOffsetX, DamageOffsetY, DamageOffsetZ;
        public float DamageRotationX, DamageRotationY, DamageRotationZ;
        public float DamageRotationW = 1f;
        public float DamageRotZOffset;
        public bool FlipBySourceX = true;
        public bool FlipByTargetY = true;
    }

    [Serializable]
    public sealed class FightBattleHitTriggerSkillState
    {
        public List<int> SkillIds = new List<int>();
        public bool UseTargetPosition;
        public bool UseSkillAsNewSkillParent;

        public FightBattleHitTriggerSkillState Copy()
        {
            return new FightBattleHitTriggerSkillState
            {
                SkillIds = new List<int>(SkillIds ?? new List<int>()),
                UseTargetPosition = UseTargetPosition,
                UseSkillAsNewSkillParent = UseSkillAsNewSkillParent
            };
        }
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
        // User-confirmed duel double knockout: discard settlement and resource/attempt charges.
        public bool Invalid;
        public bool CanSettle => Finished && !Invalid;
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
        public bool RoleDiePlayPending;
        public List<FightBattleUnitState> Units = new List<FightBattleUnitState>();
        public List<FightBattleGridItemState> GridItems = new List<FightBattleGridItemState>();
        public List<FightBattleWaveState> Waves = new List<FightBattleWaveState>();
        public FightBattleStageStatusConfig StageStatus;
        public FightBattleGrid Grid;
        public FightTurnQueueState Timeline = new FightTurnQueueState();
        public FightBattleOutcome Outcome = new FightBattleOutcome();
        public FightBattleResultRules ResultRules;
        public FightDamageTestRules DamageTest;
        // Local session persistence; absent fields in older saves retain legacy resume behavior.
        // User-confirmed offline partial settlement; never denotes a victory or a killed enemy.
        public bool PartEnded;
        public FightBattleStatusSecondsClock StatusSeconds;
        public bool PreparationPending;
        public double PreparationSecondsRemaining;
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
        public List<FightBattleGridViewEvent> GridViewEvents = new List<FightBattleGridViewEvent>();
        // Actual StatusEnd/RemoveApplyEntity time, independent of lingering skill effects.
        public List<FightBattleUnitEndEvent> UnitEndEvents = new List<FightBattleUnitEndEvent>();
        public List<FightBattleBodyAnimatorTriggerEvent> BodyAnimatorTriggers =
            new List<FightBattleBodyAnimatorTriggerEvent>();
        public List<FightBattleStatusCustomActionEvent> StatusCustomActions =
            new List<FightBattleStatusCustomActionEvent>();
        public List<int> SpawnedUnitIds = new List<int>();
        // One notification per successful disperse operation; ordinary expiration is not a disperse.
        public List<int> DispersedUnitIds = new List<int>();
    }

    [Serializable]
    public sealed class FightBattleUnitEndEvent
    {
        public int UnitId;
        public int StatusId;
        public float Delay;
    }

    [Serializable]
    public sealed class FightBattleStatusCustomActionEvent
    {
        public int UnitId;
        public int Action;
        // Actual damaging occurrence that produced this action. Its final receipt
        // owns all nested/approach offsets; zero keeps legacy timing unknown.
        public int SourceDamageId;
        // A hit can apply the action to its source instead of its damaged target.
        // Zero retains the original same-target association for legacy receipts.
        public int SourceDamageTargetId;
    }

    [Serializable]
    public sealed class FightBattleMovementEvent
    {
        public int UnitId;
        public int MoveBehaviourId;
        public int MoveSkillEntityId;
        // Ordinary movement owns a separate skill entity before its action starts.
        // Null in legacy receipts and in forced/self skill actions.
        public FightBattleSkillCastPresentation OrdinarySkillPresentation;
        public float OrdinaryMoveDelay;
        public bool IsRetreat;
        // Confirmed offline action direction, captured before path settlement.
        // Missing legacy snapshots remain false and use the existing presentation.
        public bool HasDirection;
        public int Direction;
        public bool HasFacing;
        public bool EndFlipX;
        public float Delay;
        public float MoveDuration = -1f;
        public float MoveSpeed = -1f;
        public List<FightGridPoint> Path = new List<FightGridPoint>();
    }

    [Serializable]
    public sealed class FightBattleSkillCastEvent
    {
        public int ActorId;
        // Captured before settling this cast: later hits/moves must not retarget its view.
        public FightBattleSkillCastPresentation Presentation;
        // Per-cast selection receipts; never re-run target selection when projecting native hit views.
        public List<FightBattleHitSelectionSnapshot> HitSelections = new List<FightBattleHitSelectionSnapshot>();
        // Runtime damage serials created directly by this actual cast. Nested
        // casts own their own list, even when caster and skill class are equal.
        public List<int> DamageEventIds = new List<int>();
        // Original DoSkillCost precedes skill-start notification. These values
        // belong to this actual cast, independently of later same-class replays.
        public bool HasSkillCostHp;
        public long SkillCostBeforeHp;
        public long SkillCostAfterHp;
        // Start time relative to this step; Playable cue times are relative to this cast.
        public float Delay;
        public int Slot;
        public int SkillEntityId;
        public int SkillItemId;
        public bool IsReplay;
        public bool IsPetEnergy;
        public bool IsPetSupport;
        public int ReplayType; // Official EReplaySkillType: Normal=0, Extra=1, ExtraWithTip=2.
        public int ReplayStatusInstanceId; // Zero for a skill's own replay configuration.
        public List<int> TargetIds = new List<int>();
    }

    [Serializable]
    public sealed class FightBattleBodyAnimatorTriggerEvent
    {
        public int UnitId;
        public string Parameter;
        public float Delay;
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
                || double.IsNaN(checkpoint.PreparationSecondsRemaining)
                || double.IsInfinity(checkpoint.PreparationSecondsRemaining)
                || checkpoint.PreparationSecondsRemaining < 0
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
            if (checkpoint.PartEnded && (!checkpoint.Finished || checkpoint.Outcome.Won || checkpoint.Outcome.Invalid))
                throw new ArgumentException("Partial settlement requires a finished, valid non-victory.", nameof(checkpoint));
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
                if (unit != null && unit.PetEnergy == null) unit.PetEnergy = new FightBattlePetEnergyState();
                if (unit?.PetEnergy != null && unit.PetEnergy.SkillProperties == null)
                    unit.PetEnergy.SkillProperties = new List<FightPropertyValue>();
                if (unit?.PetEnergy != null && unit.PetEnergy.EntityProperties == null)
                    unit.PetEnergy.EntityProperties = new List<FightPropertyValue>();
                if (unit?.PetEnergy != null && unit.PetEnergy.EntityActiveSkills == null)
                    unit.PetEnergy.EntityActiveSkills = new List<FightBattleSkillDefinition>();
                if (unit?.PetEnergy != null && unit.PetEnergy.EntityPassiveStatusIds == null)
                    unit.PetEnergy.EntityPassiveStatusIds = new List<int>();
                if (unit?.PetEnergy != null && unit.PetEnergy.SupportSkills == null)
                    unit.PetEnergy.SupportSkills = new List<FightBattleSkillDefinition>();
                if (unit?.PetEnergy != null && unit.PetEnergy.SupportSkillState == null)
                    unit.PetEnergy.SupportSkillState = new FightSkillAgentState();
                if (unit == null || unit.Id <= 0 || !ids.Add(unit.Id) || unit.Hp < 0
                    || unit.MaxHp < 0 || unit.Hp > unit.MaxHp || unit.MaxHpPlies < 0 || unit.MaxResilience < 0
                    || unit.Resilience < 0 || unit.Resilience > unit.MaxResilience
                    || unit.Properties == null || unit.Skills == null || unit.SkillState == null || unit.Turn == null
                    || unit.PetEnergy == null || unit.PetEnergy.SkillProperties == null
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
            checkpoint.ResultRules?.Validate(checkpoint);
            if (checkpoint.Outcome.Invalid && (!checkpoint.Finished || checkpoint.Outcome.Won
                || checkpoint.Outcome.Reason != FightBattleResultRules.InvalidDuelReason))
                throw new ArgumentException("Fight checkpoint contains an inconsistent invalid result.", nameof(checkpoint));
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

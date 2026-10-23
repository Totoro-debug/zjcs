using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleUnitState
    {
        public int Id;
        public int MasterId;
        public int Side;
        public int CharacterType;
        public int MonsterId;
        public int MonsterType;
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
        public long Resilience;
        public long MaxResilience;
        public int BrokenRound = -1;
        public bool Ended;
        public List<FightPropertyValue> Properties = new List<FightPropertyValue>();
        public List<FightBattleSkillDefinition> Skills = new List<FightBattleSkillDefinition>();
        public FightSkillAgentState SkillState = new FightSkillAgentState();
        public FightTurnUnit Turn = new FightTurnUnit();
        public List<FightBattleStatusState> Statuses = new List<FightBattleStatusState>();
        public List<FightBattleFalloffCounter> FalloffCounts = new List<FightBattleFalloffCounter>();
        public List<FightGridPoint> BodyOffsets = new List<FightGridPoint>
        {
            new FightGridPoint(0, 0)
        };
    }

    [Serializable]
    public sealed class FightPropertyValue
    {
        public int Id;
        public long Value;
    }

    [Serializable]
    public sealed class FightBattleStatusState
    {
        public int InstanceId;
        public int ClassId;
        public int StatusType;
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
    }

    [Serializable]
    public sealed class FightBattleFalloffCounter
    {
        public int CastId;
        public int StatusClassId;
        public int Count;
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
        public int SkillType;
        public int SkillTargetType;
        public int ElementType;
        public int RootEntityId;
        public int LimitedTimes = -1;
        public bool ResetCooldownAtStart;
        public bool CanTriggerChild = true;
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
        public int PriorityProperty;
        public bool PriorityFindMax = true;
        public bool RangeVerified = true;
        public bool HasCustomCure;
        public long CustomCure;
        public long SkillCost;
        public List<FightPropertyValue> Properties = new List<FightPropertyValue>();
        public List<FightBattleHitDefinition> Hits = new List<FightBattleHitDefinition>();
        // Official FightSKillMoveCfg entries from FightSkillComponent.SourceMoveList (sorted by
        // Delay). DoMoveConditions entries cannot be evaluated locally and are flagged.
        public List<FightBattleSkillMove> SourceMoves = new List<FightBattleSkillMove>();
    }

    [Serializable]
    public sealed class FightBattleSkillMove
    {
        public int MoveBehaviourId;
        public float Delay;
        public int OffsetX;
        public int OffsetY;
        public bool TowardsTargetPos;
        public int Direction;
        public int Distance = -1;
        public float BasePercent = 1f;
        public bool AffectedByProp;
        public bool CancelDiagonally;
        public bool HasConditions;
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
        // Official ActionType from FightHitBaseComponentInfo.DamageActionType: 4 Damage, 5 Cure,
        // 0 None. Synthetic definitions default to Damage.
        public int ActionType = 4;
        public float Delay;
        public float DelayDistFactor;
        public int Anchor;
        public bool IgnoreAICount;
        // Official HitScopCfg.Pos offsets: the damage area around the hit anchor. Empty means the
        // hit only applies to the selected target(s).
        public List<FightGridPoint> Scope = new List<FightGridPoint>();
        public bool AllowEmptyScope;
        // Official FightDamageComponent.StatusList entries applied when this hit lands.
        public List<FightBattleStatusApplication> AppliedStatuses = new List<FightBattleStatusApplication>();
        public bool StatusListRandomOne;
        // Official FightHitExtHemophagiaComponent.HemophagiaPropType (0 = none).
        public int HemophagiaProperty;
        // Official FightDamageComponent.DisperseStatus (status types, count, chance, random pick).
        public float DisperseChance;
        public List<int> DisperseStatusTypes = new List<int>();
        public int DisperseCount;
        public bool DisperseRandom;
        // Official FightDamageComponent.MoveCfg: displacement applied to the damaged unit.
        public FightBattleSkillMove HitMove;
        public bool HasCustomDamage;
        public long CustomDamage;
        public double DamageRate = 1.0;
        public bool UseDamageRate;
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
        // Reference configuration of the edge that introduced this hit node (HitCfg.Delay and
        // HitCfg.ActOnSouce anchor). The same entity can be referenced from several parents; the
        // first reference wins for the shared node.
        public float Delay;
        public float DelayDistFactor;
        public int Anchor;
        public bool IgnoreAICount;
        public int ActionType = 4;
        public List<FightGridPoint> Scope = new List<FightGridPoint>();
        public bool AllowEmptyScope;
        public List<FightBattleStatusApplication> AppliedStatuses = new List<FightBattleStatusApplication>();
        public bool StatusListRandomOne;
        public int HemophagiaProperty;
        public float DisperseChance;
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
        public float LessThanHpPer = -1f;
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
    }

    [Serializable]
    public sealed class FightBattleMovementEvent
    {
        public int UnitId;
        public List<FightGridPoint> Path = new List<FightGridPoint>();
    }

    [Serializable]
    public sealed class FightBattleSkillCastEvent
    {
        public int ActorId;
        public int Slot;
        public int SkillEntityId;
        public int SkillItemId;
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
                    || unit.MaxHp < 0 || unit.Hp > unit.MaxHp || unit.MaxResilience < 0
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

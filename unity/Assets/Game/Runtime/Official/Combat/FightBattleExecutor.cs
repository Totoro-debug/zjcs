using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed class FightBattleExecutionResult
    {
        public FightBattleCheckpoint Checkpoint;
        public FightBattleStepEvent Event;
        public bool Replayed;
    }

    public sealed partial class FightBattleExecutor
    {
        /// <summary>Official GameMoveTime used by the scene view for one grid cell.</summary>
        public const float MovementSecondsPerCell = 0.5f;
        private const float DefaultStepSeconds = 0.35f;
        private readonly FightBattleCheckpoint checkpoint;
        private readonly FightDamageFormula formula;
        private readonly FightBattleHitExecutor hitExecutor;
        private readonly List<FightBattleUnitState> units = new List<FightBattleUnitState>();
        private readonly Dictionary<int, FightBattleUnitState> unitById = new Dictionary<int, FightBattleUnitState>();
        private readonly FightTurnQueue turns;
        private readonly FightRandomStream random;
        private readonly FightOfficialCombatCatalog statusCatalog;
        private readonly FightOfficialSkillItemCatalog skillItems;
        private List<int> currentStepSpawnedUnitIds;
        private List<FightBattleStatusCustomActionEvent> currentStepStatusCustomActions;
        private List<int> currentStepDispersedUnitIds;
        private int lastGlobalRound;
        private sealed class MovementStartContext
        {
            public List<FightBattleSkillCastEvent> Casts;
            public List<FightBattleDamageEvent> DamageEvents;
            public List<FightBattleMovementEvent> Movements;
            public float StatusStartTime;
            public float Duration;
            public float ActionDuration = -1f;
        }
        private MovementStartContext movementStartContext;
        private FightConditionalHitProperties conditionalHitProperties;
        private bool applyingActionExistStatuses;
        private readonly FightStatusMutexRuntime statusMutex;
        private FightBattleExecutor(FightBattleCheckpoint checkpoint, FightDamageFormula formula,
            FightDamageSettlement settlement, bool initialize,
            FightOfficialCombatCatalog statusCatalog = null,
            FightOfficialSkillItemCatalog skillItems = null)
        {
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
            this.statusCatalog = statusCatalog;
            this.skillItems = skillItems;
            statusMutex = new FightStatusMutexRuntime(statusCatalog?.StatusMutexGroups);
            FightBattleCheckpointRules.Validate(checkpoint, !initialize);
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (checkpoint.Units.Count == 0) throw new ArgumentException("Fight roster is empty.", nameof(checkpoint));

            hitExecutor = new FightBattleHitExecutor(formula, settlement, checkpoint.DamageTest);
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                // Checkpoints are recorded between atomic steps. Legacy saves did not keep
                // the removal receipt, but their already removed units must not emit new casts.
                if (!initialize && unit.MasterId > 0 && (unit.Hp <= 0 || unit.Ended))
                    unit.HitSummonRemoveProcessed = true;
                FightBattlePetEnergyRuntime.Initialize(unit);
                ApplyMonsterInfo(unit);
                InitializeUnitResilience(unit);
                ApplySkillAiPriorities(unit);
                if (statusCatalog != null && unit.Skills != null)
                    foreach (var skill in unit.Skills)
                    {
                        statusCatalog.RestoreHitScopeTransforms(skill);
                        statusCatalog.RestoreSkillLogicDefinition(skill);
                    }
                if (statusCatalog != null && unit.Statuses != null)
                    foreach (FightBattleStatusState status in unit.Statuses)
                        if (status != null && statusCatalog.ContainsEntity(status.ClassId))
                        {
                            if (status.SkillReplay == null) status.SkillReplay = statusCatalog.StatusReplayDefinition(status.ClassId);
                            statusCatalog.RestoreSharedStackDefinition(status);
                            status.AiMoreHit = statusCatalog.StatusMoreHit(status.ClassId);
                            status.HasCreatorDamageBoost = statusCatalog.StatusHasCreatorDamageBoost(status.ClassId);
                            RestoreHpChangeStackDefinition(status);
                            RestoreRoundStartDefinition(status);
                            RestoreConditionalHitDefinition(status);
                            statusCatalog.RestoreCureReduceDefinition(status);
                            statusCatalog.RestoreSkillEndDefinition(status);
                            RestoreRevivalDefinition(status);
                            RestoreHitSummonDefinition(status);
                            RestoreRoundIntervalDefinition(status);
                        }
                units.Add(unit);
                unitById.Add(unit.Id, unit);
                SynchronizeTurn(unit);
            }
            RestoreGridItems();
            if (statusCatalog != null)
                foreach (var item in checkpoint.GridItems)
                    if (item?.Runtime?.Skills != null)
                        foreach (var skill in item.Runtime.Skills)
                        {
                            statusCatalog.RestoreHitScopeTransforms(skill);
                            statusCatalog.RestoreSkillLogicDefinition(skill);
                        }
            if (!unitById.ContainsKey(1))
                throw new ArgumentException("Fight roster does not contain the main player unit.", nameof(checkpoint));
            turns = new FightTurnQueue(checkpoint.Timeline, Turns());
            lastGlobalRound = turns.GlobalRound();
            random = new FightRandomStream(checkpoint.RandomSeed);
            foreach (var unit in units) statusMutex.Synchronize(unit);
            if (initialize) Initialize();
            else if (statusCatalog != null)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            foreach (var unit in units) SynchronizeDeathForbidden(unit);
            if (statusCatalog != null)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            foreach (var unit in units)
                foreach (var status in unit.Statuses) InitializeRoundStartDelay(status);
        }

        public FightBattleCheckpoint State => checkpoint;

        public static FightBattleExecutor Begin(FightBattleCheckpoint checkpoint,
            FightDamageFormula formula, FightDamageSettlement settlement,
            FightOfficialCombatCatalog statusCatalog = null,
            FightOfficialSkillItemCatalog skillItems = null)
        {
            return new FightBattleExecutor(checkpoint, formula, settlement, true, statusCatalog,
                skillItems);
        }

        public static FightBattleExecutor Restore(FightBattleCheckpoint checkpoint,
            FightDamageFormula formula, FightDamageSettlement settlement,
            FightOfficialCombatCatalog statusCatalog = null,
            FightOfficialSkillItemCatalog skillItems = null)
        {
            return new FightBattleExecutor(checkpoint, formula, settlement, false, statusCatalog,
                skillItems);
        }

        private void ApplySkillAiPriorities(FightBattleUnitState unit)
        {
            if (skillItems == null || unit == null || unit.Skills == null) return;
            foreach (FightBattleSkillDefinition skill in unit.Skills)
            {
                if (skill == null || skill.ItemId <= 0
                    || !skillItems.TryGet(skill.ItemId, out FightOfficialSkillItem item)) continue;
                if (item.AiPriorityId > 0) skill.AiPriorityId = item.AiPriorityId;
                if (item.LastAiPriorityId > 0) skill.LastAiPriorityId = item.LastAiPriorityId;
                if (skill.AiPriorityId > 0)
                    skill.AiPriorityTypes = new List<int>(skillItems.PriorityTypes(skill.AiPriorityId));
                if (skill.LastAiPriorityId > 0)
                    skill.LastAiPriorityTypes = new List<int>(skillItems.PriorityTypes(skill.LastAiPriorityId));
            }
        }

        private void ApplyMonsterInfo(FightBattleUnitState unit)
        {
            if (statusCatalog == null || unit == null || unit.MonsterId <= 0) return;
            int aiType = statusCatalog.MonsterAiType(unit.MonsterId);
            if (aiType < 0) return;
            unit.CharacterType = statusCatalog.MonsterCharacterType(unit.MonsterId);
            unit.AiType = aiType;
        }

        private FightBattleExecutionResult StepCore(int sequence, int counter)
        {
            if (sequence != checkpoint.Sequence) throw new InvalidOperationException("Fight sequence does not match.");
            if (counter <= checkpoint.Counter)
            {
                FightBattleStepReceipt receipt = Receipt(sequence, counter);
                if (receipt == null) throw new InvalidOperationException("Fight step counter is stale.");
                return new FightBattleExecutionResult { Checkpoint = checkpoint, Event = receipt.Event, Replayed = true };
            }
            if (counter != checkpoint.Counter + 1) throw new InvalidOperationException("Fight step counter is out of order.");
            if (checkpoint.Finished) throw new InvalidOperationException("Fight is already finished.");
            foreach (var unit in units)
                foreach (var status in unit.Statuses) InitializeRoundStartDelay(status);

            FightBattleExecutionResult startup = ExecutePendingStatusStarts(sequence, counter);
            if (startup != null) return startup;
            if (checkpoint.Finished) return RecordTerminal(sequence, counter);

            RefreshTurnUnits();
            FightTurnUnit turn = turns.Next();
            if (turn == null)
            {
                Finish(3);
                return RecordTerminal(sequence, counter);
            }
            if (turns.GlobalRound() > checkpoint.MaxRound)
            {
                Finish(3);
                return RecordTerminal(sequence, counter);
            }

            FightBattleUnitState actor = unitById[turn.Id];
            int globalRound = turns.GlobalRound();
            turns.State.Round = globalRound;
            var skillState = new FightSkillState(actor.SkillState);
            int actorRound = unchecked(actor.SkillState.Round + 1);
            skillState.BeginRound(actorRound);
            if (actor.PetEnergy != null && actor.PetEnergy.Enabled
                && actor.PetEnergy.OwnerUnitId > 0)
            {
                // Recovery follows the checkpoint's configured energy clock. Existing
                // offline pet assemblies use the owner as their transfer clock.
                if (actor.PetEnergy.PetUnitId == actor.Id
                    || actor.PetEnergy.PetUnitId != actor.PetEnergy.OwnerUnitId)
                    FightBattlePetEnergyRuntime.RecoverAtPetRoundStart(actor);
            }
            if (statusCatalog != null && actor.Statuses != null)
                foreach (FightBattleStatusState status in actor.Statuses)
                    if (status != null && statusCatalog.StatusStopsSkillCooldown(status.ClassId))
                        skillState.StopCooldownForRound();
            var before = new Dictionary<int, long>();
            for (int i = 0; i < units.Count; i++) before[units[i].Id] = units[i].Hp;
            var movements = new List<FightBattleMovementEvent>();
            var casts = new List<FightBattleSkillCastEvent>();
            var damageEvents = new List<FightBattleDamageEvent>();
            movementStartContext = new MovementStartContext { Casts = casts, DamageEvents = damageEvents, Movements = movements };
            roundStartNotForbiddenListener = actor;
            var statusCustomActions = new List<FightBattleStatusCustomActionEvent>();
            currentStepSpawnedUnitIds = new List<int>();
            currentStepStatusCustomActions = statusCustomActions;
            currentStepDispersedUnitIds = new List<int>();
            var performedTargets = new List<FightBattleUnitState>();
            var checkedSlots = new HashSet<int>();
            float stepDuration = 0f;
            float stepActionDuration = -1f;

            CleanupGridItems();
            if (FightStatusLifecycle.TickTurn(actor, StatusOwners(), actorRound, FightStatusLifecycle.TimingStart) > 0)
                RebuildBattleProperties();
            ApplyMissingHitSummonRemovalTriggers();
            if (statusCatalog != null)
                foreach (FightBattleUnitState summonOwner in new List<FightBattleUnitState>(units))
                    FightStatusTriggerRuntime.ApplyRoleSummonCount(summonOwner, units,
                        statusCatalog, random);
            float statusEndDelay = 0f;
            ExecuteAccumulatedDamageEnds(casts, damageEvents, movements,
                ref statusEndDelay, ref stepDuration, ref stepActionDuration);

            if (statusCatalog != null)
            {
                    FightStatusTriggerRuntime.ApplyMoveRange(actor, units, statusCatalog, random, true);
                ExecuteMoveNearTriggers(actor, FightStatusTriggerRuntime.MoveNearEventStart,
                    null, casts, damageEvents, movements, ref stepDuration,
                    ref stepActionDuration);
                ExecuteMoveNearTriggers(actor, FightStatusTriggerRuntime.MoveNearEventRound,
                    null, casts, damageEvents, movements, ref stepDuration,
                    ref stepActionDuration);
                ExecuteMoveNearTriggers(actor, FightStatusTriggerRuntime.MoveNearEventStandRound,
                    null, casts, damageEvents, movements, ref stepDuration,
                    ref stepActionDuration);
                foreach (FightBattleUnitState source in new List<FightBattleUnitState>(units))
                {
                    ExecuteRoundStartTriggers(source, actor);
                    RebuildBattleProperties();
                }
                List<FightBattleStatusSkillInvocation> roundCheckInvocations =
                    FightStatusTriggerRuntime.CollectRoundCheckSkills(actor, units, random);
                float roundCheckDelay = 0f;
                ExecuteConfiguredStatusSkills(roundCheckInvocations, casts, damageEvents,
                    movements, ref roundCheckDelay, out float roundCheckDuration,
                    out float roundCheckActionDuration);
                stepDuration = Math.Max(stepDuration, roundCheckDuration + roundCheckDelay);
                if (roundCheckActionDuration >= 0f)
                    stepActionDuration = Math.Max(stepActionDuration, roundCheckActionDuration);
            }
            ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                ref stepDuration, ref stepActionDuration);
            CollectStatusCustomActions(statusCustomActions);

            // Official RoundPhase.StartEnd follows Start listeners and precedes Round skills.
            if (statusCatalog != null)
                foreach (var source in new List<FightBattleUnitState>(units))
                {
                    ExecuteRoundStartTriggers(source, actor, 5);
                    RebuildBattleProperties();
                }

            ExecutePetSupportSkills(actor, actorRound, globalRound, casts, damageEvents,
                movements, ref stepDuration, ref stepActionDuration, out float petSupportDuration);
            int chaosInvasionPhaseSkillSlot = PrepareChaosInvasionPhaseSkill(actor,
                globalRound, skillState);
            // The official roster contains 18130 from battle start; keep the limited skill
            // unavailable until its user-confirmed round.
            if (checkpoint.StageId == ChaosInvasionStageId
                && checkpoint.StageType == ChaosInvasionStageType
                && (actor.MonsterId == ChaosInvasionBossEntityId
                    || actor.MonsterTemplateId == ChaosInvasionBossEntityId)
                && globalRound < ChaosInvasionPhaseTwoRound && actor.Skills != null)
                foreach (FightBattleSkillDefinition scheduledSkill in actor.Skills)
                    if (scheduledSkill != null
                        && scheduledSkill.EntityId == ChaosInvasionPhaseTwoSkillEntityId)
                        checkedSlots.Add(scheduledSkill.Slot);
            if (chaosInvasionPhaseSkillSlot >= 0 && actor.BrokenRound >= 0
                && actor.Resilience <= 0)
                checkedSlots.Add(chaosInvasionPhaseSkillSlot);
            int ordinaryCastStart = casts.Count;
            int ordinaryDamageStart = damageEvents.Count, ordinaryMovementStart = movements.Count;
            float approachEnd = 0f;
            while (true)
            {
                FightBattleSkillDefinition skill = FightBattleTargeting.FindReadySkill(
                    actor, units, actorRound, excludedSlots: checkedSlots,
                    globalRound: globalRound,
                    configuredCureAvailability: candidate => ConfiguredCureAvailability(actor, candidate));
                if (skill == null) break;

                var excludedForNextSkill = new HashSet<int>(checkedSlots) { skill.Slot };
                skill.UseLastAiPriority = FightBattleTargeting.FindReadySkill(actor, units,
                    actorRound, excludedSlots: excludedForNextSkill,
                    globalRound: globalRound,
                    configuredCureAvailability: candidate => ConfiguredCureAvailability(actor, candidate)) == null;

                List<FightBattleUnitState> targets = FightBattleTargeting.SelectInSkillRange(actor,
                    units, skill, random: random);
                if (skill.Slot == chaosInvasionPhaseSkillSlot)
                    targets = ChaosInvasionPlayerTargets(actor);
                int approachCastStart = casts.Count, approachDamageStart = damageEvents.Count;
                int approachMovementStart = movements.Count;
                if (skill.Slot != chaosInvasionPhaseSkillSlot && targets.Count == 0
                    && MoveIntoSkillRange(actor, skill, movements))
                {
                    // ActionSkillMove runs each path segment before the active skill can start.
                    // Its path here includes the source cell; the original component's does not.
                    OffsetSkillEvents(casts, approachCastStart, damageEvents, approachDamageStart,
                        movements, approachMovementStart, approachEnd);
                    FightBattleMovementEvent approach = movements[approachMovementStart];
                    approachEnd = approach.OrdinarySkillPresentation != null
                        ? Math.Max(0f, approach.Delay - approach.OrdinaryMoveDelay)
                            + approach.OrdinarySkillPresentation.ActionDuration
                        : Math.Max(0f, approach.Delay) + MovementDuration(approach);
                    targets = FightBattleTargeting.SelectInSkillRange(actor, units, skill,
                        random: random);
                }
                if (actor.Hp <= 0) break;
                if (skill.ComboSkills.Count > 0)
                    targets.RemoveAll(target => !ComboTargetAllowed(actor, skill, target));
                if (targets.Count == 0 && skill.TargetType != 5
                    && !FightBattleTargeting.HasCasterAreaTargets(actor, units, skill))
                {
                    checkedSlots.Add(skill.Slot);
                    continue;
                }

                // FightSkillAgentComponentImplement.UpdateSkillDataOnRun records this
                // release before its end callbacks can check LimitedTimes or modify CD.
                FightBattleUnitState comboTargetPosition = null;
                if (skill.ComboSkills.Count > 0 || skill.FallbackSkills.Count > 0)
                {
                    if (targets.Count > 0)
                        TryComboTargetPosition(actor, skill, targets[0], out comboTargetPosition);
                    ApplyComboFacing(actor, skill);
                }
                var preparedCast = SkillCasts.Prepare(actor, skill, targets, comboTargetPosition);
                skill = preparedCast.Skill;
                var cast = preparedCast.Cast;
                int skillCastStart = casts.Count, skillDamageStart = damageEvents.Count;
                int skillMovementStart = movements.Count;
                skillState.RunSkill(skill.Slot);
                if (skill.ComboSkills.Count > 0) casts.Add(cast);
                ExecuteSkill(actor, skill, targets, casts, damageEvents, movements,
                    out float castLastDelay, out float skillStartDuration,
                    out float skillStartActionDuration, targetPosition: comboTargetPosition, presentationCast: cast);
                checkpoint.Statistics.RecordCast(actor.Id, skill.EntityId, skill.ItemId);
                stepDuration = Math.Max(stepDuration,
                    petSupportDuration + approachEnd + Math.Max(ResolveDuration(skill, castLastDelay), skillStartDuration));
                float actionDuration = ResolveActionDuration(skill, castLastDelay);
                if (actionDuration >= 0)
                    stepActionDuration = Math.Max(stepActionDuration, petSupportDuration + approachEnd + actionDuration);
                if (skillStartActionDuration >= 0f)
                    stepActionDuration = Math.Max(stepActionDuration, petSupportDuration + approachEnd + skillStartActionDuration);
                checkedSlots.Add(skill.Slot);
                performedTargets.AddRange(targets);
                CompleteCastPresentation(cast, skill, castLastDelay);
                if (skill.ComboSkills.Count == 0) casts.Add(cast);
                float cooldownDuration = 0f, cooldownActionDuration = -1f;
                ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                    ref cooldownDuration, ref cooldownActionDuration);
                stepDuration = Math.Max(stepDuration, petSupportDuration + approachEnd + cooldownDuration);
                if (cooldownActionDuration >= 0f)
                    stepActionDuration = Math.Max(stepActionDuration,
                        petSupportDuration + approachEnd + cooldownActionDuration);
                OffsetSkillEvents(casts, skillCastStart, damageEvents, skillDamageStart,
                    movements, skillMovementStart, approachEnd);
                CollectStatusCustomActions(statusCustomActions);
            }

            CompleteChaosInvasionPhaseSkillAttempt(actor, chaosInvasionPhaseSkillSlot, skillState);
            OffsetSkillEvents(casts, ordinaryCastStart, damageEvents, ordinaryDamageStart,
                movements, ordinaryMovementStart, petSupportDuration);
            FightBattleSkillDefinition eventSkill = null;
            if (casts.Count > ordinaryCastStart)
                eventSkill = FindSkillBySlot(actor, casts[ordinaryCastStart].Slot);
            else if (movements.Count == 0 && !actor.IsVoodooDoll)
                MoveAwayFromNearestEnemy(actor, movements);
            // Keep unavailable-skill turns non-damaging; the local retreat rule may still move
            // the actor when a legal grid destination increases its distance from enemies.

            if (statusCatalog != null)
                for (int movementIndex = 0; movementIndex < movements.Count; movementIndex++)
                {
                    FightBattleMovementEvent movement = movements[movementIndex];
                    if (statusCatalog != null)
                        foreach (FightBattleUnitState rangeSource in units)
                            FightStatusTriggerRuntime.ApplyMoveRange(rangeSource, units,
                                statusCatalog, random, false);
                    ExecuteMoveNearTriggers(actor, FightStatusTriggerRuntime.MoveNearEventMoved,
                        movement, casts, damageEvents, movements, ref stepDuration,
                        ref stepActionDuration);
                    ExecuteMoveNearTriggers(actor,
                        FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, casts,
                        damageEvents, movements, ref stepDuration, ref stepActionDuration);
                    ExecuteMoveNearTriggers(actor,
                        FightStatusTriggerRuntime.MoveNearEventInRangeMove, movement, casts,
                        damageEvents, movements, ref stepDuration, ref stepActionDuration);
                }

            if (statusCatalog != null)
            {
                List<FightBattleStatusSkillInvocation> roundEndSkills =
                    FightStatusTriggerRuntime.CollectRoundEndSkills(actor, units, random, checkpoint.GridItems);
                foreach (FightBattleUnitState logicOwner in new List<FightBattleUnitState>(units))
                {
                    if (logicOwner == null || logicOwner.Statuses == null) continue;
                    foreach (FightBattleStatusState logicStatus in new List<FightBattleStatusState>(logicOwner.Statuses))
                        if (logicStatus != null && logicOwner.Statuses.Contains(logicStatus))
                            roundEndSkills.AddRange(FightStatusLogicRuntime.Apply(logicOwner, logicStatus,
                                FightStatusLogicEvent.RoundEnd, actor, actor, null, null, units,
                                statusCatalog, random));
                }
                if (roundEndSkills.Count > 0)
                {
                    float triggerDelay = 0f;
                    ExecuteConfiguredStatusSkills(roundEndSkills, casts, damageEvents,
                        movements, ref triggerDelay, out float triggerDuration,
                        out float triggerActionDuration);
                    stepDuration = Math.Max(stepDuration, triggerDuration);
                    if (triggerActionDuration >= 0f)
                        stepActionDuration = Math.Max(stepActionDuration, triggerActionDuration);
                }
            }
            ExecutePetEnergySkill(actor, casts, damageEvents, movements,
                ref stepDuration, ref stepActionDuration);
            ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                ref stepDuration, ref stepActionDuration);
            CollectStatusCustomActions(statusCustomActions);

            roundStartNotForbiddenListener = null;
            skillState.FinishRound();
            turn.ActionRound = actor.SkillState.Round;
            SynchronizeTurn(actor);
            turns.Finish(turn);
            float? previousGridViewTime = gridViewTimeOverride;
            try
            {
                // Round expiration follows action end, independently of lingering
                // skill lifetime tails. This clock changes presentation only.
                gridViewTimeOverride = Math.Max(0f, stepActionDuration);
                FightStatusLifecycle.TickTurn(actor, StatusOwners(), actorRound, FightStatusLifecycle.TimingEnd);
                CleanupGridItems();
            }
            finally { gridViewTimeOverride = previousGridViewTime; }
            RebuildBattleProperties();
            ExecuteAccumulatedDamageEnds(casts, damageEvents, movements,
                ref statusEndDelay, ref stepDuration, ref stepActionDuration);
            RestoreResilienceAfterPersonalRound(actor, actorRound);
            CollectStatusCustomActions(statusCustomActions);
            FightStatusLifecycle.RemoveDestroyedTargets(units);
            ApplyMissingHitSummonRemovalTriggers();
            ExecuteAccumulatedDamageEnds(casts, damageEvents, movements,
                ref statusEndDelay, ref stepDuration, ref stepActionDuration);

            SpawnNextWaveIfCleared();
            RefreshTurnUnits();
            int globalRoundAfter = turns.GlobalRound();
            if (globalRoundAfter != lastGlobalRound)
            {
                lastGlobalRound = globalRoundAfter;
                FightStageStatusRuntime.ApplyGlobalRound(checkpoint.StageStatus, statusCatalog,
                    units, globalRoundAfter, random);
            }
            CollectStatusCustomActions(statusCustomActions);
            UpdateOutcome(globalRound);
            stepDuration = Math.Max(stepDuration, movementStartContext.Duration);
            stepActionDuration = Math.Max(stepActionDuration, movementStartContext.ActionDuration);
            var stepEvent = CreateEvent(actor, eventSkill, performedTargets, before,
                globalRound, movements, casts, damageEvents, currentStepSpawnedUnitIds,
                statusCustomActions, stepDuration, stepActionDuration);
            CompleteGridViewEvents(stepEvent);
            // Phase entry is round-driven, so the original body transition still occurs when
            // break/control prevents the one allowed 18130 release.
            if (checkpoint.StageId == ChaosInvasionStageId
                && checkpoint.StageType == ChaosInvasionStageType
                && globalRound == ChaosInvasionPhaseTwoRound
                && (actor.MonsterId == ChaosInvasionBossEntityId
                    || actor.MonsterTemplateId == ChaosInvasionBossEntityId))
                stepEvent.BodyAnimatorTriggers.Add(new FightBattleBodyAnimatorTriggerEvent
                {
                    UnitId = actor.Id,
                    Parameter = "stage2",
                    Delay = 0f
                });
            currentStepSpawnedUnitIds = null;
            currentStepStatusCustomActions = null;
            currentStepDispersedUnitIds = null;
            movementStartContext = null;
            checkpoint.Counter = counter;
            checkpoint.RandomSeed = random.Seed;
            checkpoint.Receipts.Add(new FightBattleStepReceipt
            {
                Sequence = sequence,
                Counter = counter,
                Event = stepEvent
            });
            FightBattleCheckpointRules.Validate(checkpoint, true);
            return new FightBattleExecutionResult { Checkpoint = checkpoint, Event = stepEvent };
        }

        private void Initialize()
        {
            foreach (FightBattleUnitState unit in units)
            {
                checkpoint.Statistics.Unit(unit.Id);
                new FightSkillState(unit.SkillState).Initialize(unit.SkillCooldownChangeAtStart);
                InitializePetSupportSkills(unit);
                foreach (FightBattleStatusState status in unit.Statuses)
                {
                    FightStatusLifecycle.Initialize(status, 0);
                    FightStatusCooldownRuntime.Initialize(unit, status, random);
                }
            }
            if (checkpoint.StageStatus != null && statusCatalog != null)
                FightStageStatusRuntime.ApplyStart(checkpoint.StageStatus, statusCatalog, units, 0,
                    random);
            RebuildBattleProperties();
            lastGlobalRound = turns.GlobalRound();

            if (checkpoint.Waves.Count > 0)
            {
                for (int i = 0; i < checkpoint.Waves.Count; i++)
                {
                    FightBattleWaveState wave = checkpoint.Waves[i];
                    if (wave == null || wave.UnitIds == null) throw new ArgumentException("Fight wave is invalid.");
                    wave.Spawned = i == 0;
                    foreach (int id in wave.UnitIds)
                    {
                        FightBattleUnitState unit = RequiredUnit(id);
                        if (i == 0)
                        {
                            unit.Ended = false;
                            unit.Turn.RoundType = 0;
                        }
                        else
                        {
                            unit.Ended = true;
                            unit.Turn.RoundType = 1;
                            unit.Turn.Ended = true;
                        }
                    }
                }
            }
            checkpoint.WaveIndex = 0;
            checkpoint.Timeline.Entries.Clear();
            checkpoint.Timeline.PendingChanges.Clear();
            turns.Initialize();
            checkpoint.RoleDiePlayPending = units.Any(unit => unit.Statuses.Any(status =>
                status.ClassId == 25014 && status.RoleDieRulesSupported));
            if (!HasPendingStatusStarts()) UpdateOutcome(1);
            checkpoint.RandomSeed = random.Seed;
        }

        private void ExecutePetEnergySkill(FightBattleUnitState owner,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float stepDuration,
            ref float stepActionDuration)
        {
            if (owner == null || owner.Hp <= 0 || statusCatalog == null) return;
            var states = new List<FightBattlePetEnergyState>();
            if (owner.PetEnergy != null && owner.PetEnergy.Enabled
                && (owner.PetEnergy.OwnerUnitId == 0 || owner.PetEnergy.OwnerUnitId == owner.Id))
                states.Add(owner.PetEnergy);
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit != owner && unit.PetEnergy != null && unit.PetEnergy.Enabled
                    && unit.PetEnergy.OwnerUnitId == owner.Id)
                    states.Add(unit.PetEnergy);

            foreach (FightBattlePetEnergyState state in states)
            {
                if (!FightBattlePetEnergyRuntime.CanRelease(state)) continue;
                FightBattleUnitState caster = CreatePetEntityCaster(owner, state);
                AttachPetEntityPassiveStatuses(caster, owner, state);
                FightBattleSkillDefinition skill = statusCatalog.CreateSkill(state.SkillEntityId,
                    -1, state.SkillItemId, Math.Max(1, state.SkillRank), Math.Max(1, state.SkillLevel),
                    state.SkillSubRank > 0 ? state.SkillSubRank : owner.SubRank, state.SkillProperties);
                List<FightBattleUnitState> targets = FightBattleTargeting.SelectInSkillRange(caster,
                    units, skill, random: random);
                if (targets.Count == 0 && skill.TargetType != 5
                    && !FightBattleTargeting.HasCasterAreaTargets(caster, units, skill)) continue;
                if (!FightBattlePetEnergyRuntime.ConsumeForRelease(state)) continue;

                // Record the entity skill before executing its hit graph, matching ordinary
                // casts so the animation timeline and child-cast order start at the root skill.
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = owner.Id,
                    Presentation = FightBattleSkillCastPresentation.Capture(caster, skill, targets),
                    Slot = -1,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId,
                    IsPetEnergy = true,
                    TargetIds = targets.ConvertAll(value => value.Id)
                };
                casts.Add(cast);
                checkpoint.Statistics.RecordCast(owner.Id, skill.EntityId, skill.ItemId);
                FightBattleUnitState previousPetEnergyHpOwner = petEnergyHpOwner;
                FightBattleUnitState previousPetEnergyHpCaster = petEnergyHpCaster;
                petEnergyHpOwner = owner;
                petEnergyHpCaster = caster;
                float lastDelay, skillDuration, actionDuration;
                try
                {
                    ExecuteSkill(caster, skill, targets, casts, damageEvents, movements,
                        out lastDelay, out skillDuration, out actionDuration, presentationCast: cast);
                }
                finally
                {
                    SynchronizePetEnergyHp(caster);
                    petEnergyHpOwner = previousPetEnergyHpOwner;
                    petEnergyHpCaster = previousPetEnergyHpCaster;
                }
                CompleteCastPresentation(cast, skill, lastDelay);
                owner.Hp = Math.Max(0L, Math.Min(owner.MaxHp, caster.Hp));
                SynchronizeDeathForbidden(owner);
                owner.Turn.Hp = owner.Hp;
                stepDuration = Math.Max(stepDuration,
                    Math.Max(ResolveDuration(skill, lastDelay), skillDuration));
                if (actionDuration >= 0f)
                    stepActionDuration = Math.Max(stepActionDuration, actionDuration);
            }
        }

        private static FightBattleUnitState CreatePetEntityCaster(FightBattleUnitState owner,
            FightBattlePetEnergyState state)
        {
            var caster = new FightBattleUnitState
            {
                Id = owner.Id,
                MasterId = owner.Id,
                Side = owner.Side,
                CharacterType = 5,
                Level = Math.Max(1, state.SkillLevel),
                SubRank = state.SkillSubRank > 0 ? state.SkillSubRank : owner.SubRank,
                Profession = owner.Profession,
                X = owner.X,
                Y = owner.Y,
                Direction = owner.Direction,
                Hp = owner.Hp,
                MaxHp = owner.MaxHp,
                LastMaxHp = owner.LastMaxHp,
                Properties = state.EntityProperties == null || state.EntityProperties.Count == 0
                    ? new List<FightPropertyValue>(owner.Properties)
                    : state.EntityProperties.Select(value => new FightPropertyValue
                    { Id = value.Id, Value = value.Value }).ToList(),
                Statuses = new List<FightBattleStatusState>(),
                Skills = state.EntityActiveSkills == null
                    ? new List<FightBattleSkillDefinition>()
                    : state.EntityActiveSkills,
                SkillState = new FightSkillAgentState(),
                Turn = new FightTurnUnit { Id = owner.Id, MasterId = owner.Id, Hp = owner.Hp }
            };
            caster.Hp = Math.Min(caster.Hp, caster.MaxHp);
            return caster;
        }

        private void AttachPetEntityPassiveStatuses(FightBattleUnitState caster,
            FightBattleUnitState owner, FightBattlePetEnergyState state)
        {
            if (statusCatalog == null || state.EntityPassiveStatusIds == null) return;
            int index = 0;
            foreach (int classId in state.EntityPassiveStatusIds)
            {
                if (classId <= 0) continue;
                FightBattleStatusState status = statusCatalog.CreateStatus(classId, caster.Id, owner.Id,
                    ++index, state.SkillEntityId, state.SkillItemId, Math.Max(1, state.SkillRank),
                    Math.Max(1, state.SkillLevel), state.SkillSubRank, state.EntityProperties);
                status.Origin = new FightBattleStatusOrigin
                {
                    IsSet = true, UnitId = caster.Id, EntityId = caster.Id,
                    SkillItemId = state.SkillItemId, SkillEntityId = state.SkillEntityId
                };
                caster.Statuses.Add(status);
            }
        }

        private void ExecuteSkill(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> targets, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            out float lastDelay, FightBattleUnitState targetPosition = null, bool allowDeadSource = false,
            double externalDamageFactor = 1.0, FightBattleSkillCastEvent presentationCast = null)
        {
            ExecuteSkill(actor, skill, targets, casts, damageEvents, movements, out lastDelay,
                out float nestedDuration, out float nestedActionDuration,
                targetPosition, allowDeadSource, externalDamageFactor, presentationCast: presentationCast);
            if (movementStartContext != null)
            {
                movementStartContext.Duration = Math.Max(movementStartContext.Duration, nestedDuration);
                movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration,
                    nestedActionDuration);
            }
        }

        private void ExecuteSkill(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> targets, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            out float lastDelay, out float statusTriggerDuration,
            out float statusTriggerActionDuration, FightBattleUnitState targetPosition = null,
            bool allowDeadSource = false, double externalDamageFactor = 1.0,
            FightBattleUnitState targetingSource = null, FightBattleSkillCastEvent presentationCast = null)
        {
            // Movement listeners have local times, just like other nested skills. Return
            // their lifetime to the caller before that caller offsets the whole cast.
            var previousMovementContext = movementStartContext;
            var previousConditionalProperties = conditionalHitProperties;
            var previousGridViewCast = gridViewCast;
            var previousGridViewTime = gridViewTimeOverride;
            gridViewCast = presentationCast;
            gridViewTimeOverride = null;
            conditionalHitProperties = new FightConditionalHitProperties(actor, skill, skillItems, units, statusCatalog, checkpoint.GridItems);
            movementStartContext = new MovementStartContext { Casts = casts, DamageEvents = damageEvents, Movements = movements };
            try
            {
                ExecuteSkillCore(actor, skill, targets, casts, damageEvents, movements,
                    out lastDelay, out statusTriggerDuration, out statusTriggerActionDuration,
                    targetPosition, allowDeadSource, externalDamageFactor, targetingSource);
                conditionalHitProperties.EndCast();
                statusTriggerDuration = Math.Max(statusTriggerDuration, movementStartContext.Duration);
                statusTriggerActionDuration = Math.Max(statusTriggerActionDuration,
                    movementStartContext.ActionDuration);
            }
            finally
            {
                SynchronizePetEnergyHp(actor);
                conditionalHitProperties = previousConditionalProperties;
                movementStartContext = previousMovementContext;
                gridViewCast = previousGridViewCast;
                gridViewTimeOverride = previousGridViewTime;
            }
        }

        private void ExecuteSkillCore(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> targets, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            out float lastDelay, out float statusTriggerDuration,
            out float statusTriggerActionDuration, FightBattleUnitState targetPosition,
            bool allowDeadSource, double externalDamageFactor, FightBattleUnitState targetingSource)
        {
            lastDelay = 0f;
            statusTriggerDuration = 0f;
            statusTriggerActionDuration = -1f;
            // Support settles against the owner state, but its original target
            // filters (notably TargetType.Master) belong to the pet source.
            targetingSource = targetingSource ?? actor;
            var hitCast = FightBattleSkillCastPresentation.Capture(actor, skill, targets, targetPosition);
            // User-confirmed solo rule: a successful 18130 directly kills opposing player units;
            // the original 18131 single-cell hit is self-targeted in the client data.
            bool chaosInvasionWipe = IsChaosInvasionPhaseSkill(actor, skill);
            if (targets != null && targets.Count > 0)
                actor.AiPreviousTargetId = targets[0].Id;
            FightBattleDamageEvent skillCost = ApplySkillCost(actor, skill);
            if (skillCost != null) damageEvents.Add(skillCost);
            long skillCostHp = skillCost == null
                ? 0L : Math.Max(0L, skillCost.BeforeHp - skillCost.AfterHp);
            FightBattlePetEnergyRuntime.RecordOwnerDamage(units, actor.Id, skillCostHp);
            if (skillCostHp > 0)
            {
                ApplyHpChangeTriggers(actor, skill);
                SynchronizeDeathForbidden(actor);
            }

            var skillLogic = FightSkillLogicRuntime.Create(actor, skill, units, statusCatalog, random, checkpoint.GridItems);
            if (skillLogic != null && skillLogic.OnSkillStart()) RebuildBattleProperties();
            if (skillLogic != null && skillLogic.OnConfiguredSkillStart(
                targetPosition ?? (targets.Count > 0 ? targets[0] : null))) RebuildBattleProperties();
            ExecuteLogicChildSkills(actor, skill, skillLogic, null,
                targetPosition ?? (targets.Count > 0 ? targets[0] : null), hitCast,
                casts, damageEvents, movements, ref lastDelay,
                ref statusTriggerDuration, ref statusTriggerActionDuration);

            if (statusCatalog != null)
            {
                List<FightBattleStatusSkillInvocation> actionStartSkills =
                    FightStatusTriggerRuntime.CollectActionStartSkills(actor, skill, units, random);
                if (actionStartSkills.Count > 0)
                {
                    float triggerDelay = 0f;
                    ExecuteConfiguredStatusSkills(actionStartSkills, casts, damageEvents,
                        movements, ref triggerDelay, out float startDuration,
                        out float startActionDuration);
                    statusTriggerDuration = Math.Max(statusTriggerDuration, startDuration);
                    statusTriggerActionDuration = Math.Max(statusTriggerActionDuration, startActionDuration);
                    lastDelay = Math.Max(lastDelay, triggerDelay);
                }
                List<FightBattleStatusSkillInvocation> skillStartSkills =
                    FightStatusTriggerRuntime.CollectSkillStartSkills(actor, skill, targets,
                        units, random, statusCatalog);
                if (skillStartSkills.Count > 0)
                {
                    float triggerDelay = 0f;
                    ExecuteConfiguredStatusSkills(skillStartSkills, casts, damageEvents,
                        movements, ref triggerDelay, out float startDuration,
                        out float startActionDuration);
                    statusTriggerDuration = Math.Max(statusTriggerDuration, startDuration);
                    statusTriggerActionDuration = Math.Max(statusTriggerActionDuration, startActionDuration);
                    lastDelay = Math.Max(lastDelay, triggerDelay);
                }
            }

            // Start triggers settle synchronously and may kill the actor. Preserve the explicit
            // death-trigger exception; ordinary hits require a living source.
            if (!allowDeadSource && actor.Hp <= 0) return;

            double skillDamageScale = 1.0;
            if (statusCatalog != null)
            {
                skillDamageScale = FightStatusTriggerRuntime.PrepareConsumeStackSkillDamageScale(
                    actor, skill, statusCatalog, random);
                if (skillDamageScale != 1.0)
                    RebuildBattleProperties();
            }

            FightDamageFalloff.BeginCast(skill, ref checkpoint.FalloffSerial);
            IReadOnlyList<FightBattleHitDefinition> hits = FightBattleHitCollector.CollectAll(skill);
            var hitTargetSelectors = new FightHitTargetSelectors();
            var damageHitTargets = new HashSet<int>();
            var completedSourceMoves = new HashSet<FightBattleSkillMove>();

            foreach (FightBattleHitDefinition hit in hits)
            {
                if (!allowDeadSource && actor.Hp <= 0) break;
                ApplySourceMoves(actor, skill, targets, movements, ref lastDelay,
                    hit.Delay, completedSourceMoves, targetPosition);
                if (hit.Delay > lastDelay) lastDelay = hit.Delay;
                List<FightBattleUnitState> hitTargets;
                IReadOnlyList<FightHitOccurrence> plannedHits = null;
                bool hasRandomTargetContext = hit.RandomTargetGroupId > 0;
                if (chaosInvasionWipe)
                    hitTargets = targets.Where(target => target != null
                        && target.CharacterType == 1 && target.Side != actor.Side
                        && target.Hp > 0 && !target.Ended).ToList();
                else
                {
                    var selection = hitTargetSelectors.For(hit).Select(new FightHitTargetSelectionContext {
                        Source = actor, TargetingSource = targetingSource, Units = units,
                        Skill = skill, Hit = hit, Targets = targets,
                        Primary = targetPosition ?? (targets.Count > 0 ? targets[0] : null),
                        Cast = hitCast, Grid = checkpoint.Grid, Random = random
                    });
                    if (selection == null) continue;
                    plannedHits = selection.Occurrences;
                    hitTargets = selection.Targets;
                }
                ExecuteLogicChildSkills(actor, skill, skillLogic, hit,
                    targetPosition ?? (targets.Count > 0 ? targets[0] : null), hitCast,
                    casts, damageEvents, movements, ref lastDelay,
                    ref statusTriggerDuration, ref statusTriggerActionDuration);
                if (hit.IsSummon)
                {
                    if (hit.SummonConfigType == "FightHitSummonGridItemCfg")
                    {
                        lastDelay = Math.Max(lastDelay, SpawnGridItems(actor, skill, hit,
                            hasRandomTargetContext ? hitTargets : targets, targetPosition));
                        continue;
                    }
                    SpawnSummons(actor, skill, hit,
                        hasRandomTargetContext ? hitTargets : targets);
                    continue;
                }
                decimal hitRemovedHp = 0m;
                float hitFinishDelay = Math.Max(0f, hit.Delay);
                for (int targetIndex = 0; targetIndex < hitTargets.Count; targetIndex++)
                {
                    FightBattleUnitState target = hitTargets[targetIndex];
                    FightHitOccurrence occurrence = plannedHits == null ? null : plannedHits[targetIndex];
                    if (!allowDeadSource && actor.Hp <= 0) break;
                    if (target.Hp <= 0 && hit.ActionType != 62 || target.Ended) continue;
                    if (target.IsVoodooDoll && (hit.ActionType == 5 || skill.SkillType == 3)) continue;
                    float hitDelay = HitDelay(actor, target, hit) + (occurrence == null ? 0f : occurrence.Delay);
                    if (hitDelay > lastDelay) lastDelay = hitDelay;
                    hitFinishDelay = Math.Max(hitFinishDelay, hitDelay);
                    movementStartContext.StatusStartTime = Math.Max(0f, hitDelay);
                    long beforeHp = target.Hp;
                    int damageId = NextDamageId();
                    bool originalHasCustomDamage = hit.HasCustomDamage;
                    long originalCustomDamage = hit.CustomDamage;
                    if (!chaosInvasionWipe)
                        ApplyResilienceDamageOverride(actor, target, skill, hit);
                    float elementConfrontation = 0f;
                    bool elementConfrontationResolved = true;
                    if (!chaosInvasionWipe && statusCatalog != null
                        && hit.ActionType != 0 && hit.ActionType != 5
                        && skill.SkillType != 3 && hit.DamageType >= 1 && hit.DamageType <= 7)
                        elementConfrontationResolved = statusCatalog.TryResolveElementConfrontation(
                            target, skill.ElementType, out elementConfrontation);
                    FightBattleHitResult resolved;
                    if (chaosInvasionWipe)
                    {
                        target.Hp = 0;
                        SynchronizeTurn(target);
                        resolved = new FightBattleHitResult
                        {
                            Type = new FightDamageTypeData(),
                            Formula = new FightDamageFormulaResult
                            {
                                Damage = beforeHp,
                                AbsoluteDamage = beforeHp
                            }
                        };
                    }
                    else
                    {
                        try
                        {
                            resolved = hitExecutor.Apply(random, damageId, actor, target,
                                skill, hit, checkpoint.StageType, checkpoint.IsPvp, elementConfrontation,
                                new List<int>(), allowDeadSource, skillDamageScale
                                    * (hit.UseDamageFactor ? externalDamageFactor : 1.0), conditionalHitProperties,
                                skillLogic == null ? (Action<FightDamageTypeData>)null
                                    : type => {
                                        skillLogic.OnDoHitTransient(target, hit, type);
                                        if (skillLogic.OnConfiguredTransient(target, hit, type)) RebuildBattleProperties();
                                    },
                                skillLogic == null ? (Action<FightBattleTransientDamageContext>)null
                                    : damage => skillLogic.OnTransientDamage(target, hit, damage),
                                cure => ApplyCureInterceptors(actor, target, skill, cure));
                        }
                        finally
                        {
                            hit.HasCustomDamage = originalHasCustomDamage;
                            hit.CustomDamage = originalCustomDamage;
                        }
                    }
                    if (resolved.Formula == null && resolved.Settlement == null) continue;
                    // Capture only this occurrence's HP change before any HP sharing,
                    // status response or child skill can change a participant's state.
                    if (!resolved.IsCure)
                        hitRemovedHp += Math.Max(0m, (decimal)beforeHp - target.Hp);
                    SynchronizePetEnergyHp(target);
                    long targetHpIncrease = Math.Max(0L, target.Hp - beforeHp);
                    var damage = new FightBattleDamageEvent
                    {
                        DamageId = damageId,
                        SourceId = actor.Id,
                        TargetId = target.Id,
                        SkillEntityId = skill.EntityId,
                        SkillItemId = skill.ItemId,
                        HitEntityId = hit.EntityId,
                        DamageEntityId = hit.DamageId,
                        DamageType = chaosInvasionWipe ? 1 : hit.DamageType,
                        ElementType = skill == null ? 0 : skill.ElementType,
                        ElementConfrontation = elementConfrontation,
                        IsElementAffect = resolved.Formula != null && resolved.Formula.ElementAffected,
                        ElementConfrontationUnresolved = !elementConfrontationResolved,
                        Delay = hitDelay,
                        Anchor = hit.Anchor,
                        HasHitGridPosition = occurrence != null && occurrence.HasGridPosition,
                        HitGridX = occurrence == null ? 0 : occurrence.GridPosition.X,
                        HitGridY = occurrence == null ? 0 : occurrence.GridPosition.Y,
                        IsCure = resolved.IsCure,
                        Critical = resolved.Type.Critical,
                        Block = resolved.Type.Block,
                        Dodge = resolved.Type.Dodge,
                        Immunity = resolved.Type.Immunity,
                        Blinding = resolved.Type.Blinding,
                        IgnoreShield = chaosInvasionWipe || hit.IgnoreShield,
                        TrueDamage = chaosInvasionWipe || hit.TrueDamage,
                        BreakResilience = hit.BreakResilience,
                        Damage = chaosInvasionWipe ? beforeHp
                            : resolved.Settlement == null ? 0 : resolved.Settlement.RealDamageValue,
                        HasDamageValue = chaosInvasionWipe || resolved.Settlement != null,
                        DamageValue = chaosInvasionWipe ? beforeHp
                            : resolved.Settlement == null ? 0 : resolved.Settlement.DamageValue,
                        ShieldDamage = resolved.ShieldDamage,
                        HasAbsoluteDamage = chaosInvasionWipe || resolved.Formula != null,
                        AbsoluteDamage = resolved.Formula == null ? 0 : resolved.Formula.AbsoluteDamage,
                        BeforeHp = beforeHp,
                        AfterHp = target.Hp,
                        MaxHp = target.MaxHp
                    };
                    damageEvents.Add(damage);
                    checkpoint.Statistics.RecordHit(damage, damageHitTargets);
                    FightStatusAccumulatedDamageRuntime.RecordHit(target, damage, skill);
                    TransmitVoodooDamage(target, damage, damageEvents, casts);
                    var triggerSkills = new List<int>();
                    var configuredTriggerSkills = new List<FightBattleStatusStateTrigger>();
                    var configuredComponentInvocations =
                        new List<FightBattleStatusSkillInvocation>();
                    var configuredHpTriggerSkills = new List<FightBattleStatusSkillInvocation>();
                    var configuredKillSkillInvocations =
                        new List<FightBattleStatusSkillInvocation>();
                    var hpTriggerSkills = new List<FightBattleStatusStateTrigger>();
                    var hemophagiaTriggerSkills = new List<FightBattleStatusStateTrigger>();
                    configuredTriggerSkills.AddRange(resolved.TriggeredStatusSkillInvocations);
                    if (resolved.TriggeredStatusSkillInvocations.Count == 0)
                        triggerSkills.AddRange(resolved.TriggeredStatusSkills);
                    long actualHpLoss = Math.Max(0L, beforeHp - target.Hp);
                    FightBattlePetEnergyRuntime.RecordOwnerDamage(units, target.Id, actualHpLoss);
                    CollectUnflinchingActions(target, actualHpLoss, skill);
                    FightStatusTriggerRuntime.ApplyAccumulatedDamage(target,
                        actualHpLoss, statusCatalog, random, configuredTriggerSkills, skill);
                    ApplyHitStatuses(actor, target, skill, hit, random, skillCostHp,
                        actualHpLoss);
                    CollectHitTriggerSkill(actor, target, skill, hit, configuredComponentInvocations);
                    ApplyMissingStatusComponentsOnHit(actor, target, skill, hit, resolved,
                        actualHpLoss, configuredComponentInvocations);
                    foreach (FightBattleUnitState logicOwner in new[] { actor, target })
                    {
                        if (logicOwner == null || logicOwner.Statuses == null) continue;
                        foreach (FightBattleStatusState logicStatus in new List<FightBattleStatusState>(logicOwner.Statuses))
                        {
                            if (logicStatus == null || !logicOwner.Statuses.Contains(logicStatus)) continue;
                            configuredComponentInvocations.AddRange(FightStatusLogicRuntime.Apply(logicOwner,
                                logicStatus, ReferenceEquals(logicOwner, actor)
                                    ? FightStatusLogicEvent.DoDamage : FightStatusLogicEvent.BeDamage,
                                actor, target, skill, hit, units, statusCatalog, random));
                        }
                    }
                    ApplyResilienceHit(actor, target, skill, hit, resolved, casts,
                        damageEvents, movements, ref lastDelay);
                    ApplyHitDisperse(actor, target, hit, random);
                    ApplyHitMove(actor, target, skill, hit, movements);
                    long recoveredHp = ApplyHemophagia(actor, hit, damage, hitDelay, skill, damageEvents);
                    if (recoveredHp > 0 && actor != target)
                    {
                        SynchronizePetEnergyHp(actor);
                        FightStatusTriggerRuntime.Apply(actor, statusCatalog, false, hemophagiaTriggerSkills,
                            random, skill, units, configuredHpTriggerSkills,
                            executeHpSkills: ExecuteHpStartupSkills);
                    }
                    SynchronizePetEnergyHp(target);
                    FightStatusTriggerRuntime.Apply(target, statusCatalog, false, hpTriggerSkills,
                        random, skill, units, configuredHpTriggerSkills,
                        executeHpSkills: ExecuteHpStartupSkills);
                    ApplyHpIncreaseTriggers(target, targetHpIncrease, skill);
                    if (recoveredHp > 0) ApplyHpIncreaseTriggers(actor, recoveredHp, skill);
                    if (target.Statuses != null)
                        foreach (FightBattleStatusState logicStatus in new List<FightBattleStatusState>(target.Statuses))
                            if (logicStatus != null && target.Statuses.Contains(logicStatus))
                                configuredComponentInvocations.AddRange(FightStatusLogicRuntime.Apply(target,
                                    logicStatus, FightStatusLogicEvent.AfterBeDamage, actor, target,
                                    skill, hit, units, statusCatalog, random));
                    RebuildBattleProperties();
                    float endedDuration = 0f, endedActionDuration = -1f;
                    ExecuteAccumulatedDamageEnds(casts, damageEvents, movements,
                        ref lastDelay, ref endedDuration, ref endedActionDuration);
                    statusTriggerDuration = Math.Max(statusTriggerDuration, endedDuration);
                    statusTriggerActionDuration = Math.Max(statusTriggerActionDuration, endedActionDuration);
                    if (triggerSkills.Count > 0)
                        ExecuteTriggerSkills(target, triggerSkills, casts, damageEvents, movements,
                            ref lastDelay);
                    if (configuredTriggerSkills.Count > 0)
                        ExecuteConfiguredTriggerSkills(target, configuredTriggerSkills, casts,
                            damageEvents, movements, ref lastDelay, out _, out _);
                    if (configuredComponentInvocations.Count > 0)
                        ExecuteConfiguredStatusSkills(configuredComponentInvocations, casts,
                            damageEvents, movements, ref lastDelay, out _, out _);
                    if (configuredHpTriggerSkills.Count > 0)
                        ExecuteConfiguredStatusSkills(configuredHpTriggerSkills, casts,
                            damageEvents, movements, ref lastDelay, out _, out _);
                    if (hpTriggerSkills.Count > 0)
                        ExecuteTriggerSkills(target, hpTriggerSkills, casts, damageEvents, movements,
                            ref lastDelay);
                    if (hemophagiaTriggerSkills.Count > 0)
                        ExecuteTriggerSkills(actor, hemophagiaTriggerSkills, casts, damageEvents, movements,
                            ref lastDelay);
                    // Confirm death after this hit's HP effects and immediate revival,
                    // including role-death responses, rather than on the first zero HP.
                    if (recoveredHp > 0 && actor != target) SynchronizeDeathForbidden(actor);
                    if (target.Hp <= 0)
                    {
                        List<FightBattleStatusSkillInvocation> roleDieInvocations =
                            FightStatusTriggerRuntime.CollectRoleDieSkills(target, units,
                                statusCatalog, random);
                        if (roleDieInvocations.Count > 0)
                            ExecuteConfiguredStatusSkills(roleDieInvocations, casts, damageEvents,
                                movements, ref lastDelay, out _, out _);
                        ExecuteRevivalSkills(casts, damageEvents, movements, ref lastDelay,
                            ref statusTriggerDuration, ref statusTriggerActionDuration);
                        SynchronizeDeathForbidden(target);
                        if (target.Hp <= 0)
                            FightStatusTriggerRuntime.ApplyKillSkills(actor, target, skill, units,
                                statusCatalog, random, configuredKillSkillInvocations);
                    }
                    else SynchronizeDeathForbidden(target);
                    if (configuredKillSkillInvocations.Count > 0)
                    {
                        ExecuteConfiguredStatusSkills(configuredKillSkillInvocations, casts,
                            damageEvents, movements, ref lastDelay, out float killDuration,
                            out float killActionDuration);
                        statusTriggerDuration = Math.Max(statusTriggerDuration, killDuration);
                        if (killActionDuration >= 0f)
                            statusTriggerActionDuration = Math.Max(statusTriggerActionDuration,
                                killActionDuration);
                    }
                    // Death listeners and immediate revival settle before a summoned unit is
                    // considered removed. Property rebuilds alone are not removal boundaries.
                    ApplyMissingHitSummonRemovalTriggers(target);
                    CleanupGridItems();
                    ApplyChildSkill(actor, target, skill, hit, casts, damageEvents, movements, ref lastDelay);
                }
                ExecuteLogicChildRequests(actor, skill,
                    skillLogic?.CollectHitFinishSkills(hit, hitRemovedHp, hitFinishDelay),
                    casts, damageEvents, movements, ref lastDelay,
                    ref statusTriggerDuration, ref statusTriggerActionDuration);
            }
            movementStartContext.StatusStartTime = 0f;
            ApplySourceMoves(actor, skill, targets, movements, ref lastDelay,
                float.PositiveInfinity, completedSourceMoves, targetPosition);
            FightDamageFalloff.EndCast(units, skill);
            ApplyExtraSkills(actor, skill, casts, damageEvents, movements, ref lastDelay);
            if (statusCatalog != null)
            {
                movementStartContext.StatusStartTime = Math.Max(0f, ResolveActionDuration(skill, lastDelay));
                var actionEndSkills = new List<FightBattleStatusSkillInvocation>();
                if (FightStatusTriggerRuntime.ApplySkillEndStatuses(actor, skill, units,
                    statusCatalog, random, actionEndSkills) > 0)
                    RebuildBattleProperties();
                foreach (FightBattleStatusState logicStatus in new List<FightBattleStatusState>(actor.Statuses))
                    if (logicStatus != null && actor.Statuses.Contains(logicStatus))
                        actionEndSkills.AddRange(FightStatusLogicRuntime.Apply(actor, logicStatus,
                            FightStatusLogicEvent.SkillEnd, actor, actor, skill, null, units,
                            statusCatalog, random));
                if (actionEndSkills.Count > 0)
                {
                    ExecuteConfiguredStatusSkills(actionEndSkills, casts, damageEvents,
                        movements, ref lastDelay, out float actionEndDuration,
                        out float actionEndActionDuration);
                    statusTriggerDuration = Math.Max(statusTriggerDuration, actionEndDuration);
                    if (actionEndActionDuration >= 0f)
                        statusTriggerActionDuration = Math.Max(statusTriggerActionDuration,
                            actionEndActionDuration);
                }
            }
            ApplySkillCombo(actor, skill, targets, casts, damageEvents, movements, lastDelay,
                ref statusTriggerDuration, ref statusTriggerActionDuration);
            ApplySkillReplay(actor, skill, casts, damageEvents, movements, lastDelay,
                ref statusTriggerDuration, ref statusTriggerActionDuration);
        }

        private void SpawnSummons(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, List<FightBattleUnitState> targets)
        {
            if (statusCatalog == null || skillItems == null)
                throw new InvalidOperationException("Official combat and skill item catalogs are required to spawn monsters.");
            if (hit.SummonPools == null || hit.SummonPools.Count == 0
                || hit.SummonPositions == null || hit.SummonPositions.Count == 0) return;

            FightBattleUnitState reference = ResolveSummonReference(actor, hit.SummonReferenceType,
                targets);
            if (reference == null) return;
            for (int i = 0; i < hit.SummonPositions.Count; i++)
            {
                FightBattleSummonPool pool = SelectSummonPool(actor.Id, hit);
                if (pool == null) break;

                bool isDoll = hit.SummonConfigType == "FightHitSummonVoodooDollCfg";
                FightBattleUnitState origin = isDoll ? actor : reference;
                FightGridPoint offset = hit.SummonPositions[i];
                if (origin.Direction == 1) offset.X = -offset.X;
                var position = new FightGridPoint(origin.X + offset.X, origin.Y + offset.Y);
                int level = hit.SummonLevelType == 1 ? Math.Max(1, skill.Level)
                    : Math.Max(1, actor.Level);
                FightBattleUnitState summon = isDoll
                    ? CreateVoodooDoll(actor, skill, pool, reference, position)
                    : hit.SummonConfigType == "FightHitSummonPetCfg"
                        ? CreatePetSummon(actor, skill, pool, hit, position, reference)
                        : CreateSummonedUnit(actor, skill, pool, hit, level, position, reference);
                if (!CanPlaceSummon(summon)) continue;

                units.Add(summon);
                unitById.Add(summon.Id, summon);
                summon.StatusStarted = OnStatusStarted;
                summon.StatusEnded = OnStatusEnded;
                summon.HpUpdated = OnHpUpdated;
                summon.HpIncreased = OnHpIncreased;
                summon.StatusForbiddenChanged = OnStatusForbiddenChanged;
                summon.StatusRoundResolver = StatusClockRound;
                summon.RoundStartClockResolver = RoundStartClockRound;
                foreach (var status in summon.Statuses) InitializeRoundStartDelay(status);
                checkpoint.Units.Add(summon);
                checkpoint.Statistics.Unit(summon.Id);
                turns.RegisterUnit(summon.Turn, hit.SummonImmediateRound);
                if (currentStepSpawnedUnitIds != null)
                    currentStepSpawnedUnitIds.Add(summon.Id);
                AddSummonStatuses(summon, actor, skill, pool, hit);
                if (isDoll)
                {
                    FightBattleStatusState mark = ApplySummonStatus(reference, actor, skill,
                        summon.VoodooDoll.VoodooStatusId);
                    summon.VoodooDoll.VoodooStatusInstanceId = mark == null ? 0 : mark.InstanceId;
                    ApplySummonStatus(summon, reference, skill, summon.VoodooDoll.FollowDieStatusId);
                }
                // Initial monster passives precede registration; additional statuses start
                // through StatusStarted. Sweep only still-pending HP starts, once each.
                foreach (var status in new List<FightBattleStatusState>(summon.Statuses))
                    if (summon.Statuses.Contains(status)) StartHpStatus(summon, status);
                ExecuteMissingHitSummon(summon, true);
                RebuildBattleProperties();
            }
        }

        private FightBattleUnitState ResolveSummonReference(FightBattleUnitState actor,
            int referenceType, List<FightBattleUnitState> targets)
        {
            switch (referenceType)
            {
                case 2:
                    return unitById.TryGetValue(actor.MasterId, out FightBattleUnitState master)
                        ? master : actor;
                case 3:
                case 4:
                    return targets != null && targets.Count > 0 ? targets[0] : null;
                default:
                    return actor;
            }
        }

        private FightBattleSummonPool SelectSummonPool(int masterId,
            FightBattleHitDefinition hit)
        {
            var available = new List<FightBattleSummonPool>();
            foreach (FightBattleSummonPool pool in hit.SummonPools)
            {
                if (pool == null || pool.MonsterId <= 0 || pool.MaxSummonCount <= 0) continue;
                int count = 0;
                foreach (FightBattleUnitState unit in units)
                    if (unit != null && unit.Hp > 0 && !unit.Ended && unit.MasterId == masterId
                        && unit.MonsterId == pool.MonsterId) count++;
                if (count < pool.MaxSummonCount) available.Add(pool);
            }
            if (available.Count == 0) return null;
            if (!hit.SummonIsRandom) return available[0];
            int index = Math.Min((int)(random.Next() * available.Count), available.Count - 1);
            return available[index];
        }

        private FightBattleUnitState CreateSummonedUnit(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, FightBattleSummonPool pool,
            FightBattleHitDefinition hit, int level, FightGridPoint position,
            FightBattleUnitState reference)
        {
            int groupNo = actor.MonsterGroupNo;
            var properties = CopyProperties(statusCatalog.CalculateMonsterProps(
                pool.MonsterId, groupNo, level, Math.Max(0, actor.SubRank)));
            if (hit.SummonInheritProps)
            {
                var sourceProperties = new Dictionary<int, long>();
                foreach (var property in reference.Properties)
                    sourceProperties[property.Id] = property.Value;
                FightSummonPropertyInheritance inheritance = statusCatalog.CalculateSummonInheritance(
                    pool.MonsterId, skill.Level, skill.Rank, actor.SubRank, sourceProperties);
                if (inheritance.Fixed != null)
                    foreach (var property in inheritance.Fixed)
                        properties[property.Key] = checked(Property(properties, property.Key) + property.Value);
                if (inheritance.Summoner != null)
                    foreach (var property in inheritance.Summoner)
                        properties[property.Key] = checked(Property(properties, property.Key) + property.Value);
            }
            var passiveStatusIds = new List<int>();
            foreach (int itemId in statusCatalog.MonsterPassiveSkillItems(pool.MonsterId, groupNo))
            {
                FightOfficialSkillItem item = skillItems.Required(itemId);
                IReadOnlyDictionary<int, long> passive = statusCatalog.CalculatePassiveSkillProps(
                    item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                    item.PassiveRankFactorId, item.PassivePropFactors, 1, level,
                    Math.Max(0, actor.SubRank));
                foreach (KeyValuePair<int, long> property in passive)
                    properties[property.Key] = properties.TryGetValue(property.Key,
                        out long current) ? checked(current + property.Value) : property.Value;
                if (item.PassiveStatusIds != null) passiveStatusIds.AddRange(item.PassiveStatusIds);
            }

            int masterId = ResolveSummonMaster(actor, hit.SummonReferenceType);
            List<FightGridPoint> bodyOffsets = statusCatalog.MonsterBodyOffsets(pool.MonsterId);
            if (bodyOffsets.Count == 0) bodyOffsets.Add(new FightGridPoint(0, 0));
            var summon = new FightBattleUnitState
            {
                Id = NextSummonUnitId(),
                MasterId = masterId,
                Side = actor.Side,
                CharacterType = statusCatalog.MonsterCharacterType(pool.MonsterId),
                AiType = statusCatalog.MonsterAiType(pool.MonsterId),
                MonsterId = pool.MonsterId,
                MonsterTemplateId = statusCatalog.MonsterTemplateId(pool.MonsterId),
                MonsterGroupNo = groupNo,
                MonsterType = statusCatalog.MonsterType(pool.MonsterId),
                MaxHpPlies = statusCatalog.MonsterHpPliesFor(pool.MonsterId, groupNo, level),
                Level = level,
                SubRank = Math.Max(0, actor.SubRank),
                Profession = actor.Profession,
                Wave = actor.Wave,
                SkillCooldownChangeAtStart = statusCatalog.MonsterCooldownChangeAtStart(pool.MonsterId),
                MoveMask = actor.MoveMask,
                X = position.X,
                Y = position.Y,
                Hp = Property(properties, formula.PropertyId("MaxHp")),
                MaxHp = Property(properties, formula.PropertyId("MaxHp")),
                LastMaxHp = Property(properties, formula.PropertyId("MaxHp")),
                BaseProperties = ToPropertyList(properties),
                Properties = ToPropertyList(properties),
                BodyOffsets = bodyOffsets
            };
            if (summon.MaxHp <= 0)
                throw new InvalidOperationException("Official summoned monster has no positive MaxHp: "
                    + pool.MonsterId);
            summon.MoveDist = formula.CalculateMoveDist(properties);
            summon.Turn.Id = summon.Id;
            summon.Turn.MasterId = summon.MasterId;
            summon.Turn.Hp = summon.Hp;
            summon.Turn.Speed = (int)Math.Max(1L, Property(properties, formula.PropertyId("Speed")));

            List<int> activeSkillItems = statusCatalog.MonsterActiveSkillItems(pool.MonsterId,
                groupNo);
            var sourceProps = new Dictionary<int, long>(properties);
            for (int slot = 0; slot < activeSkillItems.Count; slot++)
            {
                int itemId = activeSkillItems[slot];
                FightOfficialSkillItem item = skillItems.Required(itemId);
                if (item.EntityId <= 0) continue;
                IReadOnlyDictionary<int, long> skillProps = statusCatalog.CalculateSkillProps(
                    item.EntityId, 1, level, summon.SubRank);
                FightBattleSkillDefinition definition = statusCatalog.CreateSkill(item.EntityId,
                    slot, itemId, 1, level, summon.SubRank, ToPropertyList(skillProps));
                long cooldown = statusCatalog.CalculateSkillCooldown(item.EntityId, 1, level,
                    summon.SubRank, sourceProps);
                summon.Skills.Add(definition);
                summon.SkillState.Slots.Add(statusCatalog.CreateSkillSlot(definition, cooldown));
            }
            new FightSkillState(summon.SkillState).Initialize(summon.SkillCooldownChangeAtStart);

            foreach (int statusId in statusCatalog.MonsterInitialStatusIds(pool.MonsterId))
                ApplySummonStatus(summon, actor, skill, statusId);
            foreach (int statusId in passiveStatusIds) ApplySummonStatus(summon, actor, skill, statusId);
            return summon;
        }

        private int ResolveSummonMaster(FightBattleUnitState actor, int referenceType)
        {
            switch (referenceType)
            {
                case 1: return actor.Id;
                case 2: return actor.MasterId > 0 ? actor.MasterId : actor.Id;
                default: return 0;
            }
        }

        private bool CanPlaceSummon(FightBattleUnitState summon)
        {
            var occupied = new HashSet<FightGridPoint>();
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null || unit.Hp <= 0 || unit.Ended) continue;
                occupied.Add(new FightGridPoint(unit.X, unit.Y));
                if (unit.BodyOffsets != null)
                    foreach (FightGridPoint offset in unit.BodyOffsets)
                        occupied.Add(new FightGridPoint(unit.X + offset.X, unit.Y + offset.Y));
            }
            var own = new HashSet<FightGridPoint>();
            IReadOnlyList<FightGridPoint> body = summon.BodyOffsets == null
                || summon.BodyOffsets.Count == 0
                ? new[] { new FightGridPoint(0, 0) } : summon.BodyOffsets;
            foreach (FightGridPoint offset in body)
            {
                var point = new FightGridPoint(summon.X + offset.X, summon.Y + offset.Y);
                if (!own.Add(point) || occupied.Contains(point)) return false;
                if (checkpoint.Grid != null
                    && !checkpoint.Grid.CanStand(point, summon.MoveMask, occupied)) return false;
            }
            return true;
        }

        private void AddSummonStatuses(FightBattleUnitState summon, FightBattleUnitState actor,
            FightBattleSkillDefinition skill, FightBattleSummonPool pool,
            FightBattleHitDefinition hit)
        {
            var seen = new HashSet<int>();
            foreach (int statusId in pool.StatusIds ?? new List<int>())
                if (statusId > 0 && seen.Add(statusId)) ApplySummonStatus(summon, actor, skill, statusId);
            foreach (int statusId in hit.SummonStatusIds ?? new List<int>())
                if (statusId > 0 && seen.Add(statusId)) ApplySummonStatus(summon, actor, skill, statusId);
            if (hit.SummonLifespanStatusId > 0 && seen.Add(hit.SummonLifespanStatusId))
                ApplySummonStatus(summon, actor, skill, hit.SummonLifespanStatusId);
            RebuildBattleProperties();
        }

        private FightBattleStatusState ApplySummonStatus(FightBattleUnitState summon, FightBattleUnitState actor,
            FightBattleSkillDefinition skill, int statusId)
        {
            if (statusId <= 0) return null;
            FightBattleStatusState template = statusCatalog.CreateStatus(statusId, summon.Id,
                actor.Id, NextStatusInstanceId(), skill.EntityId, skill.ItemId, skill.Rank,
                skill.Level, skill.SubRank, actor.Properties, 0, null, skill.BlessPropertyScales);
            template.Origin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy();
            FightBattleStatusState applied = FightStatusLifecycle.Apply(summon, template,
                StatusClockRound(template), template.InstanceId, random);
            FightStatusLifecycle.Initialize(applied, StatusClockRound(template));
            return applied;
        }

        private int StatusClockRound(FightBattleStatusState status)
        {
            int id = FightStatusLifecycle.ResolveTargetUnitId(status, StatusOwners(true));
            return unitById.TryGetValue(id, out var clock) ? clock.SkillState.Round : 0;
        }

        private int NextSummonUnitId()
        {
            int maximum = checkpoint.SummonSerial;
            foreach (FightBattleUnitState unit in unitById.Values)
                if (unit != null && unit.Id > maximum) maximum = unit.Id;
            if (maximum == int.MaxValue) throw new InvalidOperationException("Summoned unit id space is exhausted.");
            checkpoint.SummonSerial = maximum + 1;
            return checkpoint.SummonSerial;
        }

        private int NextStatusInstanceId()
        {
            int maximum = checkpoint.StatusInstanceSerial;
            foreach (FightBattleUnitState unit in unitById.Values)
                if (unit != null && unit.Statuses != null)
                    foreach (FightBattleStatusState status in unit.Statuses)
                        if (status != null && status.InstanceId > maximum) maximum = status.InstanceId;
            if (maximum == int.MaxValue) throw new InvalidOperationException("Status instance id space is exhausted.");
            checkpoint.StatusInstanceSerial = maximum + 1;
            return checkpoint.StatusInstanceSerial;
        }

        private static long Property(IReadOnlyDictionary<int, long> properties, int propertyId)
        {
            return propertyId > 0 && properties != null
                && properties.TryGetValue(propertyId, out long value) ? value : 0L;
        }

        private static Dictionary<int, long> CopyProperties(IReadOnlyDictionary<int, long> source)
        {
            var result = new Dictionary<int, long>();
            if (source == null) return result;
            foreach (KeyValuePair<int, long> property in source)
                result[property.Key] = property.Value;
            return result;
        }

        private FightBattleDamageEvent ApplySkillCost(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
        {
            if (skill == null || actor == null) return null;
            if (skill.CostType == 1 && skill.SkillCost > 0)
            {
                if (checkpoint.DamageTest != null && checkpoint.DamageTest.UnlimitedHp(checkpoint.StageType, actor)) return null;
                long beforeHp = actor.Hp;
                actor.Hp = Math.Min(actor.MaxHp, Math.Max(0L, actor.Hp - skill.SkillCost));
                if (actor.Hp == beforeHp) return null;
                return new FightBattleDamageEvent
                {
                    SourceId = actor.Id,
                    TargetId = actor.Id,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId,
                    IsSkillCost = true,
                    Damage = actor.Hp - beforeHp,
                    BeforeHp = beforeHp,
                    AfterHp = actor.Hp,
                    MaxHp = actor.MaxHp
                };
            }
            else if (skill.CostType == 2)
            {
                skill.SkillCost = FightBattleShieldRuntime.ConsumeAllPersistentShields(actor);
                RebuildBattleProperties();
            }
            return null;
        }

        /// <summary>
        /// Applies official FightSKillMoveCfg self displacement. Only the direction/target cases that
        /// can be resolved without the move-behaviour entity and subclass move conditions are applied:
        /// Distance -1 pulls the caster next to the target, and Distance >= 0 with
        /// RelativeToTarget moves that many cells toward/away from the target.
        /// </summary>
        private void ApplySourceMoves(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> targets, List<FightBattleMovementEvent> movements,
            ref float lastDelay, float until, HashSet<FightBattleSkillMove> completed,
            FightBattleUnitState targetPosition)
        {
            if (skill.SourceMoves == null || skill.SourceMoves.Count == 0) return;
            if (movements == null) return;
            foreach (FightBattleSkillMove move in skill.SourceMoves)
            {
                if (actor.Hp <= 0) break;
                if (move == null) continue;
                if (move.Delay > until || !completed.Add(move)) continue;
                if (statusCatalog != null && !statusCatalog.CanStartEntityAction(actor,
                    move.MoveBehaviourId)) continue;
                FightBattleUnitState target = null;
                foreach (FightBattleUnitState candidate in targets)
                    if (candidate != null && candidate.Id != actor.Id) { target = candidate; break; }
                if (target == null || !MoveConditionsSatisfied(move, actor, target)) continue;
                if (!MoveChanceSucceeded(move, actor, target)) continue;
                if (move.Delay > lastDelay) lastDelay = move.Delay;
                if (move.Distance < 0)
                {
                    int movementIndex = movements.Count;
                    if (move.AiMustMoveTo && !move.AIIgnore && !move.CanExpandOffset
                        && move.Direction == 1 && move.TowardsTargetPos && statusCatalog != null)
                    {
                        if (!TryRequiredSourceLanding(actor, target, skill, move, out var landing, targetPosition)) continue;
                        var start = new FightGridPoint(actor.X, actor.Y);
                        if (landing.Equals(start)) continue;
                        if (skill.MoveByTeleport)
                        {
                            var movement = new FightBattleMovementEvent { UnitId = actor.Id,
                                HasDirection = true, Direction = actor.Direction };
                            movement.Path.Add(start); movement.Path.Add(landing);
                            actor.X = landing.X; actor.Y = landing.Y;
                            movements.Add(movement);
                        }
                        else TryMoveActor(actor, landing, 64, movements);
                    }
                    else MoveNextToTarget(actor, target, move, movements);
                    ApplyMovementAction(movements, movementIndex, move, skill);
                    continue;
                }
                if (move.Direction == 2)
                {
                    ApplyPathSideMove(actor, actor, target, move, movements, skill);
                    continue;
                }
                if (move.Direction != 1) continue; // RelativeToSource is not modelled
                int stepX = Math.Sign(target.X - actor.X);
                int stepY = Math.Sign(target.Y - actor.Y);
                if (!move.TowardsTargetPos) { stepX = -stepX; stepY = -stepY; }
                if (move.CancelDiagonally && stepX != 0 && stepY != 0)
                {
                    if (Math.Abs(target.X - actor.X) >= Math.Abs(target.Y - actor.Y)) stepY = 0;
                    else stepX = 0;
                }
                for (int distance = move.Distance; distance > 0; distance--)
                {
                    var desired = new FightGridPoint(actor.X + stepX * distance + move.OffsetX,
                        actor.Y + stepY * distance + move.OffsetY);
                    int movementIndex = movements.Count;
                    if (!TryMoveActor(actor, desired, distance, movements)) continue;
                    ApplyMovementAction(movements, movementIndex, move, skill);
                    break;
                }
            }
        }

        private bool MoveChanceSucceeded(FightBattleSkillMove move,
            FightBattleUnitState source, FightBattleUnitState target)
        {
            double chance = move.BasePercent;
            if (move.AffectedByProp)
                chance = formula.CalculateStatusEffectRate(source.Properties, target.Properties,
                    move.BasePercent, 0);
            return random.Next() <= chance;
        }

        private static bool MoveConditionsSatisfied(FightBattleSkillMove move,
            FightBattleUnitState actor, FightBattleUnitState target)
        {
            if (move == null || actor == null || target == null) return false;
            if (move.Conditions == null || move.Conditions.Count == 0) return !move.HasConditions;
            int targetDistance = Manhattan(new FightGridPoint(actor.X, actor.Y),
                new FightGridPoint(target.X, target.Y));
            foreach (FightBattleSkillMoveCondition condition in move.Conditions)
            {
                if (condition == null || condition.Type != "SkillTargetDistanceMoveCondition"
                    || targetDistance > condition.Distance) return false;
            }
            return true;
        }

        private void MoveNextToTarget(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillMove move, List<FightBattleMovementEvent> movements)
        {
            var candidates = new List<FightGridPoint>
            {
                new FightGridPoint(target.X + move.OffsetX, target.Y + move.OffsetY - 1),
                new FightGridPoint(target.X + move.OffsetX - 1, target.Y + move.OffsetY),
                new FightGridPoint(target.X + move.OffsetX + 1, target.Y + move.OffsetY),
                new FightGridPoint(target.X + move.OffsetX, target.Y + move.OffsetY + 1)
            };
            candidates.Sort((left, right) =>
                (Math.Abs(left.X - actor.X) + Math.Abs(left.Y - actor.Y))
                .CompareTo(Math.Abs(right.X - actor.X) + Math.Abs(right.Y - actor.Y)));
            HashSet<FightGridPoint> occupied = OccupiedPositions(actor.Id);
            FightBattleGrid grid = checkpoint.Grid;
            if (grid == null) return;
            foreach (FightGridPoint candidate in candidates)
            {
                if (!grid.CanStand(candidate, actor.MoveMask, occupied)) continue;
                if (!grid.Contains(candidate)) continue;
                if (TryMoveActor(actor, candidate, 64, movements)) return;
            }
        }

        private bool TryMoveActor(FightBattleUnitState actor, FightGridPoint desired, int maxStep,
            List<FightBattleMovementEvent> movements)
        {
            return TryMoveUnit(actor, desired, maxStep, movements);
        }

        private bool TryMoveUnit(FightBattleUnitState actor, FightGridPoint desired, int maxStep,
            List<FightBattleMovementEvent> movements)
        {
            FightBattleGrid grid = checkpoint.Grid;
            if (grid == null || maxStep <= 0) return false;
            HashSet<FightGridPoint> occupied = OccupiedPositions(actor.Id);
            var start = new FightGridPoint(actor.X, actor.Y);
            List<FightGridPoint> path = grid.FindPath(start, desired, actor.MoveMask,
                maxStep + 1, 0, true, actor.BodyOffsets, occupied);
            if (path == null || path.Count <= 1) return false;
            int steps = Math.Min(maxStep, path.Count - 1);
            var movement = new FightBattleMovementEvent { UnitId = actor.Id,
                                HasDirection = true, Direction = actor.Direction };
            movement.Path.Add(start);
            for (int i = 1; i <= steps; i++)
            {
                FightGridPoint previous = path[i - 1];
                FightGridPoint next = path[i];
                UpdateMovementDirection(actor, previous, next);
                actor.X = next.X;
                actor.Y = next.Y;
                movement.Path.Add(next);
            }
            movements.Add(movement);
            return true;
        }

        /// <summary>
        /// Official <c>FightDamageComponent.ChildSkillCfg</c> supplies the child, rate and target-HP
        /// condition. The confirmed offline rule preserves the triggering caster, cultivation and
        /// hit target as the position anchor; the child still applies its own side, scope and effects.
        /// Direct child effects keep their casts in the current step and follow their own configuration.
        /// </summary>
        private void ApplyChildSkill(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition parentSkill,
            FightBattleHitDefinition hit,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || parentSkill == null || hit == null
                || hit.ChildSkillId <= 0 || !parentSkill.CanTriggerChild) return;
            if (actor == null || target == null || target.Hp <= 0) return;
            if (hit.ChildSkillRate < 1f && random.Next() > hit.ChildSkillRate) return;
            if (hit.ChildSkillLessThanHpPer >= 0f
                && target.Hp / (double)Math.Max(1L, target.MaxHp) > hit.ChildSkillLessThanHpPer) return;
            int rank = Math.Max(1, parentSkill.Rank);
            int level = Math.Max(1, parentSkill.Level);
            // Skill property tables are keyed from the first real sub-rank. A legacy/root
            // skill may omit cultivation (0), but its child still inherits the source skill's
            // parameters and must resolve the official minimum sub-rank rather than an empty row.
            int subRank = Math.Max(1, parentSkill.SubRank);
            FightBattleSkillDefinition skill;
            try
            {
                skill = statusCatalog.CreateSkill(hit.ChildSkillId, -1, 0, rank, level, subRank,
                    ToPropertyList(statusCatalog.CalculateSkillProps(hit.ChildSkillId, rank, level, subRank)));
            }
            catch (InvalidOperationException)
            {
                return;
            }
            skill.StatusOrigin = FightBattleStatusOrigin.Resolve(parentSkill, actor.Id).Copy();
            InheritBlessingScales(skill, parentSkill.BlessPropertyScales);
            // Do not run AI selection across the roster again: each hit owns its child target.
            // Self effects select the caster, while scoped hits retain the original hit position.
            var candidates = new List<FightBattleUnitState>
            {
                skill.TargetType == 0 ? actor : target
            };
            List<FightBattleUnitState> targets = FightBattleTargeting.Select(actor, candidates,
                skill.TargetType, 0, false, false, skill, random);
            checkpoint.Statistics.RecordCast(actor.Id, skill.EntityId, skill.ItemId);
            var cast = new FightBattleSkillCastEvent
            {
                ActorId = actor.Id,
                Presentation = FightBattleSkillCastPresentation.Capture(actor, skill, targets, target),
                Slot = -1,
                SkillEntityId = skill.EntityId,
                SkillItemId = skill.ItemId
            };
            foreach (FightBattleUnitState unit in targets) cast.TargetIds.Add(unit.Id);
            casts.Add(cast);
            ExecuteSkill(actor, skill, targets, casts, damageEvents, movements,
                out float delay, target, presentationCast: cast);
            CompleteCastPresentation(cast, skill, delay);
            if (delay > lastDelay) lastDelay = delay;
        }

        private static FightSkillReplayCounter ReplayCounter(FightBattleUnitState actor, FightBattleSkillDefinition skill)
        {
            var state = actor.SkillState;
            if (state.ReplayCounts == null) state.ReplayCounts = new List<FightSkillReplayCounter>();
            state.ReplayCounts.RemoveAll(value => value.Round != state.Round);
            // Replacement entities share the original equipped slot's round count.
            int entity = skill.Slot < 0 ? skill.EntityId : 0;
            var counter = state.ReplayCounts.Find(value => value.Slot == skill.Slot && value.EntityId == entity);
            if (counter == null)
            {
                counter = new FightSkillReplayCounter { Round = state.Round, Slot = skill.Slot, EntityId = entity };
                state.ReplayCounts.Add(counter);
            }
            return counter;
        }

        private static bool CanReplay(FightBattleSkillDefinition skill, FightSkillReplayCounter counter)
            => skill.ReplayMaxCountEachRound < 0 || counter.Count < skill.ReplayMaxCountEachRound;

        /// <summary>
        /// Official <c>FightSkillExtSkillComponent</c>: when the skill action ends, roll each entry's
        /// rate and release the extra skill as the same actor. Extra skills continue by configuration and
        /// keep their casts/damage in the current step.
        /// </summary>
        private void ApplyExtraSkills(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || skill == null || skill.ExtraSkills == null
                || skill.ExtraSkills.Count == 0) return;
            int rank = Math.Max(1, skill.Rank);
            int level = Math.Max(1, skill.Level);
            int subRank = Math.Max(0, skill.SubRank);
            foreach (FightBattleSkillExtra extra in skill.ExtraSkills)
            {
                if (extra == null || extra.SkillId <= 0) continue;
                if (extra.Rate < 1f && random.Next() > extra.Rate) continue;
                FightBattleSkillDefinition extraSkill;
                try
                {
                    extraSkill = statusCatalog.CreateSkill(extra.SkillId, -1, 0, rank, level, subRank,
                        ToPropertyList(statusCatalog.CalculateSkillProps(extra.SkillId, rank, level, subRank)));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                extraSkill.StatusOrigin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy();
                InheritBlessingScales(extraSkill, skill.BlessPropertyScales);
                List<FightBattleUnitState> targets = FightBattleTargeting.Select(actor, units,
                    extraSkill.TargetType, 0, false, false, extraSkill, random);
                if (targets.Count == 0
                    && !FightBattleTargeting.HasCasterAreaTargets(actor, units, extraSkill)) continue;
                checkpoint.Statistics.RecordCast(actor.Id, extraSkill.EntityId, extraSkill.ItemId);
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = actor.Id,
                    Presentation = FightBattleSkillCastPresentation.Capture(actor, extraSkill, targets),
                    Slot = -1,
                    SkillEntityId = extraSkill.EntityId,
                    SkillItemId = extraSkill.ItemId
                };
                foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                casts.Add(cast);
                ExecuteSkill(actor, extraSkill, targets, casts, damageEvents, movements,
                    out float delay, presentationCast: cast);
                CompleteCastPresentation(cast, extraSkill, delay);
                if (delay > lastDelay) lastDelay = delay;
            }
        }

        /// <summary>
        /// Executes the skill list of an HP-triggered status as the status owner. Trigger casts are
        /// governed by the skill trigger flag and status whitelist, keep their damage
        /// and movement events in the current step, and do not consume the owner's skill slots.
        /// </summary>
        private void ExecuteTriggerSkills(FightBattleUnitState owner, List<int> skillIds,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || skillIds == null || skillIds.Count == 0) return;
            var triggers = new List<FightBattleStatusStateTrigger>();
            foreach (int skillId in skillIds)
                triggers.Add(new FightBattleStatusStateTrigger
                {
                    EntityId = skillId,
                    SourceSkillRank = 1,
                    SourceSkillLevel = Math.Max(1, owner.Level),
                    SourceSubRank = Math.Max(1, owner.SubRank)
                });
            ExecuteTriggerSkills(owner, triggers, casts, damageEvents, movements, ref lastDelay);
        }

        private void ExecuteTriggerSkills(FightBattleUnitState owner,
            List<FightBattleStatusStateTrigger> triggers,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || triggers == null || triggers.Count == 0) return;
            foreach (FightBattleStatusStateTrigger trigger in triggers)
            {
                if (trigger == null || trigger.EntityId <= 0) continue;
                FightBattleSkillDefinition skill;
                try
                {
                    int rank = Math.Max(1, trigger.SourceSkillRank);
                    int level = Math.Max(1, trigger.SourceSkillLevel);
                    int subRank = Math.Max(0, trigger.SourceSubRank);
                    skill = statusCatalog.CreateSkill(trigger.EntityId, -1, 0, rank, level, subRank,
                        ToPropertyList(statusCatalog.CalculateSkillProps(trigger.EntityId,
                            rank, level, subRank)));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                skill.StatusOrigin = trigger.Origin?.Copy();
                InheritBlessingScales(skill, trigger.BlessPropertyScales);
                List<FightBattleUnitState> targets = FightBattleTargeting.Select(owner, units,
                    skill.TargetType, 0, false, false, skill, random);
                if (targets.Count == 0
                    && !FightBattleTargeting.HasCasterAreaTargets(owner, units, skill)) continue;
                checkpoint.Statistics.RecordCast(owner.Id, skill.EntityId, skill.ItemId);
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = owner.Id,
                    Presentation = FightBattleSkillCastPresentation.Capture(owner, skill, targets),
                    Slot = -1,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId
                };
                foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                casts.Add(cast);
                ExecuteSkill(owner, skill, targets, casts, damageEvents, movements, out float delay, presentationCast: cast);
                CompleteCastPresentation(cast, skill, delay);
                if (delay > lastDelay) lastDelay = delay;
            }
        }

        private void ExecuteConfiguredTriggerSkills(FightBattleUnitState owner,
            IList<FightBattleStatusStateTrigger> triggers,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay,
            out float maxDuration, out float maxActionDuration)
        {
            maxDuration = 0f;
            maxActionDuration = -1f;
            if (statusCatalog == null || triggers == null || triggers.Count == 0)
                return;
            int level = Math.Max(1, owner.Level);
            int subRank = Math.Max(1, owner.SubRank);
            foreach (FightBattleStatusStateTrigger trigger in triggers)
            {
                if (trigger == null || trigger.EntityId <= 0) continue;
                FightBattleSkillDefinition skill;
                try
                {
                    int rank = Math.Max(1, trigger.SourceSkillRank);
                    int skillLevel = trigger.SourceSkillLevel > 0 ? trigger.SourceSkillLevel : level;
                    int skillSubRank = trigger.SourceSubRank > 0 ? trigger.SourceSubRank : subRank;
                    skill = statusCatalog.CreateSkill(trigger.EntityId, -1, trigger.SourceSkillItemId,
                        rank, skillLevel, skillSubRank,
                        ToPropertyList(statusCatalog.CalculateSkillProps(trigger.EntityId, rank,
                            skillLevel, skillSubRank)));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                skill.StatusOrigin = trigger.Origin?.Copy();
                InheritBlessingScales(skill, trigger.BlessPropertyScales);

                FightBattleUnitState targetPosition = null;
                if (trigger.TargetPos == 1)
                    targetPosition = owner;
                else if (trigger.TargetPos != 0)
                    continue;
                List<FightBattleUnitState> targets = FightBattleTargeting.Select(owner, units,
                    skill.TargetType, 0, false, false, skill, random);
                if (targets.Count == 0 && targetPosition == null
                    && !FightBattleTargeting.HasCasterAreaTargets(owner, units, skill)) continue;

                checkpoint.Statistics.RecordCast(owner.Id, skill.EntityId, skill.ItemId);
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = owner.Id,
                    Presentation = FightBattleSkillCastPresentation.Capture(owner, skill, targets, targetPosition),
                    Slot = -1,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId
                };
                foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                casts.Add(cast);
                ExecuteSkill(owner, skill, targets, casts, damageEvents, movements,
                    out float delay, targetPosition, presentationCast: cast);
                CompleteCastPresentation(cast, skill, delay);
                if (delay > lastDelay) lastDelay = delay;
                maxDuration = Math.Max(maxDuration, ResolveDuration(skill, delay));
                float actionDuration = ResolveActionDuration(skill, delay);
                if (actionDuration >= 0f)
                    maxActionDuration = Math.Max(maxActionDuration, actionDuration);
            }
        }

        private void ExecuteConfiguredStatusSkills(
            IList<FightBattleStatusSkillInvocation> invocations,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay,
            out float maxDuration, out float maxActionDuration)
        {
            maxDuration = 0f;
            maxActionDuration = -1f;
            if (statusCatalog == null || invocations == null || invocations.Count == 0) return;
            float sequentialEnd = lastDelay;
            foreach (FightBattleStatusSkillInvocation invocation in invocations)
            {
                if (invocation == null || invocation.Trigger == null
                    || !unitById.TryGetValue(invocation.SourceUnitId,
                        out FightBattleUnitState source)
                    || !unitById.TryGetValue(invocation.TargetUnitId,
                        out FightBattleUnitState target)
                    || !unitById.TryGetValue(invocation.TargetPositionUnitId,
                        out FightBattleUnitState targetPosition)
                    || (!FightBattleTargeting.IsSelectable(source)
                        && !invocation.AllowDeadSource && !IsGridSkillSource(source))
                    || (!invocation.SelectTargets && !FightBattleTargeting.IsSelectable(target)
                        && !invocation.AllowDeadTarget)
                    || (!invocation.SelectTargets && !FightBattleTargeting.IsSelectable(targetPosition)
                        && !invocation.AllowDeadTarget
                        && !(invocation.AllowDeadSource && targetPosition == source))) continue;
                FightBattleSkillDefinition skill;
                try
                {
                    FightBattleStatusStateTrigger trigger = invocation.Trigger;
                    int rank = Math.Max(1, trigger.SourceSkillRank);
                    int level = trigger.SourceSkillLevel > 0 ? trigger.SourceSkillLevel : Math.Max(1, source.Level);
                    int subRank = trigger.SourceSubRank > 0 ? trigger.SourceSubRank : Math.Max(1, source.SubRank);
                    skill = statusCatalog.CreateSkill(trigger.EntityId, -1, trigger.SourceSkillItemId,
                        rank, level, subRank, ToPropertyList(statusCatalog.CalculateSkillProps(
                            trigger.EntityId, rank, level, subRank)));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                skill.StatusOrigin = invocation.Trigger.Origin?.Copy();
                if (invocation.SourceRootSkillId > 0) skill.RootEntityId = invocation.SourceRootSkillId;
                InheritBlessingScales(skill, invocation.Trigger.BlessPropertyScales);
                // SkillTargetType.Pos/PosCanStand/PosSkillDirectionCanStand use
                // this unit as a position reference, not as the affected entity.
                bool entityTarget = skill.SkillTargetType == 1 || skill.SkillTargetType == 4
                    || skill.SkillTargetType == 5;
                if (!invocation.SelectTargets && entityTarget
                    && (!invocation.AllowDeadTarget && !IsExplicitTargetCompatible(source, target, skill.TargetType)
                        || FightBattleTargeting.IsActionBlockedByStatus(target, skill.BlockingTargetActionTypes))) continue;

                var targets = invocation.SelectTargets
                    ? FightBattleTargeting.Select(source, units, skill.TargetType, 0, false, false, skill, random)
                    : new List<FightBattleUnitState> { target };
                if (targets.Count == 0 && skill.TargetType != 5
                    && !FightBattleTargeting.HasCasterAreaTargets(source, units, skill)) continue;
                checkpoint.Statistics.RecordCast(source.Id, skill.EntityId, skill.ItemId);
                int castStart = casts.Count;
                int damageStart = damageEvents.Count;
                int movementStart = movements.Count;
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = source.Id,
                    Presentation = FightBattleSkillCastPresentation.Capture(source, skill, targets,
                        !invocation.SelectTargets || invocation.UseTargetPositionWithSelection
                            ? targetPosition : null),
                    Slot = -1,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId
                };
                foreach (var selected in targets) cast.TargetIds.Add(selected.Id);
                casts.Add(cast);
                ExecuteSkill(source, skill, targets, casts, damageEvents, movements,
                    out float delay, out float nestedDuration, out float nestedActionDuration,
                    !invocation.SelectTargets || invocation.UseTargetPositionWithSelection
                        ? targetPosition : null, invocation.AllowDeadSource || IsGridSkillSource(source),
                    invocation.DamageFactor, presentationCast: cast);
                CompleteCastPresentation(cast, skill, delay);
                // FightStatusMoveNearComponentInfo.SkillTriggerDelayTime delays the entire skill,
                // including its nested triggers. Keep each event relative until execution returns.
                float offset = Math.Max(0f, invocation.Delay);
                if (invocation.Sequential) offset = Math.Max(offset, sequentialEnd);
                OffsetSkillEvents(casts, castStart, damageEvents, damageStart, movements,
                    movementStart, offset);
                lastDelay = Math.Max(lastDelay, offset + delay);
                maxDuration = Math.Max(maxDuration, offset + Math.Max(
                    ResolveDuration(skill, delay), nestedDuration));
                float actionDuration = Math.Max(ResolveActionDuration(skill, delay), nestedActionDuration);
                if (invocation.Sequential) sequentialEnd = offset + Math.Max(0f, actionDuration);
                if (actionDuration >= 0f)
                    maxActionDuration = Math.Max(maxActionDuration, offset + actionDuration);
            }
        }

        private static void OffsetSkillEvents(List<FightBattleSkillCastEvent> casts, int castStart,
            List<FightBattleDamageEvent> damageEvents, int damageStart,
            List<FightBattleMovementEvent> movements, int movementStart, float offset)
        {
            if (offset <= 0f) return;
            for (int index = castStart; index < casts.Count; index++) casts[index].Delay += offset;
            for (int index = damageStart; index < damageEvents.Count; index++) damageEvents[index].Delay += offset;
            for (int index = movementStart; index < movements.Count; index++) movements[index].Delay += offset;
        }

        private void ExecuteMoveNearTriggers(FightBattleUnitState roundActor, int eventKind,
            FightBattleMovementEvent movement, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            ref float stepDuration, ref float stepActionDuration)
        {
            if (statusCatalog == null) return;
            List<FightBattleStatusSkillInvocation> invocations =
                FightStatusTriggerRuntime.CollectMoveNearSkills(roundActor, StatusOwners(), eventKind,
                    movement, random);
            if (invocations.Count == 0) return;
            float triggerDelay = 0f;
            ExecuteConfiguredStatusSkills(invocations, casts, damageEvents, movements,
                ref triggerDelay, out float triggerDuration, out float triggerActionDuration);
            stepDuration = Math.Max(stepDuration, Math.Max(triggerDuration, triggerDelay));
            if (triggerActionDuration >= 0f)
                stepActionDuration = Math.Max(stepActionDuration, triggerActionDuration);
        }

        private void ExecutePendingCooldownModifySkills(
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float stepDuration,
            ref float stepActionDuration)
        {
            if (statusCatalog == null) return;
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.Statuses != null)
                    foreach (FightBattleStatusState status in unit.Statuses)
                        FightStatusCooldownRuntime.ProcessPending(unit, status, random);
            List<FightBattleStatusSkillInvocation> invocations =
                FightStatusTriggerRuntime.CollectCooldownModifySkills(units, random);
            if (invocations.Count == 0) return;
            float triggerDelay = 0f;
            ExecuteConfiguredStatusSkills(invocations, casts, damageEvents, movements,
                ref triggerDelay, out float triggerDuration, out float triggerActionDuration);
            stepDuration = Math.Max(stepDuration, triggerDuration);
            if (triggerActionDuration >= 0f)
                stepActionDuration = Math.Max(stepActionDuration, triggerActionDuration);
        }

        private void CollectStatusCustomActions(
            List<FightBattleStatusCustomActionEvent> customActions)
        {
            if (customActions == null) return;
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null || unit.Statuses == null) continue;
                foreach (FightBattleStatusState status in unit.Statuses)
                {
                    if (status == null || status.SkillCooldownActionPendingCount <= 0) continue;
                    for (int pending = 0; pending < status.SkillCooldownActionPendingCount; pending++)
                        customActions.Add(new FightBattleStatusCustomActionEvent
                        {
                            UnitId = unit.Id,
                            Action = 3
                        });
                    status.SkillCooldownActionPendingCount = 0;
                }
            }
        }

        private void CollectUnflinchingActions(FightBattleUnitState target, long actualHpLoss,
            FightBattleSkillDefinition sourceSkill = null)
        {
            if (target == null || actualHpLoss <= 0 || target.Statuses == null
                || currentStepStatusCustomActions == null) return;
            // The server invocation timing is unavailable; offline uses one configured-rate check per actual HP loss.
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (!FightSkillTriggerPolicy.AllowsStatus(sourceSkill, status)
                    || !status.HasUnflinchingOnDamageComponent
                    || random.Next() > status.UnflinchingOnDamageRate) continue;
                currentStepStatusCustomActions.Add(new FightBattleStatusCustomActionEvent
                {
                    UnitId = target.Id,
                    Action = 2
                });
            }
        }

        private static bool IsExplicitTargetCompatible(FightBattleUnitState source,
            FightBattleUnitState target, int targetType)
        {
            bool sameSide = source.Side == target.Side;
            switch (targetType)
            {
                case 0: return target.Id == source.Id;
                case 1: return !sameSide;
                case 2: return sameSide;
                case 3: return sameSide && target.Id != source.Id;
                case 4: return true;
                case 6: return target.Id == source.MasterId;
                case 7: return target.MasterId == source.Id;
                case 8: return sameSide && target.Id != source.Id && target.CharacterType == 1;
                case 9: return sameSide && (target.CharacterType == 1 || target.CharacterType == 4);
                default: return false;
            }
        }

        private static List<FightPropertyValue> ToPropertyList(IReadOnlyDictionary<int, long> values)
        {
            var result = new List<FightPropertyValue>();
            if (values == null) return result;
            foreach (KeyValuePair<int, long> entry in values)
                result.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
            return result;
        }

        /// <summary>
        /// Official <c>FightHitExtHemophagiaComponent</c>: heals the skill source by the configured
        /// lifesteal property. The property is treated as a 10000-based percent (the pinned prop
        /// table does not mark it, so the scale is an explicit local assumption) and the heal is
        /// recorded as a cure event so statistics and replay stay consistent.
        /// </summary>
        private long ApplyHemophagia(FightBattleUnitState actor, FightBattleHitDefinition hit,
            FightBattleDamageEvent applied, float delay, FightBattleSkillDefinition skill,
            List<FightBattleDamageEvent> events)
        {
            if (hit.HemophagiaProperty <= 0 || applied == null || applied.IsCure) return 0L;
            long dealt = Math.Abs(applied.Damage);
            if (dealt <= 0) return 0L;
            long property = GetProperty(actor, hit.HemophagiaProperty);
            if (property <= 0) return 0L;
            double rate = property / 10000.0;
            if (rate > 1.0) rate = 1.0;
            long heal = (long)Math.Round(dealt * rate);
            if (heal <= 0) return 0L;
            long before = actor.Hp;
            actor.Hp = Math.Min(actor.MaxHp, actor.Hp + heal);
            long actual = actor.Hp - before;
            if (actual <= 0) return 0L;
            var cure = new FightBattleDamageEvent
            {
                DamageId = applied.DamageId,
                SourceId = actor.Id,
                TargetId = actor.Id,
                SkillEntityId = skill == null ? 0 : skill.EntityId,
                SkillItemId = skill == null ? 0 : skill.ItemId,
                HitEntityId = hit.EntityId,
                DamageType = 1,
                ElementType = skill == null ? 0 : skill.ElementType,
                Delay = delay,
                Anchor = hit.Anchor,
                IsCure = true,
                Damage = actual,
                BeforeHp = before,
                AfterHp = actor.Hp,
                MaxHp = actor.MaxHp
            };
            events.Add(cure);
            checkpoint.Statistics.RecordHit(cure);
            return actual;
        }

        /// <summary>
        /// Official <c>FightDamageComponent.MoveCfg</c>: pushes or pulls the damaged unit along the
        /// source-to-target vector by the configured distance, falling back to shorter distances
        /// when the destination is blocked. The default (-1) means no displacement.
        /// </summary>
        private void ApplyHitMove(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            List<FightBattleMovementEvent> movements)
        {
            FightBattleSkillMove move = hit == null ? null : hit.HitMove;
            if (move == null || !MoveConditionsSatisfied(move, actor, target)
                || move.Distance <= 0 || movements == null
                || target.Hp <= 0) return;
            if (statusCatalog != null && !statusCatalog.CanStartEntityAction(target,
                move.MoveBehaviourId)) return;
            if (!MoveChanceSucceeded(move, actor, target)) return;
            if (move.Direction == 2)
            {
                ApplyPathSideMove(target, actor, target, move, movements, skill);
                return;
            }
            if (move.Direction != 0 && move.Direction != 1) return;
            int stepX = Math.Sign(target.X - actor.X);
            int stepY = Math.Sign(target.Y - actor.Y);
            if (move.TowardsTargetPos) { stepX = -stepX; stepY = -stepY; }
            if (move.CancelDiagonally && stepX != 0 && stepY != 0)
            {
                if (Math.Abs(target.X - actor.X) >= Math.Abs(target.Y - actor.Y)) stepY = 0;
                else stepX = 0;
            }
            if (stepX == 0 && stepY == 0) return;
            for (int distance = move.Distance; distance > 0; distance--)
            {
                var desired = new FightGridPoint(target.X + stepX * distance + move.OffsetX,
                    target.Y + stepY * distance + move.OffsetY);
                int movementIndex = movements.Count;
                if (!TryMoveUnit(target, desired, distance, movements)) continue;
                ApplyMovementAction(movements, movementIndex, move, skill);
                break;
            }
        }

        private bool ApplyPathSideMove(FightBattleUnitState mover, FightBattleUnitState source,
            FightBattleUnitState target, FightBattleSkillMove move,
            List<FightBattleMovementEvent> movements, FightBattleSkillDefinition skill)
        {
            if (mover == null || source == null || target == null || move == null
                || move.Distance <= 0 || movements == null) return false;

            int deltaX = target.X - source.X;
            int deltaY = target.Y - source.Y;
            if (deltaX == 0 && deltaY == 0) return false;

            // Keep the first side stable across runs: positive grid axis, then its opposite.
            bool horizontalPath = Math.Abs(deltaX) >= Math.Abs(deltaY);
            int firstSideX = horizontalPath ? 0 : 1;
            int firstSideY = horizontalPath ? 1 : 0;
            for (int side = 0; side < 2; side++)
            {
                int sideX = side == 0 ? firstSideX : -firstSideX;
                int sideY = side == 0 ? firstSideY : -firstSideY;
                for (int distance = move.Distance; distance > 0; distance--)
                {
                    var desired = new FightGridPoint(mover.X + sideX * distance + move.OffsetX,
                        mover.Y + sideY * distance + move.OffsetY);
                    int movementIndex = movements.Count;
                    if (!TryMoveUnit(mover, desired, distance, movements)) continue;
                    ApplyMovementAction(movements, movementIndex, move, skill);
                    return true;
                }
            }
            return false;
        }

        private void ApplyMovementAction(List<FightBattleMovementEvent> movements,
            int startIndex, FightBattleSkillMove move, FightBattleSkillDefinition skill)
        {
            if (movements == null || move == null) return;
            // A start listener can append another movement. Do not overwrite its own action timing
            // or notify it twice while completing the outer action.
            int endIndex = movements.Count;
            for (int index = startIndex; index < endIndex; index++)
            {
                FightBattleMovementEvent movement = movements[index];
                if (movement == null) continue;
                movement.Delay = move.Delay;
                movement.MoveBehaviourId = move.MoveBehaviourId;
                movement.MoveDuration = move.MoveDuration;
                movement.MoveSpeed = move.MoveSpeed;
                NotifyMovementStart(movement, statusCatalog?.EntityActionType(move.MoveBehaviourId) ?? 0,
                    movements, skill);
            }
        }

        private void NotifyMovementStart(FightBattleMovementEvent movement, int actionType,
            List<FightBattleMovementEvent> movements, FightBattleSkillDefinition sourceSkill = null)
        {
            if (statusCatalog == null || movement == null
                || !unitById.TryGetValue(movement.UnitId, out FightBattleUnitState mover)) return;
            statusCatalog.ApplyMovementFacing(mover, movement);
            statusCatalog.CompleteRoleMovement(movement);
            if (movementStartContext == null) return;
            // ActionAgentComponentImplement.StartAction fires StartEvent after AddEntity;
            // FightActionSkillMoveComponent.StartImpl has already committed the path at that point.
            List<FightBattleStatusSkillInvocation> invocations =
                FightStatusTriggerRuntime.CollectActionStartSkills(mover, actionType,
                    units, random, sourceSkill);
            if (invocations.Count == 0) return;
            int castStart = movementStartContext.Casts.Count;
            int damageStart = movementStartContext.DamageEvents.Count;
            int movementStart = movements.Count;
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, movementStartContext.Casts,
                movementStartContext.DamageEvents, movements, ref delay,
                out float duration, out float actionDuration);
            // Nested results are relative to the movement action that started them.
            float offset = Math.Max(0f, movement.Delay);
            OffsetSkillEvents(movementStartContext.Casts, castStart,
                movementStartContext.DamageEvents, damageStart, movements, movementStart, offset);
            movementStartContext.Duration = Math.Max(movementStartContext.Duration,
                offset + Math.Max(duration, delay));
            if (actionDuration >= 0f)
                movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration,
                    offset + actionDuration);
        }

        /// <summary>
        /// Official <c>DisperseStatus</c>: removes dispersible statuses of the configured types from
        /// the damaged target, either the first ones or a random subset.
        /// </summary>
        private void ApplyHitDisperse(FightBattleUnitState source, FightBattleUnitState target,
            FightBattleHitDefinition hit, FightRandomStream random)
        {
            if (source == null || target == null || hit == null || hit.DisperseStatusTypes == null
                || hit.DisperseStatusTypes.Count == 0 || target.Statuses.Count == 0) return;
            double chance = hit.DisperseChance;
            if (hit.DisperseAffectedByProp)
            {
                chance = formula.CalculateStatusEffectRate(source.Properties, target.Properties,
                    hit.DisperseChance, 0);
                if (double.IsNaN(chance)) return;
            }
            if (chance < 1.0 && random.Next() > chance) return;
            var candidates = new List<FightBattleStatusState>();
            foreach (FightBattleStatusState status in target.Statuses)
                if (status != null && status.CanDispersed
                    && hit.DisperseStatusTypes.Contains(status.StatusType)) candidates.Add(status);
            if (candidates.Count == 0) return;
            int count = hit.DisperseCount <= 0 ? candidates.Count : Math.Min(hit.DisperseCount, candidates.Count);
            if (count < candidates.Count && hit.DisperseRandom)
            {
                for (int i = 0; i < count; i++)
                {
                    int index = Math.Min((int)(random.Next() * candidates.Count), candidates.Count - 1);
                    candidates[i] = candidates[index];
                    candidates[index] = candidates[candidates.Count - 1];
                    candidates.RemoveAt(candidates.Count - 1);
                }
            }
            for (int i = 0; i < count; i++) FightStatusLifecycle.End(target, candidates[i]);
            // Confirmed local notification rule: successful removal only, once for this operation.
            if (count > 0) currentStepDispersedUnitIds?.Add(target.Id);
        }

        /// <summary>
        /// Applies the official damage status list: each entry picks the skill source or the damage
        /// target, checks the remaining-HP condition and probability, then adds the status through
        /// the shared lifecycle (duration starts at the owner's next round update).
        /// </summary>
        private void ApplyHitStatuses(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit, FightRandomStream random,
            long skillCostHp, long actualHpLoss)
        {
            if (statusCatalog == null || hit.AppliedStatuses == null) return;
            if (hit.StatusListRandomOne && hit.AppliedStatuses.Count > 1)
            {
                int index = Math.Min((int)(random.Next() * hit.AppliedStatuses.Count),
                    hit.AppliedStatuses.Count - 1);
                ApplyHitStatus(actor, target, skill, hit.AppliedStatuses[index], random,
                    skillCostHp, actualHpLoss);
                return;
            }
            foreach (FightBattleStatusApplication application in hit.AppliedStatuses)
                ApplyHitStatus(actor, target, skill, application, random, skillCostHp,
                    actualHpLoss);
        }

        private void RebuildBattleProperties()
        {
            if (statusCatalog != null && !applyingActionExistStatuses)
            {
                // A newly started status may cast synchronously while its action-existence
                // producer is still recording the new instance. Do not enter that producer twice.
                applyingActionExistStatuses = true;
                try { FightStatusTriggerRuntime.ApplyActionExistStatuses(units, statusCatalog, random); }
                finally { applyingActionExistStatuses = false; }
            }
            foreach (FightBattleUnitState unit in units) statusMutex.Synchronize(unit);
            foreach (FightBattleUnitState unit in units)
                foreach (FightBattleStatusState status in unit.Statuses)
                    if (status != null && status.StartRound < 0)
                        FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            FinishVoodooDolls();
            if (statusCatalog != null)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            checkpoint.Statistics.RecordPendingShieldGrants(units);
            checkpoint.Statistics.RecordPendingStatusApplications(units);
        }

        private void ApplyHitStatus(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleStatusApplication application,
            FightRandomStream random, long skillCostHp, long actualHpLoss)
        {
            if (application == null || application.StatusId <= 0) return;
            FightBattleUnitState owner = application.TargetType == 1 ? actor : target;
            if (owner == null || (owner.Hp <= 0
                && !statusCatalog.HasBackToLifeComponent(application.StatusId))) return;
            if (application.LessThanHpPer >= 0f
                && owner.Hp / (double)Math.Max(1L, owner.MaxHp) >= application.LessThanHpPer) return;
            double chance = application.Chance;
            bool affectedByProp = application.AffectedByProp;
            if (application.CharacterTypeRateOverrides != null)
                foreach (FightBattleStatusRateOverride rateOverride in application.CharacterTypeRateOverrides)
                    if (rateOverride != null && rateOverride.CharacterType == owner.CharacterType)
                    {
                        chance = rateOverride.Rate;
                        affectedByProp = rateOverride.AffectedByProp;
                        break;
                    }
            chance = statusCatalog.CalculateStatusApplicationRate(actor, owner,
                application.StatusId, chance, affectedByProp, formula);
            if (!(random.Next() <= chance)) return;
            int instanceId = 1;
            foreach (FightBattleStatusState existing in owner.Statuses)
                if (existing != null && existing.InstanceId >= instanceId)
                    instanceId = existing.InstanceId + 1;
            long customStatusValue = ResolveStatusCustomValue(application, skillCostHp,
                actualHpLoss);
            FightBattleStatusState template = statusCatalog.CreateStatus(application.StatusId,
                owner.Id, actor.Id, instanceId, skill == null ? 0
                    : skill.RootEntityId > 0 ? skill.RootEntityId : skill.EntityId,
                skill == null ? 0 : skill.ItemId, skill == null ? 1 : skill.Rank,
                skill == null ? 1 : skill.Level, skill == null ? actor.SubRank : skill.SubRank,
                actor.Properties, customStatusValue, null, skill?.BlessPropertyScales);
            template.Origin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy();
            FightStatusLifecycle.Apply(owner, template, -1, instanceId, random);
        }

        private static void InheritBlessingScales(FightBattleSkillDefinition skill,
            IEnumerable<FightStatusPropertyScale> scales)
        {
            if (skill == null || scales == null) return;
            skill.BlessPropertyScales = FightStatusPropertyModifiers.Copy(scales);
            FightStatusPropertyModifiers.ApplyBlessingScales(skill.EntityId, skill.Properties,
                skill.BlessPropertyScales);
        }

        private static long ResolveStatusCustomValue(FightBattleStatusApplication application,
            long skillCostHp, long actualHpLoss)
        {
            if (application.SkillCostHp) return Math.Max(0L, skillCostHp);
            if (application.DamageCostHpPer < 0f)
                return Math.Max(0L, application.CustomShieldValue);
            double rate = Math.Max(0.0, Math.Min(1.0, application.DamageCostHpPer));
            double value = Math.Max(0L, actualHpLoss) * rate;
            return value >= long.MaxValue ? long.MaxValue : (long)value;
        }

        /// <summary>
        /// Official <c>CalcSkillHitDelay</c>: <c>FinalDelay = Delay + distance * DelayDistFactor</c>
        /// where the distance is measured from the hit start position to the hit position. Hits
        /// anchored on the source cast position therefore keep their configured delay.
        /// </summary>
        private static float HitDelay(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleHitDefinition hit)
        {
            if (((FightHitAnchor)hit.Anchor).ResolveExecAnchor() != FightHitExecAnchor.TargetPos) return hit.Delay;
            double dx = target.X - actor.X;
            double dy = target.Y - actor.Y;
            return hit.Delay + (float)(Math.Sqrt(dx * dx + dy * dy) * hit.DelayDistFactor);
        }

        /// <summary>
        /// Skill lifetime in seconds from the official duration configuration. "AllEnded" values are
        /// relative to the last configured hit delay; a never-ending skill is paced to its last hit.
        /// </summary>
        private static float ResolveDuration(FightBattleSkillDefinition skill, float lastDelay)
        {
            if (skill.AllRelevantEndedDuration >= 0f) return lastDelay + skill.AllRelevantEndedDuration;
            if (skill.AllEndedDuration >= 0f) return lastDelay + skill.AllEndedDuration;
            if (skill.Duration >= 0f) return Math.Max(lastDelay, skill.Duration);
            return lastDelay;
        }

        private static float ResolveActionDuration(FightBattleSkillDefinition skill, float lastDelay)
        {
            if (skill.AllRelevantEndedActionDuration >= 0f)
                return lastDelay + skill.AllRelevantEndedActionDuration;
            if (skill.AllEndedActionDuration >= 0f) return lastDelay + skill.AllEndedActionDuration;
            if (skill.ActionDuration >= 0f) return Math.Max(lastDelay, skill.ActionDuration);
            return -1f;
        }

        private FightBattleStepEvent CreateEvent(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, List<FightBattleUnitState> targets,
            Dictionary<int, long> before, int globalRound,
            List<FightBattleMovementEvent> movements, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<int> spawnedUnitIds,
            List<FightBattleStatusCustomActionEvent> statusCustomActions,
            float skillDuration, float actionDuration)
        {
            float movementSeconds = 0f;
            if (movements != null)
            {
                var movementEnds = new Dictionary<int, float>();
                foreach (FightBattleMovementEvent movement in movements)
                {
                    if (movement == null || movement.Path == null || movement.Path.Count <= 1) continue;
                    movementEnds.TryGetValue(movement.UnitId, out float previousEnd);
                    float start = (float)FightOrdinaryMovementTiming.ActionStartDelay(movement, previousEnd);
                    float movementDuration = MovementDuration(movement);
                    float end = start + movementDuration;
                    movementEnds[movement.UnitId] = end;
                    movementSeconds = Math.Max(movementSeconds, end);
                    if (movement.OrdinarySkillPresentation != null)
                    {
                        float skillStart = start - movement.OrdinaryMoveDelay;
                        var presentation = movement.OrdinarySkillPresentation;
                        if (presentation.Duration >= 0f)
                            movementSeconds = Math.Max(movementSeconds, skillStart + presentation.Duration);
                        if (presentation.ActionDuration >= 0f)
                            actionDuration = Math.Max(actionDuration, skillStart + presentation.ActionDuration);
                    }
                }
            }
            float duration = Math.Max(skillDuration, movementSeconds);
            if (duration <= 0f) duration = DefaultStepSeconds;
            var result = new FightBattleStepEvent
            {
                ActorId = actor.Id,
                ActorRound = actor.SkillState.Round,
                GlobalRound = globalRound,
                Wave = checkpoint.WaveIndex + 1,
                SkillEntityId = casts.Count > 0 ? casts[0].SkillEntityId : skill == null ? 0 : skill.EntityId,
                SkillItemId = casts.Count > 0 ? casts[0].SkillItemId : skill == null ? 0 : skill.ItemId,
                Kind = targets.Count == 0 ? 0 : casts.Count == 0 && skill == null ? 1 : 2,
                ActionDuration = actionDuration,
                Duration = Math.Max(0.2f, duration),
                Movements = movements,
                SkillCasts = casts,
                DamageEvents = damageEvents ?? new List<FightBattleDamageEvent>(),
                StatusCustomActions = statusCustomActions ?? new List<FightBattleStatusCustomActionEvent>(),
                DispersedUnitIds = currentStepDispersedUnitIds ?? new List<int>(),
                SpawnedUnitIds = spawnedUnitIds ?? new List<int>()
            };
            foreach (FightBattleUnitState target in targets)
                if (!result.TargetIds.Contains(target.Id)) result.TargetIds.Add(target.Id);
            foreach (FightBattleUnitState unit in units)
            {
                long beforeHp = before.TryGetValue(unit.Id, out long value) ? value : 0L;
                if (beforeHp != unit.Hp)
                    result.HpChanges.Add(new FightBattleHpChange
                    {
                        UnitId = unit.Id,
                        Before = beforeHp,
                        After = unit.Hp,
                        MaxHp = unit.MaxHp
                    });
            }
            return result;
        }

        private static float MovementDuration(FightBattleMovementEvent movement)
        {
            float duration = 0f;
            for (int index = 1; index < movement.Path.Count; index++)
            {
                if (movement.MoveDuration >= 0f)
                    duration += movement.MoveDuration;
                else if (movement.MoveSpeed > 0f)
                {
                    FightGridPoint previous = movement.Path[index - 1];
                    FightGridPoint next = movement.Path[index];
                    int distance = Math.Abs(next.X - previous.X) + Math.Abs(next.Y - previous.Y);
                    duration += distance / movement.MoveSpeed;
                }
                else
                    duration += MovementSecondsPerCell;
            }
            return duration;
        }

        private bool MoveIntoSkillRange(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, List<FightBattleMovementEvent> movements)
        {
            FightBattleGrid grid = checkpoint.Grid;
            if (grid == null || actor.MoveDist <= 0 || skill.Range == null || skill.Range.Count == 0
                || statusCatalog != null && !statusCatalog.CanStartAction(actor,
                    FightOfficialCombatCatalog.MoveActionType))
                return false;

            List<FightBattleUnitState> targets = FightBattleTargeting.Select(actor, units,
                skill.TargetType, skill: skill, random: random);
            if (targets.Count == 0) return false;

            var occupied = OccupiedPositions(actor.Id);

            List<FightGridPoint> bestPath = null;
            bool bestReachesPosition = false;
            foreach (FightBattleUnitState target in targets)
                foreach (FightGridPoint desired in FightBattleTargeting.CastingPositions(target, skill.Range))
                {
                    List<FightGridPoint> path = grid.FindPath(
                        new FightGridPoint(actor.X, actor.Y), desired, actor.MoveMask,
                        actor.MoveDist + 2, 0, true, actor.BodyOffsets, occupied);
                    if (path == null || path.Count <= 1) continue;
                    // FindPath also returns a closest partial path for blocked destinations.
                    // Such a short fallback must not hide a reachable casting position.
                    bool reachesPosition = path[path.Count - 1].Equals(desired);
                    if (bestPath == null || reachesPosition && !bestReachesPosition
                        || reachesPosition == bestReachesPosition && path.Count < bestPath.Count)
                    {
                        bestPath = path;
                        bestReachesPosition = reachesPosition;
                    }
                }

            if (bestPath == null) return false;
            int steps = Math.Min(actor.MoveDist, bestPath.Count - 1);
            var movement = statusCatalog?.CreateRoleMovement(actor, false)
                ?? new FightBattleMovementEvent { UnitId = actor.Id,
                    HasDirection = true, Direction = actor.Direction };
            movement.Path.Add(new FightGridPoint(actor.X, actor.Y));
            for (int i = 1; i <= steps; i++)
            {
                FightGridPoint previous = bestPath[i - 1];
                FightGridPoint next = bestPath[i];
                UpdateMovementDirection(actor, previous, next);
                actor.X = next.X;
                actor.Y = next.Y;
                movement.Path.Add(next);
            }
            if (movement.Path.Count <= 1) return false;
            movements.Add(movement);
            NotifyMovementStart(movement, FightOfficialCombatCatalog.MoveActionType, movements);
            return true;
        }

        private bool MoveAwayFromNearestEnemy(FightBattleUnitState actor,
            List<FightBattleMovementEvent> movements)
        {
            FightBattleGrid grid = checkpoint.Grid;
            if (grid == null || actor == null || actor.MoveDist <= 0
                || statusCatalog != null && !statusCatalog.CanStartAction(actor,
                    FightOfficialCombatCatalog.MoveActionType)) return false;
            var enemies = new List<FightBattleUnitState>();
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.Side != actor.Side && FightBattleTargeting.IsSelectable(unit))
                    enemies.Add(unit);
            if (enemies.Count == 0) return false;

            FightGridPoint start = new FightGridPoint(actor.X, actor.Y);
            int startDistance = NearestEnemyDistance(start, enemies);
            int bestDistance = startDistance;
            List<FightGridPoint> bestPath = null;
            HashSet<FightGridPoint> occupied = OccupiedPositions(actor.Id);
            int minimumX = Math.Max(0, actor.X - actor.MoveDist);
            int maximumX = Math.Min(grid.Width - 1, actor.X + actor.MoveDist);
            int minimumY = Math.Max(0, actor.Y - actor.MoveDist);
            int maximumY = Math.Min(grid.Height - 1, actor.Y + actor.MoveDist);
            for (int x = minimumX; x <= maximumX; x++)
                for (int y = minimumY; y <= maximumY; y++)
                {
                    var desired = new FightGridPoint(x, y);
                    if (Manhattan(start, desired) == 0 || Manhattan(start, desired) > actor.MoveDist)
                        continue;
                    List<FightGridPoint> path = grid.FindPath(start, desired, actor.MoveMask,
                        actor.MoveDist + 2, 0, true, actor.BodyOffsets, occupied);
                    if (path == null || path.Count <= 1 || path.Count - 1 > actor.MoveDist) continue;
                    int distance = NearestEnemyDistance(path[path.Count - 1], enemies);
                    if (distance <= bestDistance) continue;
                    bestDistance = distance;
                    bestPath = path;
                }
            if (bestPath == null) return false;

            var movement = statusCatalog?.CreateRoleMovement(actor, true)
                ?? new FightBattleMovementEvent { UnitId = actor.Id,
                    HasDirection = true, Direction = actor.Direction };
            movement.Path.Add(start);
            for (int i = 1; i < bestPath.Count; i++)
            {
                FightGridPoint previous = bestPath[i - 1];
                FightGridPoint next = bestPath[i];
                UpdateMovementDirection(actor, previous, next);
                actor.X = next.X;
                actor.Y = next.Y;
                movement.Path.Add(next);
            }
            movements.Add(movement);
            NotifyMovementStart(movement, FightOfficialCombatCatalog.MoveActionType, movements);
            return true;
        }

        private static int NearestEnemyDistance(FightGridPoint position,
            IReadOnlyList<FightBattleUnitState> enemies)
        {
            int nearest = int.MaxValue;
            foreach (FightBattleUnitState enemy in enemies)
                nearest = Math.Min(nearest, Manhattan(position,
                    new FightGridPoint(enemy.X, enemy.Y)));
            return nearest;
        }

        private static int Manhattan(FightGridPoint left, FightGridPoint right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private HashSet<FightGridPoint> OccupiedPositions(int actingUnitId)
        {
            var occupied = new HashSet<FightGridPoint>();
            foreach (FightBattleUnitState unit in units)
            {
                if (unit.Id == actingUnitId || !FightBattleTargeting.IsSelectable(unit)) continue;
                occupied.Add(new FightGridPoint(unit.X, unit.Y));
                if (unit.BodyOffsets == null) continue;
                foreach (FightGridPoint offset in unit.BodyOffsets)
                    occupied.Add(new FightGridPoint(unit.X + offset.X, unit.Y + offset.Y));
            }
            return occupied;
        }

        private static void UpdateMovementDirection(FightBattleUnitState actor,
            FightGridPoint from, FightGridPoint to)
        {
            // Original GridTransform.GetFlipX returns null for vertical movement.
            // A combo's fixed direction must not freeze its next ordinary move's mirror.
            if (!actor.HasFlipX) actor.FlipX = actor.Direction == 1;
            if (from.X != to.X) actor.FlipX = to.X < from.X;
            actor.HasFlipX = true;
            actor.Direction = Direction(from, to);
        }

        private static int Direction(FightGridPoint from, FightGridPoint to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx == -1) return 1;
            if (dx == 1) return 3;
            if (dy == 1) return 2;
            return 0;
        }

        private static FightBattleSkillDefinition FindSkillBySlot(FightBattleUnitState actor, int slot)
        {
            if (actor.Skills == null) return null;
            foreach (FightBattleSkillDefinition skill in actor.Skills)
                if (skill != null && skill.Slot == slot) return skill;
            return null;
        }

        private void SpawnNextWaveIfCleared()
        {
            if (checkpoint.Waves.Count == 0 || EnemyHp(checkpoint.WaveIndex) > 0
                || checkpoint.WaveIndex + 1 >= checkpoint.Waves.Count) return;
            checkpoint.WaveIndex++;
            FightBattleWaveState wave = checkpoint.Waves[checkpoint.WaveIndex];
            wave.Spawned = true;
            foreach (int id in wave.UnitIds)
            {
                FightBattleUnitState unit = RequiredUnit(id);
                unit.Ended = false;
                unit.Turn.Ended = false;
                unit.Turn.RoundType = 0;
                SynchronizeTurn(unit);
                turns.InsertUnit(unit.Turn);
            }
            foreach (FightBattleUnitState unit in units)
                foreach (FightBattleSkillDefinition skill in unit.Skills)
                    if (skill != null && skill.ResetReleasedTimeOnNextWave
                        && FindSlot(unit, skill.Slot) != null)
                        new FightSkillState(unit.SkillState).ResetReleasedTimes(skill.Slot);
        }

        private void UpdateOutcome(int round)
        {
            if (checkpoint.ResultRules != null)
            {
                checkpoint.Outcome = checkpoint.ResultRules.Evaluate(checkpoint, round);
                checkpoint.Finished = checkpoint.Outcome.Finished;
                return;
            }
            long playerHp = RequiredUnit(1).Hp;
            long enemyHp = 0;
            long enemyMax = 0;
            var futureWaveUnitIds = new HashSet<int>();
            if (checkpoint.Waves.Count > 0)
                for (int wave = checkpoint.WaveIndex + 1; wave < checkpoint.Waves.Count; wave++)
                    foreach (int id in checkpoint.Waves[wave].UnitIds) futureWaveUnitIds.Add(id);

            foreach (FightBattleUnitState unit in units)
            {
                if (unit.Side != 1 && !futureWaveUnitIds.Contains(unit.Id))
                {
                    enemyHp += unit.Hp;
                    enemyMax += unit.MaxHp;
                }
            }
            if (checkpoint.Waves.Count > 0)
                for (int wave = checkpoint.WaveIndex + 1; wave < checkpoint.Waves.Count; wave++)
                    foreach (int id in checkpoint.Waves[wave].UnitIds)
                    {
                        FightBattleUnitState unit = RequiredUnit(id);
                        enemyHp += unit.Hp;
                        enemyMax += unit.MaxHp;
                    }

            bool victory = playerHp > 0 && enemyHp == 0;
            bool defeated = playerHp == 0;
            bool timeout = round > checkpoint.MaxRound;
            // Official StageInfo may cap the main player's personal rounds independently of the
            // global round limit. Reason 4 distinguishes it from the global timeout (reason 3).
            bool playerRoundLimit = checkpoint.IsPlayerRoundLimit && checkpoint.PlayerRoundLimit > 0
                && RequiredUnit(1).SkillState.Round >= checkpoint.PlayerRoundLimit;
            checkpoint.Finished = victory || defeated || timeout || playerRoundLimit;
            checkpoint.Outcome = new FightBattleOutcome
            {
                Finished = checkpoint.Finished,
                Won = victory,
                Reason = victory ? 1 : defeated ? 2 : timeout ? 3 : playerRoundLimit ? 4 : 0,
                Round = round,
                PlayerHp = playerHp,
                EnemyHp = enemyHp,
                EnemyMaxHp = enemyMax
            };
        }

        private void Finish(int reason)
        {
            UpdateOutcome(checkpoint.MaxRound + 1);
            if (!checkpoint.Finished)
            {
                checkpoint.Finished = true;
                checkpoint.Outcome.Finished = true;
                checkpoint.Outcome.Reason = reason;
            }
        }

        private FightBattleExecutionResult RecordTerminal(int sequence, int counter)
        {
            var stepEvent = new FightBattleStepEvent
            {
                GlobalRound = turns.GlobalRound(),
                Wave = checkpoint.WaveIndex + 1,
                Kind = 3
            };
            checkpoint.Counter = counter;
            checkpoint.RandomSeed = random.Seed;
            checkpoint.Receipts.Add(new FightBattleStepReceipt
            {
                Sequence = sequence,
                Counter = counter,
                Event = stepEvent
            });
            FightBattleCheckpointRules.Validate(checkpoint, true);
            return new FightBattleExecutionResult { Checkpoint = checkpoint, Event = stepEvent };
        }

        private long EnemyHp(int waveIndex)
        {
            long hp = 0;
            if (waveIndex < 0 || waveIndex >= checkpoint.Waves.Count) return hp;
            foreach (int id in checkpoint.Waves[waveIndex].UnitIds)
            {
                FightBattleUnitState unit = RequiredUnit(id);
                if (FightBattleResultRules.CountsEnemy(unit, checkpoint.ResultRules)) hp += unit.Hp;
            }
            return hp;
        }

        private FightBattleStepReceipt Receipt(int sequence, int counter)
        {
            foreach (FightBattleStepReceipt receipt in checkpoint.Receipts)
                if (receipt.Sequence == sequence && receipt.Counter == counter) return receipt;
            return null;
        }

        private FightBattleUnitState RequiredUnit(int id)
        {
            FightBattleUnitState unit;
            if (!unitById.TryGetValue(id, out unit)) throw new ArgumentException("Fight wave references an unknown unit.");
            return unit;
        }

        private IEnumerable<FightTurnUnit> Turns()
        {
            foreach (FightBattleUnitState unit in units) yield return unit.Turn;
        }

        private void RefreshTurnUnits()
        {
            foreach (FightBattleUnitState unit in new List<FightBattleUnitState>(units))
            {
                SynchronizeDeathForbidden(unit);
                SynchronizeTurn(unit);
            }
        }

        private static void SynchronizeTurn(FightBattleUnitState unit)
        {
            unit.Turn.Id = unit.Id;
            unit.Turn.MasterId = unit.MasterId;
            unit.Turn.Hp = unit.Hp;
            unit.Turn.Ended = unit.Ended;
        }

        private static FightSkillSlotState FindSlot(FightBattleUnitState unit, int slot)
        {
            foreach (FightSkillSlotState value in unit.SkillState.Slots)
                if (value != null && value.Slot == slot) return value;
            return null;
        }

        private static long GetProperty(FightBattleUnitState unit, int propertyId)
        {
            foreach (FightPropertyValue value in unit.Properties)
                if (value != null && value.Id == propertyId) return value.Value;
            return 0;
        }

        private int NextDamageId()
        {
            unchecked { checkpoint.DamageEventSerial++; }
            if (checkpoint.DamageEventSerial <= 0) checkpoint.DamageEventSerial = 1;
            return checkpoint.DamageEventSerial;
        }
    }
}

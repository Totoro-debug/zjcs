using System;
using System.Collections.Generic;

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
        private sealed class RandomTargetGroupState
        {
            public readonly HashSet<int> TargetIds = new HashSet<int>();
            public readonly HashSet<FightGridPoint> EmptyPositions = new HashSet<FightGridPoint>();
            public readonly HashSet<FightGridPoint> GridPositions = new HashSet<FightGridPoint>();
        }

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
        private int lastGlobalRound;
        private FightBattleExecutor(FightBattleCheckpoint checkpoint, FightDamageFormula formula,
            FightDamageSettlement settlement, bool initialize,
            FightOfficialCombatCatalog statusCatalog = null,
            FightOfficialSkillItemCatalog skillItems = null)
        {
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
            this.statusCatalog = statusCatalog;
            this.skillItems = skillItems;
            FightBattleCheckpointRules.Validate(checkpoint, !initialize);
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (checkpoint.Units.Count == 0) throw new ArgumentException("Fight roster is empty.", nameof(checkpoint));

            hitExecutor = new FightBattleHitExecutor(formula, settlement);
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                ApplyMonsterInfo(unit);
                InitializeUnitResilience(unit);
                ApplySkillAiPriorities(unit);
                if (statusCatalog != null && unit.Statuses != null)
                    foreach (FightBattleStatusState status in unit.Statuses)
                        if (status != null && statusCatalog.ContainsEntity(status.ClassId))
                        {
                            status.AiMoreHit = statusCatalog.StatusMoreHit(status.ClassId);
                            status.HasCreatorDamageBoost = statusCatalog.StatusHasCreatorDamageBoost(status.ClassId);
                        }
                units.Add(unit);
                unitById.Add(unit.Id, unit);
                SynchronizeTurn(unit);
            }
            if (!unitById.ContainsKey(1))
                throw new ArgumentException("Fight roster does not contain the main player unit.", nameof(checkpoint));
            turns = new FightTurnQueue(checkpoint.Timeline, Turns());
            lastGlobalRound = turns.GlobalRound();
            random = new FightRandomStream(checkpoint.RandomSeed);
            if (initialize) Initialize();
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

        public FightBattleExecutionResult Step(int sequence, int counter)
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
            if (statusCatalog != null && actor.Statuses != null)
                foreach (FightBattleStatusState status in actor.Statuses)
                    if (status != null && statusCatalog.StatusStopsSkillCooldown(status.ClassId))
                        skillState.StopCooldownForRound();
            var before = new Dictionary<int, long>();
            for (int i = 0; i < units.Count; i++) before[units[i].Id] = units[i].Hp;
            var movements = new List<FightBattleMovementEvent>();
            var casts = new List<FightBattleSkillCastEvent>();
            var damageEvents = new List<FightBattleDamageEvent>();
            var statusCustomActions = new List<FightBattleStatusCustomActionEvent>();
            currentStepSpawnedUnitIds = new List<int>();
            currentStepStatusCustomActions = statusCustomActions;
            var performedTargets = new List<FightBattleUnitState>();
            var checkedSlots = new HashSet<int>();
            float stepDuration = 0f;
            float stepActionDuration = -1f;

            if (FightStatusLifecycle.TickTurn(actor, units, actorRound, FightStatusLifecycle.TimingStart) > 0)
                RebuildBattleProperties();
            float statusEndDelay = 0f;
            ExecuteAccumulatedDamageEnds(casts, damageEvents, movements,
                ref statusEndDelay, ref stepDuration, ref stepActionDuration);

            if (statusCatalog != null)
                foreach (FightBattleUnitState source in new List<FightBattleUnitState>(units))
                {
                    List<FightBattleStatusStateTrigger> roundStartSkills =
                        FightStatusTriggerRuntime.CollectRoundStartSkills(source, actor, units, random);
                    if (roundStartSkills.Count == 0) continue;
                    float triggerDelay = 0f;
                    ExecuteConfiguredTriggerSkills(source, roundStartSkills, casts, damageEvents,
                        movements, ref triggerDelay, out float triggerDuration,
                        out float triggerActionDuration);
                    stepDuration = Math.Max(stepDuration, triggerDuration);
                    if (triggerActionDuration >= 0f)
                        stepActionDuration = Math.Max(stepActionDuration, triggerActionDuration);
                }
            ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                ref stepDuration, ref stepActionDuration);
            CollectStatusCustomActions(statusCustomActions);

            while (true)
            {
                FightBattleSkillDefinition skill = FightBattleTargeting.FindReadySkill(
                    actor, units, actorRound, excludedSlots: checkedSlots,
                    globalRound: globalRound);
                if (skill == null) break;

                var excludedForNextSkill = new HashSet<int>(checkedSlots) { skill.Slot };
                skill.UseLastAiPriority = FightBattleTargeting.FindReadySkill(actor, units,
                    actorRound, excludedSlots: excludedForNextSkill,
                    globalRound: globalRound) == null;

                List<FightBattleUnitState> targets = FightBattleTargeting.SelectInSkillRange(actor,
                    units, skill, random: random);
                if (targets.Count == 0 && MoveIntoSkillRange(actor, skill, movements))
                    targets = FightBattleTargeting.SelectInSkillRange(actor, units, skill,
                        random: random);
                if (targets.Count == 0 && skill.TargetType != 5
                    && !FightBattleTargeting.HasCasterAreaTargets(actor, units, skill))
                {
                    checkedSlots.Add(skill.Slot);
                    continue;
                }

                ExecuteSkill(actor, skill, targets, casts, damageEvents, movements,
                    out float castLastDelay, out float skillStartDuration,
                    out float skillStartActionDuration);
                skillState.RunSkill(skill.Slot);
                checkpoint.Statistics.RecordCast(actor.Id, skill.EntityId, skill.ItemId);
                stepDuration = Math.Max(stepDuration,
                    Math.Max(ResolveDuration(skill, castLastDelay), skillStartDuration));
                float actionDuration = ResolveActionDuration(skill, castLastDelay);
                if (actionDuration >= 0)
                    stepActionDuration = Math.Max(stepActionDuration, actionDuration);
                if (skillStartActionDuration >= 0f)
                    stepActionDuration = Math.Max(stepActionDuration, skillStartActionDuration);
                checkedSlots.Add(skill.Slot);
                performedTargets.AddRange(targets);
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = actor.Id,
                    Slot = skill.Slot,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId
                };
                foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                casts.Add(cast);
                ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                    ref stepDuration, ref stepActionDuration);
                CollectStatusCustomActions(statusCustomActions);
            }

            FightBattleSkillDefinition eventSkill = null;
            if (casts.Count > 0)
                eventSkill = FindSkillBySlot(actor, casts[0].Slot);
            else if (movements.Count == 0)
                MoveAwayFromNearestEnemy(actor, movements);
            // Keep unavailable-skill turns non-damaging; the local retreat rule may still move
            // the actor when a legal grid destination increases its distance from enemies.

            if (statusCatalog != null)
            {
                List<FightBattleStatusSkillInvocation> roundEndSkills =
                    FightStatusTriggerRuntime.CollectRoundEndSkills(actor, units, random);
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
            ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                ref stepDuration, ref stepActionDuration);
            CollectStatusCustomActions(statusCustomActions);

            skillState.FinishRound();
            turn.ActionRound = actor.SkillState.Round;
            SynchronizeTurn(actor);
            turns.Finish(turn);
            FightStatusLifecycle.TickTurn(actor, units, actorRound, FightStatusLifecycle.TimingEnd);
            RebuildBattleProperties();
            ExecuteAccumulatedDamageEnds(casts, damageEvents, movements,
                ref statusEndDelay, ref stepDuration, ref stepActionDuration);
            RestoreResilienceAfterPersonalRound(actor, actorRound);
            CollectStatusCustomActions(statusCustomActions);
            FightStatusLifecycle.RemoveDestroyedTargets(units);
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
            var stepEvent = CreateEvent(actor, eventSkill, performedTargets, before,
                globalRound, movements, casts, damageEvents, currentStepSpawnedUnitIds,
                statusCustomActions, stepDuration, stepActionDuration);
            currentStepSpawnedUnitIds = null;
            currentStepStatusCustomActions = null;
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
                new FightSkillState(unit.SkillState).Initialize(unit.SkillCooldownChangeAtStart);
                foreach (FightBattleStatusState status in unit.Statuses)
                {
                    FightStatusLifecycle.Initialize(status, 0);
                    FightStatusCooldownRuntime.Initialize(unit, status, random);
                }
            }
            if (checkpoint.StageStatus != null && statusCatalog != null)
                FightStageStatusRuntime.ApplyStart(checkpoint.StageStatus, statusCatalog, units, 0,
                    random);
            if (statusCatalog != null)
                foreach (FightBattleUnitState unit in units)
                    FightStatusTriggerRuntime.Apply(unit, statusCatalog, true, random: random);
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
            UpdateOutcome(1);
        }

        private void ExecuteSkill(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> targets, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            out float lastDelay, FightBattleUnitState targetPosition = null, bool allowDeadSource = false)
        {
            ExecuteSkill(actor, skill, targets, casts, damageEvents, movements, out lastDelay,
                out _, out _, targetPosition, allowDeadSource);
        }

        private void ExecuteSkill(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> targets, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            out float lastDelay, out float statusTriggerDuration,
            out float statusTriggerActionDuration, FightBattleUnitState targetPosition = null,
            bool allowDeadSource = false)
        {
            lastDelay = 0f;
            statusTriggerDuration = 0f;
            statusTriggerActionDuration = -1f;
            if (targets != null && targets.Count > 0)
                actor.AiPreviousTargetId = targets[0].Id;
            FightBattleDamageEvent skillCost = ApplySkillCost(actor, skill);
            if (skillCost != null) damageEvents.Add(skillCost);
            long skillCostHp = skillCost == null
                ? 0L : Math.Max(0L, skillCost.BeforeHp - skillCost.AfterHp);

            if (statusCatalog != null && triggerDepth == 0)
            {
                List<FightBattleStatusSkillInvocation> skillStartSkills =
                    FightStatusTriggerRuntime.CollectSkillStartSkills(actor, skill, targets,
                        units, random, statusCatalog);
                if (skillStartSkills.Count > 0)
                {
                    float triggerDelay = 0f;
                    ExecuteConfiguredStatusSkills(skillStartSkills, casts, damageEvents,
                        movements, ref triggerDelay, out statusTriggerDuration,
                        out statusTriggerActionDuration);
                    lastDelay = Math.Max(lastDelay, triggerDelay);
                }
            }

            FightDamageFalloff.BeginCast(skill, ref checkpoint.FalloffSerial);
            IReadOnlyList<FightBattleHitDefinition> hits = FightBattleHitCollector.CollectAll(skill);
            var randomTargetGroups = new Dictionary<int, RandomTargetGroupState>();

            foreach (FightBattleHitDefinition hit in hits)
            {
                if (hit.Delay > lastDelay) lastDelay = hit.Delay;
                List<FightBattleUnitState> hitTargets;
                bool hasRandomTargetContext = hit.RandomTargetGroupId > 0;
                if (hasRandomTargetContext)
                {
                    if (!randomTargetGroups.TryGetValue(hit.RandomTargetGroupId,
                        out RandomTargetGroupState groupState))
                    {
                        groupState = new RandomTargetGroupState();
                        randomTargetGroups.Add(hit.RandomTargetGroupId, groupState);
                    }

                    var selectedTargets = new List<FightBattleUnitState>();
                    if (hit.RandomTargetScope != null && hit.RandomTargetScope.Count > 0)
                    {
                        FightBattleUnitState scopeAnchor = hit.RandomTargetAnchor == 0
                            ? targetPosition ?? (targets != null && targets.Count > 0
                                ? targets[0] : null)
                            : actor;
                        List<FightBattleRandomScopeSelection> selections =
                            FightBattleTargeting.SelectRandomScopePositions(actor, units, skill,
                                scopeAnchor, hit.RandomTargetScope, checkpoint.Grid,
                                hit.RandomCount, random, hit.RandomTargetAllowEmptyScope,
                                hit.RandomTargetNotAllowHitEmpty, hit.RandomTargetNotSameTarget,
                                groupState.TargetIds, hit.RandomTargetNotSameEmptyScope,
                                groupState.EmptyPositions, hit.RandomTargetNotSameGrid,
                                groupState.GridPositions,
                                hit.RandomTargetFilterOutOfGroundScope);
                        foreach (FightBattleRandomScopeSelection selection in selections)
                        {
                            groupState.GridPositions.Add(selection.Position);
                            if (selection.IsEmpty)
                                groupState.EmptyPositions.Add(selection.Position);
                            foreach (FightBattleUnitState selectedTarget in selection.Targets)
                            {
                                if (hit.RandomTargetNotSameTarget)
                                    groupState.TargetIds.Add(selectedTarget.Id);
                                if (!hit.RandomTargetNotSameTarget
                                    || !selectedTargets.Exists(target => target.Id == selectedTarget.Id))
                                    selectedTargets.Add(selectedTarget);
                            }
                        }
                    }
                    else
                    {
                        var randomCandidates = new List<FightBattleUnitState>();
                        foreach (FightBattleUnitState candidate in targets ??
                            new List<FightBattleUnitState>())
                            if ((!hit.RandomTargetNotSameGrid
                                    || !groupState.GridPositions.Contains(
                                        new FightGridPoint(candidate.X, candidate.Y)))
                                && (!hit.RandomTargetNotSameTarget
                                    || !groupState.TargetIds.Contains(candidate.Id)))
                                randomCandidates.Add(candidate);
                        selectedTargets = FightBattleTargeting.SelectRandomHitTargets(
                            randomCandidates, hit.RandomCount, random,
                            hit.RandomTargetNotSameTarget ? groupState.TargetIds : null);
                        foreach (FightBattleUnitState selectedTarget in selectedTargets)
                        {
                            groupState.TargetIds.Add(selectedTarget.Id);
                            groupState.GridPositions.Add(new FightGridPoint(
                                selectedTarget.X, selectedTarget.Y));
                        }
                    }
                    if (selectedTargets.Count == 0) continue;

                    hitTargets = new List<FightBattleUnitState>();
                    if (hit.Scope != null && hit.Scope.Count > 0)
                    {
                        var randomTargetEffect = new FightBattleHitDefinition
                        {
                            Anchor = 0,
                            TargetMode = hit.TargetMode,
                            Scope = hit.Scope
                        };
                        foreach (FightBattleUnitState selectedTarget in selectedTargets)
                            hitTargets.AddRange(FightBattleTargeting.SelectScopeTargets(actor,
                                units, skill, selectedTarget, randomTargetEffect, random));
                    }
                    else hitTargets.AddRange(selectedTargets);
                }
                else if (hit.Scope != null && hit.Scope.Count > 0)
                {
                    List<FightBattleUnitState> scopeTargets = FightBattleTargeting.SelectScopeTargets(
                        actor, units, skill, targetPosition
                            ?? (targets.Count > 0 ? targets[0] : null), hit, random);
                    hitTargets = hit.TargetMode == 2
                        ? FightBattleTargeting.SelectHitTargets(scopeTargets, 2, hit.RandomCount, random)
                        : scopeTargets;
                }
                else
                {
                    hitTargets = FightBattleTargeting.SelectHitTargets(targets,
                        hit.TargetMode, hit.RandomCount, random);
                }
                if (hit.IsSummon)
                {
                    SpawnSummons(actor, skill, hit,
                        hasRandomTargetContext ? hitTargets : targets);
                    continue;
                }
                foreach (FightBattleUnitState target in hitTargets)
                {
                    float hitDelay = HitDelay(actor, target, hit);
                    if (hitDelay > lastDelay) lastDelay = hitDelay;
                    long beforeHp = target.Hp;
                    int damageId = NextDamageId();
                    bool originalHasCustomDamage = hit.HasCustomDamage;
                    long originalCustomDamage = hit.CustomDamage;
                    ApplyResilienceDamageOverride(actor, target, skill, hit);
                    float elementConfrontation = 0f;
                    bool elementConfrontationResolved = true;
                    if (statusCatalog != null && hit.ActionType != 0 && hit.ActionType != 5
                        && skill.SkillType != 3 && hit.DamageType >= 1 && hit.DamageType <= 7)
                        elementConfrontationResolved = statusCatalog.TryResolveElementConfrontation(
                            target, skill.ElementType, out elementConfrontation);
                    FightBattleHitResult resolved;
                    try
                    {
                        resolved = hitExecutor.Apply(random, damageId, actor, target,
                            skill, hit, checkpoint.StageType, checkpoint.IsPvp, elementConfrontation,
                            new List<int>(), allowDeadSource);
                    }
                    finally
                    {
                        hit.HasCustomDamage = originalHasCustomDamage;
                        hit.CustomDamage = originalCustomDamage;
                    }
                    if (resolved.Formula == null && resolved.Settlement == null) continue;
                    var damage = new FightBattleDamageEvent
                    {
                        DamageId = damageId,
                        SourceId = actor.Id,
                        TargetId = target.Id,
                        SkillEntityId = skill.EntityId,
                        SkillItemId = skill.ItemId,
                        HitEntityId = hit.EntityId,
                        DamageType = hit.DamageType,
                        ElementType = skill == null ? 0 : skill.ElementType,
                        ElementConfrontation = elementConfrontation,
                        IsElementAffect = resolved.Formula != null && resolved.Formula.ElementAffected,
                        ElementConfrontationUnresolved = !elementConfrontationResolved,
                        Delay = hitDelay,
                        Anchor = hit.Anchor,
                        IsCure = resolved.IsCure,
                        Critical = resolved.Type.Critical,
                        Block = resolved.Type.Block,
                        Dodge = resolved.Type.Dodge,
                        Immunity = resolved.Type.Immunity,
                        Blinding = resolved.Type.Blinding,
                        IgnoreShield = hit.IgnoreShield,
                        TrueDamage = hit.TrueDamage,
                        BreakResilience = hit.BreakResilience,
                        Damage = resolved.Settlement == null ? 0 : resolved.Settlement.RealDamageValue,
                        HasDamageValue = resolved.Settlement != null,
                        DamageValue = resolved.Settlement == null ? 0 : resolved.Settlement.DamageValue,
                        ShieldDamage = resolved.ShieldDamage,
                        AbsoluteDamage = resolved.Formula == null ? 0 : resolved.Formula.AbsoluteDamage,
                        BeforeHp = beforeHp,
                        AfterHp = target.Hp,
                        MaxHp = target.MaxHp
                    };
                    damageEvents.Add(damage);
                    checkpoint.Statistics.RecordHit(damage);
                    FightStatusAccumulatedDamageRuntime.RecordHit(target, damage);
                    var triggerSkills = new List<int>();
                    var configuredTriggerSkills = new List<FightBattleStatusStateTrigger>();
                    var hpTriggerSkills = new List<int>();
                    triggerSkills.AddRange(resolved.TriggeredStatusSkills);
                    long actualHpLoss = Math.Max(0L, beforeHp - target.Hp);
                    CollectUnflinchingActions(target, actualHpLoss);
                    FightStatusTriggerRuntime.ApplyAccumulatedDamage(target,
                        actualHpLoss, statusCatalog, random, configuredTriggerSkills);
                    ApplyHitStatuses(actor, target, skill, hit, random, skillCostHp,
                        actualHpLoss);
                    ApplyResilienceHit(actor, target, skill, hit, resolved, casts,
                        damageEvents, movements, ref lastDelay);
                    ApplyHitDisperse(actor, target, hit, random);
                    ApplyHitMove(actor, target, hit, movements);
                    ApplyHemophagia(actor, hit, damage, hitDelay, skill, damageEvents);
                    FightStatusTriggerRuntime.Apply(target, statusCatalog, false, hpTriggerSkills,
                        random);
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
                    if (hpTriggerSkills.Count > 0)
                        ExecuteTriggerSkills(target, hpTriggerSkills, casts, damageEvents, movements,
                            ref lastDelay);
                    ApplyChildSkill(target, hit, casts, damageEvents, movements, ref lastDelay);
                }
            }
            ApplySourceMoves(actor, skill, targets, movements, ref lastDelay);
            FightDamageFalloff.EndCast(units, skill);
            ApplyExtraSkills(actor, skill, casts, damageEvents, movements, ref lastDelay);
            ApplySkillReplay(actor, skill, casts, damageEvents, movements, ref lastDelay);
            if (statusCatalog != null && triggerDepth == 0
                && FightStatusTriggerRuntime.ApplySkillEndStatuses(actor, skill, units,
                    statusCatalog, random) > 0)
                RebuildBattleProperties();
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

                FightGridPoint offset = hit.SummonPositions[i];
                if (reference.Direction == 1) offset.X = -offset.X;
                var position = new FightGridPoint(reference.X + offset.X, reference.Y + offset.Y);
                int level = hit.SummonLevelType == 1 ? Math.Max(1, skill.Level)
                    : Math.Max(1, actor.Level);
                FightBattleUnitState summon = CreateSummonedUnit(actor, skill, pool, hit, level,
                    position);
                if (!CanPlaceSummon(summon)) continue;

                units.Add(summon);
                unitById.Add(summon.Id, summon);
                checkpoint.Units.Add(summon);
                turns.RegisterUnit(summon.Turn, hit.SummonImmediateRound);
                if (currentStepSpawnedUnitIds != null)
                    currentStepSpawnedUnitIds.Add(summon.Id);
                AddSummonStatuses(summon, actor, skill, pool, hit);
                FightStatusTriggerRuntime.Apply(summon, statusCatalog, true, random: random);
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
            FightBattleHitDefinition hit, int level, FightGridPoint position)
        {
            int groupNo = actor.MonsterGroupNo;
            var properties = new Dictionary<int, long>(statusCatalog.CalculateMonsterProps(
                pool.MonsterId, groupNo, level, Math.Max(0, actor.SubRank)));
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

        private void ApplySummonStatus(FightBattleUnitState summon, FightBattleUnitState actor,
            FightBattleSkillDefinition skill, int statusId)
        {
            if (statusId <= 0) return;
            FightBattleStatusState template = statusCatalog.CreateStatus(statusId, summon.Id,
                actor.Id, NextStatusInstanceId(), skill.EntityId, skill.ItemId, skill.Rank,
                skill.Level, skill.SubRank, actor.Properties);
            FightBattleStatusState applied = FightStatusLifecycle.Apply(summon, template,
                summon.SkillState.Round, template.InstanceId, random);
            FightStatusLifecycle.Initialize(applied, summon.SkillState.Round);
        }

        private int NextSummonUnitId()
        {
            int maximum = checkpoint.SummonSerial;
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.Id > maximum) maximum = unit.Id;
            if (maximum == int.MaxValue) throw new InvalidOperationException("Summoned unit id space is exhausted.");
            checkpoint.SummonSerial = maximum + 1;
            return checkpoint.SummonSerial;
        }

        private int NextStatusInstanceId()
        {
            int maximum = checkpoint.StatusInstanceSerial;
            foreach (FightBattleUnitState unit in units)
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

        private FightBattleDamageEvent ApplySkillCost(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
        {
            if (skill == null || actor == null) return null;
            if (skill.CostType == 1 && skill.SkillCost > 0)
            {
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
            ref float lastDelay)
        {
            if (skill.SourceMoves == null || skill.SourceMoves.Count == 0) return;
            if (movements == null) return;
            foreach (FightBattleSkillMove move in skill.SourceMoves)
            {
                if (move == null) continue;
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
                    MoveNextToTarget(actor, target, move, movements);
                    ApplyMovementAction(movements, movementIndex, move);
                    continue;
                }
                if (move.Direction == 2)
                {
                    ApplyPathSideMove(actor, actor, target, move, movements);
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
                    ApplyMovementAction(movements, movementIndex, move);
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
            var movement = new FightBattleMovementEvent { UnitId = actor.Id };
            movement.Path.Add(start);
            for (int i = 1; i <= steps; i++)
            {
                FightGridPoint previous = path[i - 1];
                FightGridPoint next = path[i];
                actor.Direction = Direction(previous, next);
                actor.X = next.X;
                actor.Y = next.Y;
                movement.Path.Add(next);
            }
            movements.Add(movement);
            return true;
        }

        private int childDepth;

        /// <summary>
        /// Official <c>FightDamageComponent.ChildSkillCfg</c>: after a hit lands, the damaged target may
        /// release the configured child skill (rate and remaining-HP condition). Child skills run one
        /// level deep and keep their casts in the current step.
        /// </summary>
        private void ApplyChildSkill(FightBattleUnitState target, FightBattleHitDefinition hit,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || hit == null || hit.ChildSkillId <= 0 || childDepth > 0) return;
            if (target == null || target.Hp <= 0) return;
            if (hit.ChildSkillRate < 1f && random.Next() > hit.ChildSkillRate) return;
            if (hit.ChildSkillLessThanHpPer >= 0f
                && target.Hp / (double)Math.Max(1L, target.MaxHp) > hit.ChildSkillLessThanHpPer) return;
            int level = Math.Max(1, target.Level);
            int subRank = Math.Max(1, target.SubRank);
            FightBattleSkillDefinition skill;
            try
            {
                skill = statusCatalog.CreateSkill(hit.ChildSkillId, -1, 0, 1, level, subRank,
                    ToPropertyList(statusCatalog.CalculateSkillProps(hit.ChildSkillId, 1, level, subRank)));
            }
            catch (InvalidOperationException)
            {
                return;
            }
            List<FightBattleUnitState> targets = FightBattleTargeting.Select(target, units,
                skill.TargetType, 0, false, false, skill, random);
            if (targets.Count == 0 && !FightBattleTargeting.HasCasterAreaTargets(target, units, skill)) return;
            checkpoint.Statistics.RecordCast(target.Id, skill.EntityId, skill.ItemId);
            var cast = new FightBattleSkillCastEvent
            {
                ActorId = target.Id,
                Slot = -1,
                SkillEntityId = skill.EntityId,
                SkillItemId = skill.ItemId
            };
            foreach (FightBattleUnitState unit in targets) cast.TargetIds.Add(unit.Id);
            casts.Add(cast);
            childDepth++;
            try
            {
                ExecuteSkill(target, skill, targets, casts, damageEvents, movements, out float delay);
                if (delay > lastDelay) lastDelay = delay;
            }
            finally
            {
                childDepth--;
            }
        }

        private int replayDepth;

        /// <summary>
        /// Official <c>FightSkillReplayComponent</c>: after the skill action, replay the same skill once
        /// when the actor's configured status stack condition holds. The status stack count is consumed
        /// when configured. Relation 1 is treated as "at least" (the only value used by the pinned
        /// data) and any other relation as equality.
        /// </summary>
        private void ApplySkillReplay(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (!skill.HasReplay || skill.ReplayStatusId <= 0 || replayDepth > 0) return;
            FightBattleStatusState status = null;
            foreach (FightBattleStatusState candidate in actor.Statuses)
                if (candidate != null && candidate.ClassId == skill.ReplayStatusId) { status = candidate; break; }
            if (status == null) return;
            bool matched = skill.ReplayRelation == 1
                ? status.StackCount >= skill.ReplayStackCount
                : status.StackCount == skill.ReplayStackCount;
            if (!matched) return;
            if (skill.ReplayConsumeStack && skill.ReplayConsumeCount > 0)
            {
                status.StackCount -= skill.ReplayConsumeCount;
                if (status.StackCount <= 0) actor.Statuses.Remove(status);
            }
            List<FightBattleUnitState> targets = FightBattleTargeting.Select(actor, units,
                skill.TargetType, 0, false, false, skill, random);
            if (targets.Count == 0 && !FightBattleTargeting.HasCasterAreaTargets(actor, units, skill)) return;
            checkpoint.Statistics.RecordCast(actor.Id, skill.EntityId, skill.ItemId);
            var cast = new FightBattleSkillCastEvent
            {
                ActorId = actor.Id,
                Slot = skill.Slot,
                SkillEntityId = skill.EntityId,
                SkillItemId = skill.ItemId,
                IsReplay = true
            };
            foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
            casts.Add(cast);
            replayDepth++;
            try
            {
                ExecuteSkill(actor, skill, targets, casts, damageEvents, movements, out float delay);
                if (delay > lastDelay) lastDelay = delay;
            }
            finally
            {
                replayDepth--;
            }
        }

        private int extraDepth;

        /// <summary>
        /// Official <c>FightSkillExtSkillComponent</c>: when the skill action ends, roll each entry's
        /// rate and release the extra skill as the same actor. Extra skills run one level deep and
        /// keep their casts/damage in the current step.
        /// </summary>
        private void ApplyExtraSkills(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || skill == null || skill.ExtraSkills == null
                || skill.ExtraSkills.Count == 0 || extraDepth > 0) return;
            extraDepth++;
            try
            {
                int level = Math.Max(1, actor.Level);
                int subRank = Math.Max(1, actor.SubRank);
                foreach (FightBattleSkillExtra extra in skill.ExtraSkills)
                {
                    if (extra == null || extra.SkillId <= 0) continue;
                    if (extra.Rate < 1f && random.Next() > extra.Rate) continue;
                    FightBattleSkillDefinition extraSkill;
                    try
                    {
                        extraSkill = statusCatalog.CreateSkill(extra.SkillId, -1, 0, 1, level, subRank,
                            ToPropertyList(statusCatalog.CalculateSkillProps(extra.SkillId, 1, level, subRank)));
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    List<FightBattleUnitState> targets = FightBattleTargeting.Select(actor, units,
                        extraSkill.TargetType, 0, false, false, extraSkill, random);
                    if (targets.Count == 0
                        && !FightBattleTargeting.HasCasterAreaTargets(actor, units, extraSkill)) continue;
                    checkpoint.Statistics.RecordCast(actor.Id, extraSkill.EntityId, extraSkill.ItemId);
                    var cast = new FightBattleSkillCastEvent
                    {
                        ActorId = actor.Id,
                        Slot = -1,
                        SkillEntityId = extraSkill.EntityId,
                        SkillItemId = extraSkill.ItemId
                    };
                    foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                    casts.Add(cast);
                    ExecuteSkill(actor, extraSkill, targets, casts, damageEvents, movements,
                        out float delay);
                    if (delay > lastDelay) lastDelay = delay;
                }
            }
            finally
            {
                extraDepth--;
            }
        }

        private int triggerDepth;

        /// <summary>
        /// Executes the skill list of an HP-triggered status as the status owner. Trigger casts run at
        /// most one level deep (a trigger skill cannot start another trigger chain), keep their damage
        /// and movement events in the current step, and do not consume the owner's skill slots.
        /// </summary>
        private void ExecuteTriggerSkills(FightBattleUnitState owner, List<int> skillIds,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (statusCatalog == null || skillIds == null || skillIds.Count == 0 || triggerDepth > 0) return;
            triggerDepth++;
            try
            {
                int level = Math.Max(1, owner.Level);
                int subRank = Math.Max(1, owner.SubRank);
                foreach (int skillId in skillIds)
                {
                    if (skillId <= 0) continue;
                    FightBattleSkillDefinition skill;
                    try
                    {
                        skill = statusCatalog.CreateSkill(skillId, -1, 0, 1, level, subRank,
                            ToPropertyList(statusCatalog.CalculateSkillProps(skillId, 1, level, subRank)));
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    List<FightBattleUnitState> targets = FightBattleTargeting.Select(owner, units,
                        skill.TargetType, 0, false, false, skill, random);
                    if (targets.Count == 0
                        && !FightBattleTargeting.HasCasterAreaTargets(owner, units, skill)) continue;
                    checkpoint.Statistics.RecordCast(owner.Id, skill.EntityId, skill.ItemId);
                    var cast = new FightBattleSkillCastEvent
                    {
                        ActorId = owner.Id,
                        Slot = -1,
                        SkillEntityId = skill.EntityId,
                        SkillItemId = skill.ItemId
                    };
                    foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                    casts.Add(cast);
                    ExecuteSkill(owner, skill, targets, casts, damageEvents, movements, out float delay);
                    if (delay > lastDelay) lastDelay = delay;
                }
            }
            finally
            {
                triggerDepth--;
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
            if (statusCatalog == null || triggers == null || triggers.Count == 0 || triggerDepth > 0)
                return;
            triggerDepth++;
            try
            {
                int level = Math.Max(1, owner.Level);
                int subRank = Math.Max(1, owner.SubRank);
                foreach (FightBattleStatusStateTrigger trigger in triggers)
                {
                    if (trigger == null || trigger.EntityId <= 0) continue;
                    FightBattleSkillDefinition skill;
                    try
                    {
                        skill = statusCatalog.CreateSkill(trigger.EntityId, -1, 0, 1, level, subRank,
                            ToPropertyList(statusCatalog.CalculateSkillProps(trigger.EntityId, 1,
                                level, subRank)));
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }

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
                        Slot = -1,
                        SkillEntityId = skill.EntityId,
                        SkillItemId = skill.ItemId
                    };
                    foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                    casts.Add(cast);
                    ExecuteSkill(owner, skill, targets, casts, damageEvents, movements,
                        out float delay, targetPosition);
                    if (delay > lastDelay) lastDelay = delay;
                    maxDuration = Math.Max(maxDuration, ResolveDuration(skill, delay));
                    float actionDuration = ResolveActionDuration(skill, delay);
                    if (actionDuration >= 0f)
                        maxActionDuration = Math.Max(maxActionDuration, actionDuration);
                }
            }
            finally
            {
                triggerDepth--;
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
            if (statusCatalog == null || invocations == null || invocations.Count == 0
                || triggerDepth > 0) return;
            triggerDepth++;
            try
            {
                foreach (FightBattleStatusSkillInvocation invocation in invocations)
                {
                    if (invocation == null || invocation.Trigger == null
                        || !unitById.TryGetValue(invocation.SourceUnitId,
                            out FightBattleUnitState source)
                        || !unitById.TryGetValue(invocation.TargetUnitId,
                            out FightBattleUnitState target)
                        || !unitById.TryGetValue(invocation.TargetPositionUnitId,
                            out FightBattleUnitState targetPosition)
                        || !FightBattleTargeting.IsSelectable(source)
                        || !FightBattleTargeting.IsSelectable(target)
                        || !FightBattleTargeting.IsSelectable(targetPosition)) continue;

                    FightBattleSkillDefinition skill;
                    try
                    {
                        skill = statusCatalog.CreateSkill(invocation.Trigger.EntityId, -1, 0,
                            1, Math.Max(1, source.Level), Math.Max(1, source.SubRank),
                            ToPropertyList(statusCatalog.CalculateSkillProps(
                                invocation.Trigger.EntityId, 1, Math.Max(1, source.Level),
                                Math.Max(1, source.SubRank))));
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    if (!IsExplicitTargetCompatible(source, target, skill.TargetType)
                        || FightBattleTargeting.IsActionBlockedByStatus(target,
                            skill.BlockingTargetActionTypes)) continue;

                    var targets = new List<FightBattleUnitState> { target };
                    checkpoint.Statistics.RecordCast(source.Id, skill.EntityId, skill.ItemId);
                    var cast = new FightBattleSkillCastEvent
                    {
                        ActorId = source.Id,
                        Slot = -1,
                        SkillEntityId = skill.EntityId,
                        SkillItemId = skill.ItemId
                    };
                    cast.TargetIds.Add(target.Id);
                    casts.Add(cast);
                    ExecuteSkill(source, skill, targets, casts, damageEvents, movements,
                        out float delay, targetPosition);
                    if (delay > lastDelay) lastDelay = delay;
                    maxDuration = Math.Max(maxDuration, ResolveDuration(skill, delay));
                    float actionDuration = ResolveActionDuration(skill, delay);
                    if (actionDuration >= 0f)
                        maxActionDuration = Math.Max(maxActionDuration, actionDuration);
                }
            }
            finally
            {
                triggerDepth--;
            }
        }

        private void ExecutePendingCooldownModifySkills(
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float stepDuration,
            ref float stepActionDuration)
        {
            if (statusCatalog == null || triggerDepth > 0) return;
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

        private void CollectUnflinchingActions(FightBattleUnitState target, long actualHpLoss)
        {
            if (target == null || actualHpLoss <= 0 || target.Statuses == null
                || currentStepStatusCustomActions == null) return;
            // The server invocation timing is unavailable; offline uses one configured-rate check per actual HP loss.
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (status == null || !status.HasUnflinchingOnDamageComponent
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
        private void ApplyHemophagia(FightBattleUnitState actor, FightBattleHitDefinition hit,
            FightBattleDamageEvent applied, float delay, FightBattleSkillDefinition skill,
            List<FightBattleDamageEvent> events)
        {
            if (hit.HemophagiaProperty <= 0 || applied == null || applied.IsCure) return;
            long dealt = Math.Abs(applied.Damage);
            if (dealt <= 0) return;
            long property = GetProperty(actor, hit.HemophagiaProperty);
            if (property <= 0) return;
            double rate = property / 10000.0;
            if (rate > 1.0) rate = 1.0;
            long heal = (long)Math.Round(dealt * rate);
            if (heal <= 0) return;
            long before = actor.Hp;
            actor.Hp = Math.Min(actor.MaxHp, actor.Hp + heal);
            long actual = actor.Hp - before;
            if (actual <= 0) return;
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
        }

        /// <summary>
        /// Official <c>FightDamageComponent.MoveCfg</c>: pushes or pulls the damaged unit along the
        /// source-to-target vector by the configured distance, falling back to shorter distances
        /// when the destination is blocked. The default (-1) means no displacement.
        /// </summary>
        private void ApplyHitMove(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleHitDefinition hit, List<FightBattleMovementEvent> movements)
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
                ApplyPathSideMove(target, actor, target, move, movements);
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
                ApplyMovementAction(movements, movementIndex, move);
                break;
            }
        }

        private bool ApplyPathSideMove(FightBattleUnitState mover, FightBattleUnitState source,
            FightBattleUnitState target, FightBattleSkillMove move,
            List<FightBattleMovementEvent> movements)
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
                    ApplyMovementAction(movements, movementIndex, move);
                    return true;
                }
            }
            return false;
        }

        private static void ApplyMovementAction(List<FightBattleMovementEvent> movements,
            int startIndex, FightBattleSkillMove move)
        {
            if (movements == null || move == null) return;
            for (int index = startIndex; index < movements.Count; index++)
            {
                FightBattleMovementEvent movement = movements[index];
                if (movement == null) continue;
                movement.Delay = move.Delay;
                movement.MoveDuration = move.MoveDuration;
                movement.MoveSpeed = move.MoveSpeed;
            }
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
            if (statusCatalog != null)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
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
                owner.Id, actor.Id, instanceId, skill == null ? 0 : skill.EntityId,
                skill == null ? 0 : skill.ItemId, skill == null ? 1 : skill.Rank,
                skill == null ? 1 : skill.Level, skill == null ? actor.SubRank : skill.SubRank,
                actor.Properties, customStatusValue);
            FightStatusLifecycle.Apply(owner, template, -1, instanceId, random);
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
            if (hit.Anchor != 0) return hit.Delay;
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
                    float start = Math.Max(0f, movement.Delay);
                    if (movementEnds.TryGetValue(movement.UnitId, out float previousEnd))
                        start = Math.Max(start, previousEnd);
                    float movementDuration = MovementDuration(movement);
                    float end = start + movementDuration;
                    movementEnds[movement.UnitId] = end;
                    movementSeconds = Math.Max(movementSeconds, end);
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
            foreach (FightBattleUnitState target in targets)
                foreach (FightGridPoint range in skill.Range)
                {
                    var desired = new FightGridPoint(target.X - range.X, target.Y - range.Y);
                    List<FightGridPoint> path = grid.FindPath(
                        new FightGridPoint(actor.X, actor.Y), desired, actor.MoveMask,
                        actor.MoveDist + 2, 0, true, actor.BodyOffsets, occupied);
                    if (path == null || path.Count <= 1) continue;
                    if (bestPath == null || path.Count < bestPath.Count) bestPath = path;
                }

            if (bestPath == null) return false;
            int steps = Math.Min(actor.MoveDist, bestPath.Count - 1);
            var movement = new FightBattleMovementEvent { UnitId = actor.Id };
            movement.Path.Add(new FightGridPoint(actor.X, actor.Y));
            for (int i = 1; i <= steps; i++)
            {
                FightGridPoint previous = bestPath[i - 1];
                FightGridPoint next = bestPath[i];
                actor.Direction = Direction(previous, next);
                actor.X = next.X;
                actor.Y = next.Y;
                movement.Path.Add(next);
            }
            if (movement.Path.Count <= 1) return false;
            movements.Add(movement);
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

            var movement = new FightBattleMovementEvent { UnitId = actor.Id };
            movement.Path.Add(start);
            for (int i = 1; i < bestPath.Count; i++)
            {
                FightGridPoint previous = bestPath[i - 1];
                FightGridPoint next = bestPath[i];
                actor.Direction = Direction(previous, next);
                actor.X = next.X;
                actor.Y = next.Y;
                movement.Path.Add(next);
            }
            movements.Add(movement);
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
            foreach (int id in checkpoint.Waves[waveIndex].UnitIds) hp += RequiredUnit(id).Hp;
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
            foreach (FightBattleUnitState unit in units) SynchronizeTurn(unit);
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

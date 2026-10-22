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

    public sealed class FightBattleExecutor
    {
        /// <summary>Official GameMoveTime used by the scene view for one grid cell.</summary>
        public const float MovementSecondsPerCell = 0.5f;
        private const float DefaultStepSeconds = 0.35f;
        private readonly FightBattleCheckpoint checkpoint;
        private readonly FightBattleHitExecutor hitExecutor;
        private readonly List<FightBattleUnitState> units = new List<FightBattleUnitState>();
        private readonly Dictionary<int, FightBattleUnitState> unitById = new Dictionary<int, FightBattleUnitState>();
        private readonly FightTurnQueue turns;
        private readonly FightRandomStream random;

        private FightBattleExecutor(FightBattleCheckpoint checkpoint, FightDamageFormula formula,
            FightDamageSettlement settlement, bool initialize)
        {
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            FightBattleCheckpointRules.Validate(checkpoint, !initialize);
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (checkpoint.Units.Count == 0) throw new ArgumentException("Fight roster is empty.", nameof(checkpoint));

            hitExecutor = new FightBattleHitExecutor(formula, settlement);
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                units.Add(unit);
                unitById.Add(unit.Id, unit);
                SynchronizeTurn(unit);
            }
            if (!unitById.ContainsKey(1))
                throw new ArgumentException("Fight roster does not contain the main player unit.", nameof(checkpoint));
            turns = new FightTurnQueue(checkpoint.Timeline, Turns());
            random = new FightRandomStream(checkpoint.RandomSeed);
            if (initialize) Initialize();
        }

        public FightBattleCheckpoint State => checkpoint;

        public static FightBattleExecutor Begin(FightBattleCheckpoint checkpoint,
            FightDamageFormula formula, FightDamageSettlement settlement)
        {
            return new FightBattleExecutor(checkpoint, formula, settlement, true);
        }

        public static FightBattleExecutor Restore(FightBattleCheckpoint checkpoint,
            FightDamageFormula formula, FightDamageSettlement settlement)
        {
            return new FightBattleExecutor(checkpoint, formula, settlement, false);
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
            var before = new Dictionary<int, long>();
            for (int i = 0; i < units.Count; i++) before[units[i].Id] = units[i].Hp;
            var movements = new List<FightBattleMovementEvent>();
            var casts = new List<FightBattleSkillCastEvent>();
            var damageEvents = new List<FightBattleDamageEvent>();
            var performedTargets = new List<FightBattleUnitState>();
            var checkedSlots = new HashSet<int>();
            float stepDuration = 0f;
            float stepActionDuration = -1f;

            while (true)
            {
                FightBattleSkillDefinition skill = FightBattleTargeting.FindReadySkill(
                    actor, units, actorRound, excludedSlots: checkedSlots);
                if (skill == null) break;

                List<FightBattleUnitState> targets = FightBattleTargeting.SelectInSkillRange(actor,
                    units, skill);
                if (targets.Count == 0 && MoveIntoSkillRange(actor, skill, movements))
                    targets = FightBattleTargeting.SelectInSkillRange(actor, units, skill);
                if (targets.Count == 0)
                {
                    checkedSlots.Add(skill.Slot);
                    continue;
                }

                ExecuteSkill(actor, skill, targets, damageEvents, out float castLastDelay);
                skillState.RunSkill(skill.Slot);
                checkpoint.Statistics.RecordCast(actor.Id, skill.EntityId, skill.ItemId);
                stepDuration = Math.Max(stepDuration, ResolveDuration(skill, castLastDelay));
                float actionDuration = ResolveActionDuration(skill, castLastDelay);
                if (actionDuration >= 0)
                    stepActionDuration = Math.Max(stepActionDuration, actionDuration);
                checkedSlots.Add(skill.Slot);
                performedTargets.AddRange(targets);
                var cast = new FightBattleSkillCastEvent
                {
                    Slot = skill.Slot,
                    SkillEntityId = skill.EntityId,
                    SkillItemId = skill.ItemId
                };
                foreach (FightBattleUnitState target in targets) cast.TargetIds.Add(target.Id);
                casts.Add(cast);
            }

            FightBattleSkillDefinition eventSkill = null;
            if (casts.Count > 0)
                eventSkill = FindSkillBySlot(actor, casts[0].Slot);
            // No releaseable skill means no action this turn. The executor deliberately does not
            // substitute an unconfigured basic attack: official units always release configured
            // skills, and a made-up attack would bypass range, cost and hit-graph rules.

            skillState.FinishRound();
            turn.ActionRound = actor.SkillState.Round;
            SynchronizeTurn(actor);
            turns.Finish(turn);

            SpawnNextWaveIfCleared();
            RefreshTurnUnits();
            UpdateOutcome(globalRound);
            var stepEvent = CreateEvent(actor, eventSkill, performedTargets, before,
                globalRound, movements, casts, damageEvents, stepDuration, stepActionDuration);
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
                new FightSkillState(unit.SkillState).Initialize(unit.SkillCooldownChangeAtStart);

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
            List<FightBattleUnitState> targets, List<FightBattleDamageEvent> damageEvents,
            out float lastDelay)
        {
            FightDamageFalloff.BeginCast(skill, ref checkpoint.FalloffSerial);
            IReadOnlyList<FightBattleHitDefinition> hits = skill.Hits;
            if (skill.HitGraphRoots.Count > 0)
            {
                var collected = new List<FightBattleHitDefinition>();
                foreach (FightBattleHitRoot root in skill.HitGraphRoots)
                {
                    List<FightBattleHitDefinition> rootHits = FightBattleHitCollector.Collect(
                        skill.HitGraphEntities, root.EntityId);
                    if (rootHits.Count > 0)
                    {
                        // The delay and anchor belong to the reference edge, not to the shared hit
                        // entity, so the first node of this root takes the configured values.
                        rootHits[0].Delay = root.Delay;
                        rootHits[0].Anchor = root.Anchor;
                        rootHits[0].IgnoreAICount = root.IgnoreAICount;
                    }
                    collected.AddRange(rootHits);
                }
                hits = collected;
            }
            else if (skill.HitGraphRootEntityIds.Count > 0)
            {
                var collected = new List<FightBattleHitDefinition>();
                foreach (int rootEntityId in skill.HitGraphRootEntityIds)
                    collected.AddRange(FightBattleHitCollector.Collect(skill.HitGraphEntities, rootEntityId));
                hits = collected;
            }
            else if (skill.HitGraphRootEntityId > 0)
                hits = FightBattleHitCollector.Collect(skill.HitGraphEntities, skill.HitGraphRootEntityId);

            lastDelay = 0f;
            foreach (FightBattleHitDefinition hit in hits)
            {
                if (hit.Delay > lastDelay) lastDelay = hit.Delay;
                List<FightBattleUnitState> hitTargets = FightBattleTargeting.SelectHitTargets(targets,
                    hit.TargetMode, hit.RandomCount, random);
                foreach (FightBattleUnitState target in hitTargets)
                {
                    long beforeHp = target.Hp;
                    int damageId = NextDamageId();
                    FightBattleHitResult resolved = hitExecutor.Apply(random, damageId, actor, target,
                        skill, hit, 0, false, 0.0, new List<int>());
                    var damage = new FightBattleDamageEvent
                    {
                        DamageId = damageId,
                        SourceId = actor.Id,
                        TargetId = target.Id,
                        SkillEntityId = skill.EntityId,
                        SkillItemId = skill.ItemId,
                        HitEntityId = hit.EntityId,
                        DamageType = hit.DamageType,
                        Delay = hit.Delay,
                        Anchor = hit.Anchor,
                        IsCure = resolved.IsCure,
                        Critical = resolved.Type.Critical,
                        Block = resolved.Type.Block,
                        Dodge = resolved.Type.Dodge,
                        Immunity = resolved.Type.Immunity,
                        Blinding = resolved.Type.Blinding,
                        Damage = resolved.Settlement == null ? 0 : resolved.Settlement.RealDamageValue,
                        AbsoluteDamage = resolved.Formula == null ? 0 : resolved.Formula.AbsoluteDamage,
                        BeforeHp = beforeHp,
                        AfterHp = target.Hp,
                        MaxHp = target.MaxHp
                    };
                    damageEvents.Add(damage);
                    checkpoint.Statistics.RecordHit(damage);
                }
            }
            FightDamageFalloff.EndCast(units, skill);
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
            List<FightBattleDamageEvent> damageEvents, float skillDuration, float actionDuration)
        {
            float movementSeconds = 0f;
            if (movements != null)
                foreach (FightBattleMovementEvent movement in movements)
                    if (movement != null && movement.Path != null && movement.Path.Count > 1)
                        movementSeconds = Math.Max(movementSeconds,
                            (movement.Path.Count - 1) * MovementSecondsPerCell);
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
                Duration = Math.Min(10f, Math.Max(0.2f, duration)),
                Movements = movements,
                SkillCasts = casts,
                DamageEvents = damageEvents ?? new List<FightBattleDamageEvent>()
            };
            foreach (FightBattleUnitState target in targets)
                if (!result.TargetIds.Contains(target.Id)) result.TargetIds.Add(target.Id);
            foreach (FightBattleUnitState unit in units)
                if (before[unit.Id] != unit.Hp)
                    result.HpChanges.Add(new FightBattleHpChange
                    {
                        UnitId = unit.Id,
                        Before = before[unit.Id],
                        After = unit.Hp,
                        MaxHp = unit.MaxHp
                    });
            return result;
        }

        private bool MoveIntoSkillRange(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, List<FightBattleMovementEvent> movements)
        {
            FightBattleGrid grid = checkpoint.Grid;
            if (grid == null || actor.MoveDist <= 0 || skill.Range == null || skill.Range.Count == 0)
                return false;

            List<FightBattleUnitState> targets = FightBattleTargeting.Select(actor, units,
                skill.TargetType, skill: skill);
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
            checkpoint.Finished = victory || defeated || timeout;
            checkpoint.Outcome = new FightBattleOutcome
            {
                Finished = checkpoint.Finished,
                Won = victory,
                Reason = victory ? 1 : defeated ? 2 : timeout ? 3 : 0,
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

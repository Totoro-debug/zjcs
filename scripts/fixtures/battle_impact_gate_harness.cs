using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zjcs.Combat.Official;

public static class BattleImpactGateHarness
{
    private sealed class Pending
    {
        public FightBattlePendingImpact Impact;
        public ManualResetEventSlim Released = new ManualResetEventSlim(false);
    }
    private sealed class Gate : IFightBattleImpactGate, IFightBattleCastPreparationObserver, IFightBattleImpactSettlementObserver
    {
        public readonly ConcurrentQueue<(int SkillItemId, bool HasStatusResult, bool StatusTriggered)> Settlements =
            new ConcurrentQueue<(int, bool, bool)>();
        public void ImpactSettled(FightBattlePendingImpact impact, FightBattleDamageEvent damage)
            => Settlements.Enqueue((impact.SkillItemId, damage.HasDamageTriggerStatus, damage.IsDamageTriggerStatus));
        public readonly BlockingCollection<Pending> Items = new BlockingCollection<Pending>();
        public readonly ConcurrentQueue<FightBattleSkillCastEvent> Prepared = new ConcurrentQueue<FightBattleSkillCastEvent>();
        public void CastPrepared(FightBattleSkillCastEvent cast) { Prepared.Enqueue(cast); }
        public void AwaitImpact(FightBattlePendingImpact impact)
        {
            var pending = new Pending { Impact = impact };
            Items.Add(pending);
            if (!pending.Released.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The impact was never released by the presentation.");
        }
        public Pending Next()
        {
            if (!Items.TryTake(out var pending, TimeSpan.FromSeconds(20)))
                throw new TimeoutException("The expected impact was not emitted.");
            return pending;
        }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Impact gate: " + message); }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new Dictionary<int, long> { {1, 1000}, {2, 10}, {3, unit.MaxHp}, {4, unit.Turn.Speed} })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        {
            var cast = new FightBattleSkillCastEvent { SkillEntityId = 10100 };
            using var preparationLatch = new FightBattleImpactLatch();
            var prepare = Task.Run(() => preparationLatch.CastPrepared(cast));
            FightBattlePreparedCast preparation = null;
            Check(SpinWait.SpinUntil(() => preparationLatch.TryTakePreparation(out preparation), 20000)
                && ReferenceEquals(preparation.Cast, cast), "The host did not receive the actual cast preparation");
            Check(!prepare.Wait(50), "The executor continued while the host was copying cast data");
            preparationLatch.CompletePreparation(preparation);
            Check(prepare.Wait(20000), "Completing the cast preparation did not resume the executor");
            bool duplicate = false;
            try { preparationLatch.CompletePreparation(preparation); }
            catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "One cast preparation was completed more than once");
            var canceled = Task.Run(() => {
                try { preparationLatch.CastPrepared(new FightBattleSkillCastEvent()); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => preparationLatch.TryTakePreparation(out _), 20000),
                "The pending preparation never reached the host");
            preparationLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel cast preparation");
        }
        {
            using var completionLatch = new FightBattleImpactLatch();
            completionLatch.CastCompleted(new FightBattleSkillCastEvent());
            Check(!completionLatch.TryTakeCastCompletion(out _), "A rejected cast was reported as started");
            var cast = new FightBattleSkillCastEvent();
            var prepare = Task.Run(() => completionLatch.CastPrepared(cast));
            FightBattlePreparedCast preparation = null;
            Check(SpinWait.SpinUntil(() => completionLatch.TryTakePreparation(out preparation), 20000),
                "The completion fixture did not prepare its cast");
            completionLatch.CompletePreparation(preparation);
            Check(prepare.Wait(20000), "The completion fixture did not release preparation");
            var canceled = Task.Run(() => {
                try { completionLatch.CastCompleted(cast); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => completionLatch.TryTakeCastCompletion(out _), 20000),
                "The cast completion did not reach the host");
            completionLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel cast completion");
        }
        {
            using var selectionLatch = new FightBattleImpactLatch();
            var cast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot();
            var canceled = Task.Run(() => {
                try { selectionLatch.HitSelected(cast, selection); return false; }
                catch (OperationCanceledException) { return true; }
            });
            FightBattleSelectedHit selected = null;
            Check(SpinWait.SpinUntil(() => selectionLatch.TryTakeSelection(out selected), 20000)
                && ReferenceEquals(selected.Cast, cast) && ReferenceEquals(selected.Selection, selection)
                && !canceled.Wait(50), "The selected hit was not held for its host");
            selectionLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel hit selection");
        }
        {
            using var settlementLatch = new FightBattleImpactLatch();
            var impact = new FightBattlePendingImpact();
            var damage = new FightBattleDamageEvent { BeforeHp = 100, AfterHp = 70 };
            var notify = Task.Run(() => settlementLatch.ImpactSettled(impact, damage));
            FightBattleSettledImpact settlement = null;
            Check(SpinWait.SpinUntil(() => settlementLatch.TryTakeSettlement(out settlement), 20000)
                && ReferenceEquals(settlement.Impact, impact) && ReferenceEquals(settlement.Damage, damage),
                "The host did not receive the actual settled hit");
            Check(!notify.Wait(50), "The executor continued while the host was copying the settled hit");
            settlementLatch.CompleteSettlement(settlement);
            Check(notify.Wait(20000), "Completing the settlement did not resume the executor");
            bool duplicate = false;
            try { settlementLatch.CompleteSettlement(settlement); }
            catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "One settlement was completed more than once");
            var canceled = Task.Run(() => {
                try { settlementLatch.ImpactSettled(impact, damage); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => settlementLatch.TryTakeSettlement(out _), 20000),
                "The pending settlement never reached the host");
            settlementLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel settlement");
        }
        {
            var cast = new FightBattleSkillCastEvent();
            var otherCast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot();
            selection.EffectCenters.Add(new FightGridPoint(4, 5));
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2, HasScopeIndex = true, ScopeIndex = 0 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 3, HasScopeIndex = true, ScopeIndex = 0 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2, HasScopeIndex = true, ScopeIndex = 1 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 4 });
            FightBattlePendingImpact Pending(int index) => new FightBattlePendingImpact {
                Cast = cast, HitSelection = selection, TargetId = selection.Targets[index].TargetId,
                EffectCenterIndex = 0, OccurrenceIndex = index,
                HasScopeIndex = selection.Targets[index].HasScopeIndex, ScopeIndex = selection.Targets[index].ScopeIndex };
            var latch = new FightBattleImpactLatch();
            var unresolved = Pending(3); unresolved.ScopeIdentityUnresolved = true;
            bool rejected = false;
            try { latch.AwaitImpact(unresolved); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !latch.TryTake(out _), "An unresolved aggregate scope must not settle at the centre");
            var one = Task.Run(() => latch.AwaitImpact(Pending(0)));
            var two = Task.Run(() => latch.AwaitImpact(Pending(1)));
            var queued = new List<FightBattlePendingImpact>();
            Check(SpinWait.SpinUntil(() => { while (latch.TryTake(out var item)) queued.Add(item); return queued.Count == 2; }, 20000),
                "The host did not receive both pending targets");
            Check(latch.RecordArrival(otherCast, selection, 0, 0) && latch.RecordArrival(cast, selection, 0, 2),
                "Independent arrivals were not recorded");
            Check(!one.Wait(50) && !two.Wait(50), "A different cast or scope released the pending targets");
            Check(latch.RecordArrival(cast, selection, 0, 0) && !latch.RecordArrival(cast, selection, 0, 0),
                "A repeated scope arrival must remain one event");
            Check(Task.WaitAll(new[] { one, two }, 20000), "A shared scope arrival did not release both affected targets");
            Check(latch.RecordArrival(cast, selection, 0, 1), "An early scope arrival was not recorded");
            var early = Task.Run(() => latch.AwaitImpact(Pending(2)));
            Check(early.Wait(20000), "An arrival before the target request was lost");
            var center = Task.Run(() => latch.AwaitImpact(Pending(3)));
            Check(!center.Wait(50) && latch.RecordArrival(cast, selection, 0, -1) && center.Wait(20000),
                "A centre hit must wait for its own original arrival");
            var waitingCast = new FightBattleSkillCastEvent();
            var canceled = Task.Run(() => {
                var pending = Pending(0); pending.Cast = waitingCast;
                try { latch.AwaitImpact(pending); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => { while (latch.TryTake(out var item)) if (ReferenceEquals(item.Cast, waitingCast)) return true; return false; }, 20000),
                "The cancellation fixture never reached its wait");
            latch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result && !latch.RecordArrival(cast, selection, 0, 0),
                "Disposing the step must cancel its waiter and ignore late arrivals");
        }
        int impacts = 0;
        foreach (var setup in new[] { new[] {10011,10630,1,3}, new[] {10015,10670,2,1}, new[] {10028,10800,3,1} })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = 3;
            var target = Unit(2, 2, 7 + setup[2], 7, formula);
            var skill = catalog.CreateSkill(setup[1], 0, setup[0], 2, 20, 1,
                catalog.CalculateSkillProps(setup[1], 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var baseline = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var ordinary = FightBattleExecutor.Restore(baseline, formula, new FightDamageSettlement(), catalog, items);
            ordinary.Step(0, 1);
            var gate = new Gate(); executor.ImpactGate = gate;
            var step = Task.Run(() => executor.Step(0, 1));
            long previousHp = target.Hp;
            for (int index = 0; index < setup[3]; index++)
            {
                var pending = gate.Next();
                try
                {
                    var impact = pending.Impact;
                    Check(impact.SourceId == 1 && impact.TargetId == 2 && impact.SkillItemId == setup[0]
                        && impact.SkillEntityId == setup[1] && impact.HitEntityId > 0 && impact.DamageEntityId > 0
                        && impact.Cast != null && impact.HitSelection != null,
                        "The native hit lacks its original cast, hit, target or location");
                    Check(gate.Prepared.Any(cast => ReferenceEquals(cast, impact.Cast)),
                        "The impact was requested before its cast preparation notification");
                    Check(impact.OccurrenceIndex >= 0 && impact.OccurrenceIndex < impact.HitSelection.Targets.Count
                        && impact.HitSelection.Targets[impact.OccurrenceIndex].TargetId == impact.TargetId
                        && impact.HitSelection.Targets[impact.OccurrenceIndex].EffectCenterIndex == impact.EffectCenterIndex,
                        "The pending impact lost its selected occurrence or effect centre");
                    Check(target.Hp == previousHp, "HP changed before the presentation released the pending impact");
                    Check(!step.Wait(50) && target.Hp == previousHp, "A paused impact settled without its presentation event");
                }
                finally { pending.Released.Set(); }
                if (index + 1 < setup[3])
                {
                    var next = gate.Next();
                    Check(target.Hp < previousHp, "A released hit did not settle before the next impact");
                    previousHp = target.Hp;
                    gate.Items.Add(next);
                }
                impacts++;
            }
            Check(step.Wait(TimeSpan.FromSeconds(20)), "The final impact did not finish the action");
            var actual = step.Result;
            if (setup[0] == 10015 || setup[0] == 10028)
            {
                var direct = gate.Settlements.Where(value => value.SkillItemId == setup[0]).ToArray();
                Check(direct.Length == 1 && direct[0].HasStatusResult == (setup[0] == 10028)
                    && !direct[0].StatusTriggered,
                    "Only an empty original status list has a known result at the impact boundary");
            }
            Check(actual.Event.DamageEvents.Count(d => d.SkillItemId == setup[0]) == setup[3], "A hit was lost or duplicated");
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(baseline),
                "Gating changed the original deterministic result");
        }
        {
            var source = Unit(1, 1, 3, 3, formula);
            var target = Unit(2, 2, 4, 3, formula);
            var point = new FightGridPoint(0, 0);
            var hit = new FightBattleHitDefinition { ActionType = 4, TargetMode = 1,
                Scope = new List<FightGridPoint> { point, point }, ScopeEntries = new List<FightBattleHitScopeEntry> {
                    new FightBattleHitScopeEntry { Position = point, Delay = .1f },
                    new FightBattleHitScopeEntry { Position = point, Delay = .2f } } };
            var skill = new FightBattleSkillDefinition { SkillType = 0, TargetType = 1 };
            var occurrences = FightBattleHitPatterns.For(hit).Plan(new FightHitPatternContext {
                Source = source, Units = new[] { source, target }, Skill = skill, Hit = hit,
                Targets = new List<FightBattleUnitState> { target }, Primary = target,
                Cast = FightBattleSkillCastPresentation.Capture(source, skill, new List<FightBattleUnitState> { target }),
                Random = new FightRandomStream(7) });
            Check(occurrences.Count == 2 && occurrences[0].ScopeIndex == 0 && occurrences[1].ScopeIndex == 1
                && occurrences[0].GridPosition.Equals(occurrences[1].GridPosition),
                "Repeated authored scope cells must retain separate occurrence identities");
        }
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, 10, formula); actor.Direction = 3;
            var skill = catalog.CreateSkill(10590, 0, 10043, 2, 20, 1,
                catalog.CalculateSkillProps(10590, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.Add(actor); int unitId = 2;
            foreach (var offset in new[] { new[] {0,0}, new[] {-1,0}, new[] {1,0}, new[] {0,-1}, new[] {0,1} })
                cp.Units.Add(Unit(unitId++, 2, 13 + offset[0], 10 + offset[1], formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(u => u.Side == 2).Select(u => u.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            using var latch = new FightBattleImpactLatch();
            executor.ImpactGate = latch; var task = Task.Run(() => executor.Step(0, 1));
            FightBattlePreparedCast preparation = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out preparation), 20000)
                && preparation.Cast.SkillItemId == 10043 && !task.IsCompleted,
                "The original cross-scope cast was not prepared before its hits");
            latch.CompletePreparation(preparation);
            FightBattleSelectedHit selected = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeSelection(out selected), 20000)
                && ReferenceEquals(selected.Cast, preparation.Cast) && selected.Selection.HitEntityId == 10591
                && selected.Selection.Targets.Count == 5 && !task.IsCompleted,
                "The host did not receive the original five-target selection before settlement");
            latch.CompleteSelection(selected);
            bool duplicateSelection = false;
            try { latch.CompleteSelection(selected); }
            catch (InvalidOperationException) { duplicateSelection = true; }
            Check(duplicateSelection, "One hit selection was completed more than once");
            var seen = new HashSet<int>();
            var scopeIndices = new HashSet<int>();
            for (int index = 0; index < 5; index++)
            {
                FightBattlePendingImpact impact = null;
                Check(SpinWait.SpinUntil(() => latch.TryTake(out impact), 20000), "A cross-scope impact was not offered to the host");
                Check(ReferenceEquals(impact.HitSelection, selected.Selection),
                    "The pending hit did not belong to the selected original view");
                var occurrence = impact.HitSelection.Targets[impact.OccurrenceIndex];
                Check(impact.SkillItemId == 10043 && seen.Add(impact.TargetId)
                    && occurrence.TargetId == impact.TargetId && occurrence.EffectCenterIndex == impact.EffectCenterIndex
                    && impact.HasScopeIndex && occurrence.HasScopeIndex && occurrence.ScopeIndex == impact.ScopeIndex
                    && scopeIndices.Add(impact.ScopeIndex)
                    && occurrence.HasGridPosition && occurrence.GridX == impact.GridX && occurrence.GridY == impact.GridY,
                    "A cross-scope impact lost the actual target, body cell or centre identity");
                Check(cp.Units.Single(u => u.Id == impact.TargetId).Hp == 1000000,
                    "A cross-scope target took damage before its own impact");
                Check(latch.RecordArrival(impact.Cast, impact.HitSelection, impact.EffectCenterIndex, impact.ScopeIndex),
                    "The matching scope arrival was not accepted");
                FightBattleSettledImpact settled = null;
                Check(SpinWait.SpinUntil(() => latch.TryTakeSettlement(out settled), 20000)
                    && ReferenceEquals(settled.Impact, impact) && settled.Damage.TargetId == impact.TargetId
                    && settled.Damage.BeforeHp == 1000000 && settled.Damage.AfterHp < 1000000
                    && cp.Units.Single(u => u.Id == impact.TargetId).Hp == settled.Damage.AfterHp
                    && !task.IsCompleted, "The host did not observe actual HP at the matching impact");
                latch.CompleteSettlement(settled);
            }
            FightBattleCompletedCast completedCast = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeCastCompletion(out completedCast), 20000)
                && ReferenceEquals(completedCast.Cast, preparation.Cast) && completedCast.Cast.Presentation.Duration >= 0f
                && !task.IsCompleted, "The completed cast duration was not offered before the step finished");
            latch.CompleteCast(completedCast);
            bool duplicateCompletion = false;
            try { latch.CompleteCast(completedCast); }
            catch (InvalidOperationException) { duplicateCompletion = true; }
            Check(duplicateCompletion, "One cast completion was applied more than once");
            Check(task.Wait(TimeSpan.FromSeconds(20)) && task.Result.Event.DamageEvents.Count(d => d.SkillItemId == 10043) == 5,
                "Cross-scope impacts were lost or duplicated");
            var saved = FightBattleCheckpointSerializer.Save(cp);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var recorded = restored.Receipts.Last().Event.SkillCasts.Single(c => c.SkillItemId == 10043)
                .HitSelections.Single(s => s.HitEntityId == 10591);
            Check(recorded.Targets.Count == 5 && recorded.Targets.All(t => t.HasScopeIndex)
                && recorded.Targets.Select(t => t.ScopeIndex).Distinct().Count() == 5,
                "Original scope identities did not survive the checkpoint");
            string legacy = System.Text.RegularExpressions.Regex.Replace(saved,
                ",\\\"HasScopeIndex\\\":(?:true|false),\\\"ScopeIndex\\\":-?[0-9]+", "");
            Check(legacy != saved, "Legacy scope fixture did not remove the new fields");
            var oldRecorded = FightBattleCheckpointSerializer.Load(legacy).Receipts.Last().Event.SkillCasts
                .Single(c => c.SkillItemId == 10043).HitSelections.Single(s => s.HitEntityId == 10591);
            Check(oldRecorded.Targets.All(t => !t.HasScopeIndex),
                "A legacy checkpoint invented scope indices that were not recorded");
        }
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 10, 7, formula);
            var skill = catalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                catalog.CalculateSkillProps(10800, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            string committed = FightBattleCheckpointSerializer.Save(cp);
            var ordinary = FightBattleCheckpointSerializer.Load(committed);
            FightBattleExecutor.Restore(ordinary, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            FightBattleExecutor Restore(FightBattleCheckpoint state) =>
                FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
            using (var isolated = new FightBattleIsolatedStep(cp, Restore, 0, 1))
            {
                FightBattlePreparedCast preparation = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakePreparation(out preparation), 20000),
                    "The isolated step did not offer the actual cast");
                Check(FightBattleCheckpointSerializer.Save(cp) == committed && target.Hp == 1000000,
                    "A running step changed its committed checkpoint");
                isolated.Latch.CompletePreparation(preparation);
                FightBattleSelectedHit selected = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSelection(out selected), 20000),
                    "The isolated step did not offer the selected hit");
                isolated.Latch.CompleteSelection(selected);
                FightBattlePendingImpact impact = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTake(out impact), 20000),
                    "The isolated step did not await the impact");
                isolated.Latch.RecordArrival(impact.Cast, impact.HitSelection, impact.EffectCenterIndex,
                    impact.HasScopeIndex ? impact.ScopeIndex : -1);
                FightBattleSettledImpact settled = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSettlement(out settled), 20000)
                    && settled.Damage.AfterHp < 1000000 && target.Hp == 1000000
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "Impact settlement escaped into the committed checkpoint");
                isolated.Latch.CompleteSettlement(settled);
                FightBattleCompletedCast castCompletion = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeCastCompletion(out castCompletion), 20000)
                    && ReferenceEquals(castCompletion.Cast, preparation.Cast),
                    "The isolated cast did not report its final duration");
                isolated.Latch.CompleteCast(castCompletion);
                FightBattleExecutor completedExecutor = null; FightBattleExecutionResult completed = null;
                Check(SpinWait.SpinUntil(() => isolated.TryTakeCompleted(out completedExecutor, out completed), 20000)
                    && completed != null && ReferenceEquals(completedExecutor.State, completed.Checkpoint)
                    && completedExecutor.ImpactGate == null && FightBattleCheckpointSerializer.Save(cp) == committed
                    && FightBattleCheckpointSerializer.Save(completed.Checkpoint) == FightBattleCheckpointSerializer.Save(ordinary),
                    "The isolated completed step changed deterministic results or its original checkpoint");
                bool duplicateTransfer = false;
                try { isolated.TryTakeCompleted(out _, out _); }
                catch (InvalidOperationException) { duplicateTransfer = true; }
                Check(duplicateTransfer, "The isolated result was transferred more than once");
            }
            bool aliased = false;
            try { using var invalid = new FightBattleIsolatedStep(cp, _ => Restore(cp), 0, 1); }
            catch (InvalidOperationException) { aliased = true; }
            Check(aliased && FightBattleCheckpointSerializer.Save(cp) == committed,
                "An executor bound to the committed checkpoint was accepted");
            using (var canceled = new FightBattleIsolatedStep(cp, Restore, 0, 1))
            {
                Check(SpinWait.SpinUntil(() => canceled.Latch.TryTakePreparation(out _), 20000),
                    "The canceled isolated step did not reach a safe rendezvous");
                canceled.Dispose();
                Check(SpinWait.SpinUntil(() => canceled.IsCompleted, 20000)
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "Canceling a partial isolated step changed the committed checkpoint");
            }
        }
        foreach (bool removed in new[] { false, true })
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 8, 7, formula);
            var skill = catalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                catalog.CalculateSkillProps(10600, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.CostType = 1; skill.SkillCost = 100;
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var gate = new Gate(); executor.ImpactGate = gate; var task = Task.Run(() => executor.Step(0, 1));
            var pending = gate.Next();
            Check(gate.Prepared.TryPeek(out var started) && ReferenceEquals(started, pending.Impact.Cast)
                && started.HasSkillCostHp && started.SkillCostBeforeHp == 1000000
                && started.SkillCostAfterHp == 999900 && actor.Hp == 999900,
                "Cast preparation must follow original HP cost and precede the pending hit");
            target.Hp = target.Turn.Hp = removed ? 0 : 500000;
            pending.Released.Set();
            Check(task.Wait(TimeSpan.FromSeconds(20)), "Changed target did not finish the action");
            var hits = task.Result.Event.DamageEvents.Where(d => d.SkillItemId == 10008 && !d.IsSkillCost).ToArray();
            Check(removed ? hits.Length == 0 : hits.Length == 1 && hits[0].BeforeHp == 500000 && hits[0].AfterHp < 500000,
                "The impact did not use the target's live state when the presentation reached it");
        }
        return "Impact gate passed: " + impacts + " held impacts across three-hit combo, jump slash and fireball; five cross-scope identities, skill cost and exact ungated results verified.";
    }
}

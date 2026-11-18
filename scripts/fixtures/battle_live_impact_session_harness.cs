using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.World.NativeTerrain.Common
{
    public struct Pos2d { public int X, Y; public Pos2d(int x, int y) { X = x; Y = y; } }
}
namespace Zjcs.Official.StoryPlayables
{
    public struct EcEntityIdentifier { public int Value; public EcEntityIdentifier(int value) { Value = value; } }
    public class MyPlayable { public string name; }
}
namespace UnityEngine { public class GameObject { public string name; } }
namespace Zjcs.Presentation.Combat
{
    internal sealed class OfficialBattleMountainWaveImpact
    {
        internal static OfficialBattleMountainWaveImpact Attach(MyPlayable playable, Action<int> arrival) => null;
        internal void Detach() { }
    }
    // Drives the production session with real battle rules and a controlled native-view boundary.
    public sealed class OfficialBattleSkillEntityPlayback
    {
        public event Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot, int, Pos2d, int> HitSelectionViewArrived;
        public event Action<FightBattleSkillCastEvent, EcEntityIdentifier, GameObject, MyPlayable> SkillGraphPlayableStarted;
        public FightBattleSkillCastEvent Cast;
        public FightBattleHitSelectionSnapshot Selection;
        public float SelectionElapsed, CompletionElapsed;
        public int Damages, Completions;
        public bool ArriveImmediately, RejectDamage;
        public FightBattleDamageEvent LastDamage;
        public void QueueSelectedHit(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            FightBattleHitSelectionSnapshot selection, float elapsed)
        {
            Cast = cast; Selection = selection; SelectionElapsed = elapsed;
            if (ArriveImmediately) {
                var target = selection.Targets.Single(t => t.TargetId == 2);
                Arrive(target.HasScopeIndex ? target.ScopeIndex : -1);
            }
        }
        public bool QueueSettledDamage(FightBattleSkillCastEvent cast, EcEntityIdentifier id, FightBattleDamageEvent damage)
        { if (!damage.HasDamageTriggerStatus || RejectDamage) return false; Damages++; LastDamage = damage; return ReferenceEquals(cast, Cast); }
        public void CompleteLiveCast(FightBattleSkillCastEvent cast, EcEntityIdentifier id, float elapsed)
        { if (!ReferenceEquals(cast, Cast)) throw new InvalidOperationException("Wrong completed cast"); Completions++; CompletionElapsed = elapsed; }
        public void Arrive(int scope) => HitSelectionViewArrived?.Invoke(Cast, Selection, 0, new Pos2d(10, 7), scope);
        public void StartGraph(EcEntityIdentifier id, string prefab = null, string leaf = null)
            => SkillGraphPlayableStarted?.Invoke(Cast, id, new GameObject { name = prefab }, new MyPlayable { name = leaf });
    }

    public static class BattleLiveImpactSessionHarness
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Live impact session: " + message); }
        private static FightBattleUnitState Unit(int id, int side, int x, FightDamageFormula formula)
        {
            var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
                Level = 50, SubRank = 5, X = x, Y = 7, Direction = 3, Hp = 1000000, MaxHp = 1000000,
                LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
            foreach (var prop in new Dictionary<int, long> { {1,1000}, {2,10}, {3,unit.MaxHp}, {4,unit.Turn.Speed} })
                unit.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
            foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
            return unit;
        }
        public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, formula); var target = Unit(2, 2, 10, formula);
            var skill = catalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                catalog.CalculateSkillProps(10800, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            string committed = FightBattleCheckpointSerializer.Save(cp);
            var ordinary = FightBattleCheckpointSerializer.Load(committed);
            FightBattleExecutor.Restore(ordinary, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            var views = new OfficialBattleSkillEntityPlayback();
            using var session = new OfficialBattleLiveImpactSession(cp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), views, 0, 1);
            int attempts = 0; bool allowStart = false, hpRefreshed = false, reentrantRejected = false, transferReentrantRejected = false;
            bool Start(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
            { attempts++; id = new EcEntityIdentifier(42); return allowStart; }
            void Refresh(FightBattleDamageEvent damage)
            {
                hpRefreshed = target.Hp == 1000000 && damage.BeforeHp == 1000000 && damage.AfterHp < 1000000
                    && views.Damages == 1 && !session.IsCompleted;
                try { session.Pump(0f, Start); }
                catch (InvalidOperationException) { reentrantRejected = true; }
                try { session.TryTakeCompleted(out _, out _); }
                catch (InvalidOperationException) { transferReentrantRejected = true; }
            }
            Check(SpinWait.SpinUntil(() => { session.Pump(0f, Start, Refresh); return attempts > 0; }, 20000),
                "The actual cast preparation was not offered");
            session.Pump(1f, Start, Refresh);
            Check(target.Hp == 1000000 && views.Selection == null, "A deferred cast ran before its host started it");
            allowStart = true;
            Check(SpinWait.SpinUntil(() => { session.Pump(0f, Start, Refresh); return views.Selection != null; }, 20000),
                "The started cast did not deliver its actual hit selection");
            Check(views.SelectionElapsed >= 0f && target.Hp == 1000000 && views.Damages == 0,
                "The local result changed before the native hit arrival");
            var occurrence = views.Selection.Targets.Single(t => t.TargetId == 2);
            int expectedScope = occurrence.HasScopeIndex ? occurrence.ScopeIndex : -1;
            views.Arrive(expectedScope + 1);
            Check(views.Damages == 0 && target.Hp == 1000000,
                "An unrelated scope arrival released the pending impact");
            views.Arrive(expectedScope);
            Check(hpRefreshed && views.Damages == 1,
                "The matching arrival returned before its original damage and HP were displayed");
            Check(SpinWait.SpinUntil(() => { session.Pump(.001f, Start, Refresh); return session.IsCompleted; }, 20000)
                && hpRefreshed && reentrantRejected && transferReentrantRejected && views.Damages == 1 && views.Completions == 1
                && views.CompletionElapsed >= views.SelectionElapsed && target.Hp == 1000000,
                "Damage, HP and cast completion were not coordinated at the actual arrival");
            Check(session.TryTakeCompleted(out var completedExecutor, out var completed)
                && ReferenceEquals(completedExecutor.State, completed.Checkpoint) && completedExecutor.ImpactGate == null
                && FightBattleCheckpointSerializer.Save(cp) == committed
                && FightBattleCheckpointSerializer.Save(completed.Checkpoint) == FightBattleCheckpointSerializer.Save(ordinary),
                "The transferred result changed the original checkpoint or deterministic outcome");
            bool duplicate = false;
            try { session.TryTakeCompleted(out _, out _); }
            catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "The live result was transferred twice");
            var earlyViews = new OfficialBattleSkillEntityPlayback { ArriveImmediately = true };
            using (var early = new OfficialBattleLiveImpactSession(cp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), earlyViews, 0, 1))
            {
                bool StartEarly(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(44); return true; }
                Check(SpinWait.SpinUntil(() => { early.Pump(0f, StartEarly); return early.IsCompleted; }, 20000)
                    && earlyViews.Damages == 1 && earlyViews.Completions == 1
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "An arrival during selection delivery did not settle without a lost wakeup");
            }

            var guardedViews = new OfficialBattleSkillEntityPlayback();
            var guardedHealth = new FightBattleLiveHealthProjection(cp);
            using (var guarded = new OfficialBattleLiveImpactSession(cp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), guardedViews, 0, 1))
            {
                bool allowHealth = false;
                bool StartGuarded(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(46); return true; }
                bool CanShow(FightBattleDamageEvent damage) => allowHealth && guardedHealth.CanApplyDamage(damage);
                void ShowHealth(FightBattleDamageEvent damage)
                { Check(guardedHealth.TryApplyDamage(damage), "Validated health did not apply"); }
                Check(SpinWait.SpinUntil(() => { guarded.Pump(0f, StartGuarded, ShowHealth, null, null, CanShow);
                    return guardedViews.Selection != null; }, 20000), "The guarded hit was not selected");
                var guardedTarget = guardedViews.Selection.Targets.Single(t => t.TargetId == 2);
                guardedViews.Arrive(guardedTarget.HasScopeIndex ? guardedTarget.ScopeIndex : -1);
                Check(guarded.PendingSettlement != null && guardedViews.Damages == 0
                    && guardedHealth.Find(2).Hp == 1000000 && target.Hp == 1000000,
                    "A rejected health projection must hold the actual damage before native display");
                allowHealth = true;
                Check(SpinWait.SpinUntil(() => { guarded.Pump(0f, StartGuarded, ShowHealth, null, null, CanShow);
                    return guarded.IsCompleted; }, 20000) && guardedViews.Damages == 1
                    && guardedHealth.Find(2).Hp < 1000000 && target.Hp == 1000000,
                    "A valid health projection must show the held damage and keep the checkpoint isolated");
            }

            var graphViews = new OfficialBattleSkillEntityPlayback();
            using (var graphSession = new OfficialBattleLiveImpactSession(cp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), graphViews, 0, 1))
            {
                bool StartGraphCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(47); return true; }
                Check(SpinWait.SpinUntil(() => { graphSession.Pump(0f, StartGraphCast); return graphViews.Selection != null; }, 20000),
                    "The graph fixture did not offer its actual selection");
                var hit = graphViews.Selection.Targets.Single(t => t.TargetId == 2);
                int scope = hit.HasScopeIndex ? hit.ScopeIndex : -1;
                bool outsideRejected = false, otherCastRejected = false, otherScopeRejected = false, otherIdRejected = false;
                try { graphSession.RecordGraphImpact(graphViews.Cast, graphViews.Selection, 0, scope); }
                catch (InvalidOperationException) { outsideRejected = true; }
                graphSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                    try { graphSession.RecordGraphImpact(new FightBattleSkillCastEvent(), graphViews.Selection, 0, scope); }
                    catch (InvalidOperationException) { otherCastRejected = true; }
                    try { graphSession.RecordGraphImpact(cast, graphViews.Selection, 0, scope + 1); }
                    catch (InvalidOperationException) { otherScopeRejected = true; }
                    if (id.Value != 47) {
                        try { graphSession.RecordGraphImpact(cast, graphViews.Selection, 0, scope); }
                        catch (InvalidOperationException) { otherIdRejected = true; }
                    } else graphSession.RecordGraphImpact(cast, graphViews.Selection, 0, scope);
                };
                graphViews.StartGraph(new EcEntityIdentifier(48));
                Check(outsideRejected && otherCastRejected && otherScopeRejected && otherIdRejected && graphViews.Damages == 0,
                    "An unrelated cast, scope, entity or non-playback call released the graph impact");
                graphViews.StartGraph(new EcEntityIdentifier(47));
                Check(graphViews.Damages == 1 && SpinWait.SpinUntil(() => graphSession.IsCompleted, 20000)
                    && graphViews.Completions == 1 && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "The matching original graph callback did not settle in its playback frame");
            }

            var jumpCp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var jumper = Unit(1, 1, 7, formula); var jumpTarget = Unit(2, 2, 9, formula);
            var jump = catalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                catalog.CalculateSkillProps(10670, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            jump.LimitedTimes = 1; jumper.Skills.Add(jump); jumper.SkillState.Slots.Add(catalog.CreateSkillSlot(jump, 0));
            jumpCp.Units.AddRange(new[] { jumper, jumpTarget }); var jumpWave = new FightBattleWaveState();
            jumpWave.UnitIds.Add(2); jumpCp.Waves.Add(jumpWave);
            FightBattleExecutor.Begin(jumpCp, formula, new FightDamageSettlement(), catalog, items);
            string beforeJump = FightBattleCheckpointSerializer.Save(jumpCp);
            var jumpBaseline = FightBattleCheckpointSerializer.Load(beforeJump);
            var jumpOrdinary = FightBattleExecutor.Restore(jumpBaseline, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            var expectedJumpDamage = jumpOrdinary.Event.DamageEvents.Single(d => d.SkillItemId == 10015);
            var jumpViews = new OfficialBattleSkillEntityPlayback();
            using (var jumpSession = new OfficialBattleLiveImpactSession(jumpCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), jumpViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(43); return true; }
                Check(SpinWait.SpinUntil(() => { jumpSession.Pump(0f, StartJump); return jumpViews.Selection != null; }, 20000),
                    "The status-bearing jump did not offer its selected hit");
                var hit = jumpViews.Selection.Targets.Single(t => t.TargetId == 2);
                jumpViews.StartGraph(new EcEntityIdentifier(999), "playable10670", "effect (2)");
                jumpViews.StartGraph(new EcEntityIdentifier(43), "playable10670", "effect (1)");
                Check(jumpViews.Damages == 0, "The jump cannot settle for another cast or its earlier effect");
                jumpSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => { throw new InvalidOperationException("observer failure"); };
                bool observerFailed = false;
                try { jumpViews.StartGraph(new EcEntityIdentifier(43), "playable10670", "effect (2)"); }
                catch (InvalidOperationException) { observerFailed = true; }
                Check(jumpViews.Damages == 1 && jumpViews.LastDamage.HasDamageTriggerStatus
                    && jumpViews.LastDamage.IsDamageTriggerStatus == expectedJumpDamage.IsDamageTriggerStatus
                    && jumpViews.LastDamage.DamageValue == expectedJumpDamage.DamageValue
                    && observerFailed && !jumpSession.StatusResolutionBlocked && jumpSession.PendingSettlement == null,
                    "The jump's actual status outcome and damage did not arrive in the impact frame");
                Check(SpinWait.SpinUntil(() => { jumpSession.Pump(0f, StartJump); return jumpSession.IsCompleted; }, 20000)
                    && jumpViews.Completions == 1 && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "The status-bearing hit did not finish in an isolated checkpoint");
                Check(jumpSession.TryTakeCompleted(out _, out var result)
                    && FightBattleCheckpointSerializer.Save(result.Checkpoint) == FightBattleCheckpointSerializer.Save(jumpBaseline),
                    "Resolving the original status changed the completed deterministic result");
            }
            var movingViews = new OfficialBattleSkillEntityPlayback();
            using (var movingSession = new OfficialBattleLiveImpactSession(jumpCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items),
                movingViews, 0, 1, observeMovements: true))
            {
                FightBattleSkillCastEvent startedCast = null;
                bool allowMovement = false, movementStarted = false;
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { startedCast = cast; id = new EcEntityIdentifier(51); return true; }
                bool StartJumpMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent move)
                {
                    Check(ReferenceEquals(cast, startedCast) && move.UnitId == 1 && move.MoveBehaviourId == 10672
                        && move.Path != null && move.Path.Count == 2 && move.Path[0].X == 7
                        && move.HasDirection && move.HasFacing,
                        "The movement handoff lost the original jump cast, direct path or action facing");
                    if (!allowMovement) return false;
                    movementStarted = true; return true;
                }
                Check(SpinWait.SpinUntil(() => { movingSession.Pump(0f, StartJump, null, StartJumpMovement);
                    return movingSession.PendingMovement != null; }, 20000)
                    && !movementStarted && movingViews.Selection == null && jumpTarget.Hp == 1000000
                    && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "The host must receive the actual jump movement before its selected hit");
                movingSession.Pump(.2f, StartJump, null, StartJumpMovement);
                Check(movingViews.Selection == null && !movementStarted, "A declined movement must keep the rules held");
                allowMovement = true;
                Check(SpinWait.SpinUntil(() => { movingSession.Pump(0f, StartJump, null, StartJumpMovement);
                    return movingViews.Selection != null; }, 20000) && movementStarted,
                    "Accepting the original jump action did not resume target selection");
                movingViews.StartGraph(new EcEntityIdentifier(51), "playable10670", "effect (2)");
                Check(movingViews.Damages == 1 && SpinWait.SpinUntil(() => movingSession.IsCompleted, 20000)
                    && movingSession.TryTakeCompleted(out _, out var movingResult)
                    && FightBattleCheckpointSerializer.Save(movingResult.Checkpoint) == FightBattleCheckpointSerializer.Save(jumpBaseline)
                    && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "The real movement handoff must preserve the isolated jump outcome");
            }
            var canceledMoveViews = new OfficialBattleSkillEntityPlayback();
            using (var canceledMove = new OfficialBattleLiveImpactSession(jumpCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items),
                canceledMoveViews, 0, 1, observeMovements: true))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(52); return true; }
                Check(SpinWait.SpinUntil(() => { canceledMove.Pump(0f, StartJump); return canceledMove.PendingMovement != null; }, 20000),
                    "The cancel fixture did not hold its pending movement");
                canceledMove.Dispose();
                Check(SpinWait.SpinUntil(() => canceledMove.IsCompleted, 20000)
                    && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "Canceling a pending movement must release the worker and preserve the checkpoint");
            }
            var delayedCp = FightBattleCheckpointSerializer.Load(beforeJump);
            var delayedHit = delayedCp.Units.Single(unit => unit.Id == 1).Skills.Single(value => value.EntityId == 10670)
                .HitGraphEntities.Single(value => value.EntityId == 10671);
            foreach (var cell in delayedHit.ScopeEntries) cell.Delay = .1f;
            var delayedViews = new OfficialBattleSkillEntityPlayback();
            using (var delayedSession = new OfficialBattleLiveImpactSession(delayedCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), delayedViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(49); return true; }
                Check(SpinWait.SpinUntil(() => { delayedSession.Pump(0f, StartJump); return delayedViews.Selection != null; }, 20000),
                    "The delayed-cell fixture did not reach its selection");
                var hit = delayedViews.Selection.Targets.Single(t => t.TargetId == 2);
                delayedViews.StartGraph(new EcEntityIdentifier(49), "playable10670", "effect (2)");
                Check(delayedViews.Damages == 0, "A non-simultaneous scope cell cannot use the landing-effect boundary");
                delayedViews.Arrive(hit.HasScopeIndex ? hit.ScopeIndex : -1);
                Check(delayedViews.Damages == 1 && SpinWait.SpinUntil(() => delayedSession.IsCompleted, 20000),
                    "The independently arriving scope cell did not settle at its actual boundary");
            }
            var multiCp = FightBattleCheckpointSerializer.Load(beforeJump);
            var secondEnemy = Unit(3, 2, 8, formula); secondEnemy.Y = 8;
            multiCp.Units.Add(secondEnemy); multiCp.Waves[0].UnitIds.Add(3);
            string beforeMulti = FightBattleCheckpointSerializer.Save(multiCp);
            var multiBaseline = FightBattleCheckpointSerializer.Load(beforeMulti);
            var multiExpected = FightBattleExecutor.Restore(multiBaseline, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            int multiHits = multiExpected.Event.DamageEvents.Count(d => d.SkillItemId == 10015);
            Check(multiHits >= 2, "The jump area fixture must select more than one enemy");
            var multiViews = new OfficialBattleSkillEntityPlayback();
            using (var multiSession = new OfficialBattleLiveImpactSession(multiCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), multiViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(50); return true; }
                Check(SpinWait.SpinUntil(() => { multiSession.Pump(0f, StartJump); return multiViews.Selection != null; }, 20000),
                    "The area jump did not offer its selected scopes");
                multiViews.StartGraph(new EcEntityIdentifier(50), "playable10670", "effect (2)");
                Check(multiViews.Damages == multiHits && SpinWait.SpinUntil(() => multiSession.IsCompleted, 20000)
                    && multiSession.TryTakeCompleted(out _, out var result)
                    && FightBattleCheckpointSerializer.Save(result.Checkpoint) == FightBattleCheckpointSerializer.Save(multiBaseline)
                    && FightBattleCheckpointSerializer.Save(multiCp) == beforeMulti,
                    "All simultaneous selected jump cells must settle once in the same landing frame");
            }
            var fallbackViews = new OfficialBattleSkillEntityPlayback { RejectDamage = true };
            using (var fallbackSession = new OfficialBattleLiveImpactSession(jumpCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), fallbackViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(45); return true; }
                Check(SpinWait.SpinUntil(() => { fallbackSession.Pump(0f, StartJump); return fallbackViews.Selection != null; }, 20000),
                    "The fallback fixture did not reach its actual hit");
                var hit = fallbackViews.Selection.Targets.Single(t => t.TargetId == 2);
                fallbackViews.Arrive(hit.HasScopeIndex ? hit.ScopeIndex : -1);
                Check(fallbackSession.PendingSettlement != null && fallbackSession.PendingSettlement.Damage.HasDamageTriggerStatus,
                    "A declined real result must remain held without inventing its status");
                Check(!fallbackSession.TryCompletePendingSettlement((impact, damage) => false)
                    && fallbackSession.PendingSettlement != null && !fallbackSession.IsCompleted,
                    "A declined fallback must keep the exact settlement waiting");
                bool delivered = false;
                Check(fallbackSession.TryCompletePendingSettlement((impact, damage) => {
                    delivered = damage.HasDamageTriggerStatus && damage.BeforeHp == 1000000
                        && damage.AfterHp < 1000000 && !fallbackSession.IsCompleted;
                    return delivered;
                }) && delivered, "The host could not deliver the held result while the worker was suspended");
                Check(SpinWait.SpinUntil(() => { fallbackSession.Pump(0f, StartJump); return fallbackSession.IsCompleted; }, 20000)
                    && fallbackViews.Completions == 1 && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "An accepted fallback did not resume the isolated step safely");
                Check(fallbackSession.TryTakeCompleted(out _, out var fallbackResult)
                    && fallbackResult.Event.DamageEvents.Any(d => d.SkillItemId == 10015),
                    "The resumed jump result was not transferable");
            }
            var nestedCp = FightBattleCheckpointSerializer.Load(beforeJump);
            var fragile = nestedCp.Units.Single(unit => unit.Id == 2);
            fragile.Hp = fragile.Turn.Hp = 1;
            fragile.Statuses.Add(catalog.CreateStatus(25013, 2, 2, 1, 25013, 10518, 6, 17, 1));
            string beforeNested = FightBattleCheckpointSerializer.Save(nestedCp);
            var nestedViews = new OfficialBattleSkillEntityPlayback();
            using (var nestedSession = new OfficialBattleLiveImpactSession(nestedCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), nestedViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(46); return true; }
                Check(SpinWait.SpinUntil(() => { nestedSession.Pump(0f, StartJump); return nestedViews.Selection != null; }, 20000),
                    "The fatal-prevention fixture did not reach its jump impact");
                var hit = nestedViews.Selection.Targets.Single(t => t.TargetId == 2);
                nestedViews.Arrive(hit.HasScopeIndex ? hit.ScopeIndex : -1);
                Check(nestedSession.StatusResolutionBlocked && nestedSession.PendingSettlement != null
                    && !nestedSession.PendingSettlement.Damage.HasDamageTriggerStatus && nestedViews.Damages == 0
                    && !nestedSession.IsCompleted && FightBattleCheckpointSerializer.Save(nestedCp) == beforeNested,
                    "A recovery skill inside status resolution must stay blocked without inventing or displaying a result");
                nestedSession.Dispose();
                Check(SpinWait.SpinUntil(() => nestedSession.IsCompleted, 20000)
                    && FightBattleCheckpointSerializer.Save(nestedCp) == beforeNested,
                    "Canceling a nested unresolved impact changed the committed checkpoint");
            }
            return "Live impact session passed: deferred start, same-frame impact, HP handoff, lifetime, isolation and actual status resolution.";
        }
    }
}

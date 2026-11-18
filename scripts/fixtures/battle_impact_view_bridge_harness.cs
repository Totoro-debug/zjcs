using System;
using System.Threading;
using System.Threading.Tasks;
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
}
namespace Zjcs.Presentation.Combat
{
    // The real native view publishes this event; the bridge under test is the production source.
    public sealed class OfficialBattleSkillEntityPlayback
    {
        public event Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot, int, Pos2d, int>
            HitSelectionViewArrived;
        public void Arrive(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
            int center, int scope) => HitSelectionViewArrived?.Invoke(cast, selection, center, new Pos2d(4, 5), scope);
        public Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot> OnQueue;
        public void QueueSelectedHit(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            FightBattleHitSelectionSnapshot selection, float elapsed) => OnQueue?.Invoke(cast, selection);
        public Func<FightBattleSkillCastEvent, FightBattleDamageEvent, bool> OnDamage;
        public bool QueueSettledDamage(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            FightBattleDamageEvent damage) => OnDamage != null && OnDamage(cast, damage);
        public bool QueueUnresolvedDamage(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            FightBattleDamageEvent damage) => OnDamage != null && OnDamage(cast, damage);
        public bool ResolveDamageStatus(FightBattleDamageEvent damage) => damage.HasDamageTriggerStatus;
    }

    public static class BattleImpactViewBridgeHarness
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Impact view bridge: " + message); }

        public static string Run()
        {
            var views = new OfficialBattleSkillEntityPlayback();
            using var latch = new FightBattleImpactLatch();
            var bridge = new OfficialBattleImpactViewBridge(views, latch);
            var cast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot();
            selection.EffectCenters.Add(new FightGridPoint(4, 5));
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2, HasScopeIndex = true, ScopeIndex = 0 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2, HasScopeIndex = true, ScopeIndex = 1 });
            FightBattlePendingImpact Pending(int index) => new FightBattlePendingImpact {
                Cast = cast, HitSelection = selection, TargetId = 2, OccurrenceIndex = index,
                HasScopeIndex = true, ScopeIndex = index };
            var selectedWorker = Task.Run(() => {
                latch.HitSelected(cast, selection);
                latch.AwaitImpact(Pending(0));
            });
            FightBattleSelectedHit offered = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeSelection(out offered), 20000),
                "The original hit selection was not offered");
            views.OnQueue = (queuedCast, queuedSelection) => throw new InvalidOperationException("queue failed");
            bool rejected = false;
            try { bridge.QueueSelection(offered, new EcEntityIdentifier(42), 0f); }
            catch (InvalidOperationException error) when (error.Message == "queue failed") { rejected = true; }
            Check(rejected && !selectedWorker.Wait(50), "A failed view delivery released the executor");
            bool queueHeld = false, reentrantRejected = false;
            views.OnQueue = (queuedCast, queuedSelection) => {
                queueHeld = !selectedWorker.IsCompleted && ReferenceEquals(queuedCast, cast)
                    && ReferenceEquals(queuedSelection, selection);
                try { latch.CompleteSelection(offered); }
                catch (InvalidOperationException) { reentrantRejected = true; }
                views.Arrive(queuedCast, queuedSelection, 0, 0);
            };
            bridge.QueueSelection(offered, new EcEntityIdentifier(42), 0f);
            Check(queueHeld && reentrantRejected && selectedWorker.Wait(20000),
                "An immediate native arrival was lost during selection handoff");
            views.OnQueue = null;
            var actualImpact = Pending(0);
            var actualDamage = new FightBattleDamageEvent { BeforeHp = 100, AfterHp = 70 };
            var settledWorker = Task.Run(() => latch.ImpactSettled(actualImpact, actualDamage));
            FightBattleSettledImpact settled = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeSettlement(out settled), 20000),
                "The actual settlement was not offered to the host");
            views.OnDamage = (owner, value) => false;
            Check(!bridge.TryQueueSettlement(settled, new EcEntityIdentifier(42)) && !settledWorker.Wait(50),
                "An incomplete view result must leave the executor waiting");
            bool reentrantSettlementRejected = false;
            views.OnDamage = (owner, value) => {
                try { latch.CompleteSettlement(settled); }
                catch (InvalidOperationException) { reentrantSettlementRejected = true; }
                return ReferenceEquals(owner, cast) && ReferenceEquals(value, actualDamage) && !settledWorker.IsCompleted;
            };
            bool hpRefreshed = false;
            Check(bridge.TryQueueSettlement(settled, new EcEntityIdentifier(42), damage => {
                hpRefreshed = ReferenceEquals(damage, actualDamage) && damage.AfterHp == 70 && !settledWorker.IsCompleted;
            }) && hpRefreshed && reentrantSettlementRejected && settledWorker.Wait(20000),
                "The actual damage and HP were not shown before the executor continued");
            cast = new FightBattleSkillCastEvent();
            var first = Task.Run(() => latch.AwaitImpact(Pending(0)));
            Check(SpinWait.SpinUntil(() => latch.TryTake(out _), 20000), "The first impact was not offered to the host");
            views.Arrive(cast, selection, 0, 1);
            Check(!first.Wait(50), "A later scope released the earlier target impact");
            views.Arrive(cast, selection, 0, 0);
            Check(first.Wait(20000), "The actual scope arrival did not release its matching impact");
            var second = Task.Run(() => latch.AwaitImpact(Pending(1)));
            Check(second.Wait(20000), "The earlier native event was lost before its matching impact was requested");
            bridge.Dispose(); bridge.Dispose();
            var otherCast = new FightBattleSkillCastEvent();
            var canceled = Task.Run(() => {
                var impact = Pending(0); impact.Cast = otherCast;
                try { latch.AwaitImpact(impact); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => { while (latch.TryTake(out var item)) if (ReferenceEquals(item.Cast, otherCast)) return true; return false; }, 20000),
                "The disposal fixture never reached its wait");
            views.Arrive(otherCast, selection, 0, 0);
            Check(!canceled.Wait(50), "A disposed bridge continued forwarding arrivals");
            latch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposing the latch did not cancel its pending impact");
            return "Impact view bridge passed: matching, early arrivals, distinct scopes and disposal.";
        }
    }
}

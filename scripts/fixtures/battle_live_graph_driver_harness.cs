#pragma warning disable 0067
using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace UnityEngine
{
    public class Transform { public string Name; public static implicit operator bool(Transform value) => value != null; }
    public class GameObject { }
}
namespace Zjcs.Official.StoryPlayables
{
    public struct EcEntityIdentifier { public int Value; }
    public class MyPlayable { }
}
namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleSkillEntityPlayback
    {
        public int Casts, Moves; public FightBattleSkillCastEvent LastCast; public Func<int, Transform> DamageTargets;
        public Action<FightBattleMovementEvent> Contact;
        public Action Completed;
        public void BindDamageUITargets(Func<int, Transform> value) { DamageTargets = value; }
        public EcEntityIdentifier Start(FightBattleSkillCastEvent cast, Transform source, Transform target,
            IReadOnlyDictionary<int, FightBattleDamageEvent> damageReceipts = null, Action onActionCompleted = null)
        { LastCast = cast; Casts++; if (onActionCompleted != null) Completed = onActionCompleted; return new EcEntityIdentifier { Value = Casts }; }
        public bool OwnsMovement(FightBattleMovementEvent movement) => movement.MoveBehaviourId == 11123;
        public void StartMovement(FightBattleMovementEvent movement, Transform source,
            FightGridPoint? position = null, Action<FightBattleMovementEvent> contact = null)
        { Moves++; Contact = contact; }
    }
    public sealed class OfficialBattleLiveImpactSession : IDisposable
    {
        public delegate bool StartCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id);
        public delegate bool StartMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement);
        public delegate bool StartBlockedMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint position, Action contact);
        public static OfficialBattleLiveImpactSession Last;
        public StartCast Cast; public StartMovement Move; public StartBlockedMovement Blocked;
        public Action<FightBattleDamageEvent> After; public Func<FightBattleDamageEvent, bool> Before;
        public bool IsCompleted, StatusResolutionBlocked, Disposed, ObserveSummons;
        public FightBattlePreparedMovement PendingMovement => null;
        public FightBattleSettledImpact PendingSettlement => null;
        public event Action<FightBattleSkillCastEvent, EcEntityIdentifier, GameObject, MyPlayable> SkillGraphPlayableStarted;
        public event Action<FightBattleStatusViewEvent> SecondsStatusChanged;
        public event Action<FightBattleStatusViewEvent> StatusViewChanged;
        public event Action<FightBattleUnitLifeEvent> UnitLifeChanged;
        public event Action<FightBattleUnitState, FightBattleHitSelectionSnapshot.SpawnedUnit> SummonCreated;
        public void DeliverSummon(FightBattleUnitState unit, FightBattleHitSelectionSnapshot.SpawnedUnit receipt)
            => SummonCreated?.Invoke(unit, receipt);
        public bool TryAdvanceStatusSeconds(double seconds, out System.Threading.Tasks.Task<bool> completion)
        { completion = null; return false; }
        public OfficialBattleLiveImpactSession(FightBattleCheckpoint checkpoint,
            Func<FightBattleCheckpoint, FightBattleExecutor> restore, OfficialBattleSkillEntityPlayback views,
            int sequence, int counter, bool observeMovements = false, bool observeStatusViews = false, bool observeUnitLives = false,
            bool observeSummons = false)
        { if (!observeMovements) throw new InvalidOperationException("Movement observation was not enabled."); ObserveSummons = observeSummons; Last = this; }
        public bool Pump(float elapsed, StartCast cast, Action<FightBattleDamageEvent> after,
            StartMovement move, StartBlockedMovement blocked, Func<FightBattleDamageEvent, bool> before)
        { Cast = cast; Move = move; Blocked = blocked; After = after; Before = before; return true; }
        public bool TryTakeCompleted(out FightBattleExecutor executor, out FightBattleExecutionResult result)
        { executor = null; result = null; return IsCompleted; }
        public void Dispose() { Disposed = true; }
    }
    public static class BattleLiveGraphDriverHarness
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Live graph driver: " + message); }
        public static void Run()
        {
            var checkpoint = new FightBattleCheckpoint();
            checkpoint.Units.Add(new FightBattleUnitState { Id = 1, Hp = 100, MaxHp = 100 });
            checkpoint.Units.Add(new FightBattleUnitState { Id = 2, Hp = 200, MaxHp = 200 });
            var transforms = new Dictionary<int, Transform> { { 2, new Transform { Name = "target" } } };
            var views = new OfficialBattleSkillEntityPlayback();
            bool hasSkill = false; int healthUpdates = 0, costs = 0, contacts = 0, contactEvents = 0;
            var driver = new OfficialBattleLiveGraphDriver(checkpoint, _ => null, views, _ => hasSkill,
                id => transforms.TryGetValue(id, out var value) ? value : null, null,
                (damage, state) => { Check(state.Hp == damage.AfterHp, "callback must see projected health"); healthUpdates++; },
                (cast, state) => { Check(state.Hp == 90, "callback must see projected cost"); costs++; });
            driver.BlockedContact += _ => contactEvents++;
            driver.Pump(.1f);
            var session = OfficialBattleLiveImpactSession.Last;
            var approach = new FightBattleMovementEvent { UnitId = 2, MoveBehaviourId = 11123 };
            Check(session.Move(null, approach) && views.Moves == 1,
                "an immediate ordinary approach must start without a skill cast");
            approach.Delay = .1f;
            Check(!session.Move(null, approach) && views.Moves == 1,
                "a movement with no cast cannot invent a delay origin");
            var cast = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 11120,
                Presentation = new FightBattleSkillCastPresentation { TargetId = 2 },
                HasSkillCostHp = true, SkillCostBeforeHp = 100, SkillCostAfterHp = 90 };
            Check(!session.Cast(cast, out _) && driver.DisplayUnit(1).Hp == 100 && views.Casts == 0,
                "an unsupported skill cannot consume health or start");
            hasSkill = true;
            Check(!session.Cast(cast, out _) && driver.DisplayUnit(1).Hp == 100,
                "a missing source cannot consume health");
            transforms[1] = new Transform { Name = "source" };
            Check(session.Cast(cast, out _) && views.Casts == 1 && costs == 1
                && driver.DisplayUnit(1).Hp == 90 && checkpoint.Units[0].Hp == 100,
                "the actual cast must project cost without modifying the checkpoint");
            Check(!session.Cast(cast, out _), "the same cast cannot start twice");
            var ordinary = new FightBattleMovementEvent { UnitId = 1, MoveBehaviourId = 11123,
                MoveSkillEntityId = 15300, OrdinarySkillPresentation = new FightBattleSkillCastPresentation { ActionDuration = .5f },
                Delay = .2f, OrdinaryMoveDelay = .2f };
            hasSkill = false;
            Check(!session.Move(null, ordinary) && views.Casts == 1,
                "ordinary motion must wait for its original skill graph");
            hasSkill = true;
            ordinary.OrdinarySkillPresentation.ActionDuration = float.NaN;
            Check(!session.Move(null, ordinary) && views.Casts == 1,
                "an invalid action end must not start the movement skill");
            ordinary.OrdinarySkillPresentation.ActionDuration = .5f;
            Check(!session.Move(null, ordinary) && views.Casts == 2 && views.Moves == 1
                && views.LastCast.SkillEntityId == 15300 && views.LastCast.ActorId == 1,
                "ordinary motion must start its original movement skill before the action");
            driver.Pump(.1f);
            Check(!session.Move(null, ordinary) && views.Casts == 2,
                "the movement prelude must not restart its skill or finish early");
            driver.Pump(.15f);
            Check(session.Move(null, ordinary) && views.Casts == 2 && views.Moves == 2,
                "the original movement action must start after its authored skill prelude");
            var attack = new FightBattleSkillCastEvent { ActorId = 1, Slot = 0, SkillEntityId = 10600,
                Presentation = new FightBattleSkillCastPresentation { TargetId = 2 } };
            var reaction = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 10600,
                Presentation = new FightBattleSkillCastPresentation { TargetId = 2 } };
            Check(!session.Cast(attack, out _) && session.Cast(reaction, out _),
                "an ordinary attack must wait for movement without blocking a movement-start trigger");
            views.Completed();
            Check(session.Cast(attack, out _), "the ordinary attack must start when the native action completes");
            var move = new FightBattleMovementEvent { UnitId = 2, MoveBehaviourId = 11123, Delay = .5f };
            Check(!session.Move(cast, move), "the movement must wait for its own cast action time");
            driver.Pump(.1f);
            Check(!session.Blocked(cast, move, new FightGridPoint(3, 4), () => contacts++),
                "a blocked movement cannot start early");
            driver.Pump(.2f);
            Check(session.Blocked(cast, move, new FightGridPoint(3, 4), () => contacts++) && views.Moves == 3
                && contacts == 0 && contactEvents == 0, "blocked action start cannot fabricate contact");
            views.Contact(move); views.Contact(move);
            Check(contacts == 1 && contactEvents == 1, "actual contact must be delivered exactly once");
            var damage = new FightBattleDamageEvent { DamageId = 1, TargetId = 2, HasDamageValue = true,
                BeforeHp = 200, AfterHp = 150, MaxHp = 200 };
            Check(session.Before(damage) && driver.DisplayUnit(2).Hp == 200, "preflight must not change health");
            session.After(damage);
            Check(!session.Before(damage) && healthUpdates == 1 && driver.DisplayUnit(2).Hp == 150
                && checkpoint.Units[1].Hp == 200, "damage must update only the isolated projection once");
            session.IsCompleted = true;
            Check(driver.TryTakeCompleted(out _, out _), "a completed step was not transferable");
            views.Contact(move); Check(contacts == 1, "transferred driver accepted stale contact");
            driver.Dispose(); Check(session.Disposed, "disposing the driver must cancel its session");
            int summons = 0, presented = 0;
            OfficialBattleLiveGraphDriver summonDriver = null;
            summonDriver = new OfficialBattleLiveGraphDriver(checkpoint, _ => null, views, _ => true,
                id => transforms.TryGetValue(id, out var value) ? value : null, null, (_, __) => { },
                summonShown: (unit, receipt) => {
                    Check(summonDriver.DisplayUnit(unit.Id)?.Hp == unit.Hp && receipt.UnitId == unit.Id,
                        "the summoned unit must be available to presentation before its view starts");
                    summons++;
                });
            summonDriver.SummonPresented += (_, __) => presented++;
            var summoned = new FightBattleUnitState { Id = 3, Hp = 60, MaxHp = 60 };
            var spawned = new FightBattleHitSelectionSnapshot.SpawnedUnit { UnitId = 3 };
            var summonSession = OfficialBattleLiveImpactSession.Last;
            Check(summonSession.ObserveSummons, "a bound summon host did not enable live observation");
            summonSession.DeliverSummon(summoned, spawned);
            Check(summons == 1 && presented == 1 && summonDriver.DisplayUnit(3).Hp == 60
                && checkpoint.Units.Count == 2, "the summon handoff changed the committed roster or was duplicated");
            summonDriver.Dispose();
            Console.WriteLine("Live graph driver cast, action timing, contact and health handoff passed.");
        }
    }
}

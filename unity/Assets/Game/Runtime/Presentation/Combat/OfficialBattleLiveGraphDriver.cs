using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Connects one isolated rule step to the scene's original skill and movement views.</summary>
    public sealed class OfficialBattleLiveGraphDriver : IDisposable
    {
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly OfficialBattleLiveImpactSession session;
        private readonly FightBattleLiveHealthProjection health;
        private readonly Func<int, bool> hasSkill;
        private readonly Func<int, Transform> body, pet;
        private readonly Action<FightBattleDamageEvent, FightBattleUnitState> damageShown;
        private readonly Action<FightBattleSkillCastEvent, FightBattleUnitState> costShown;
        private readonly Dictionary<FightBattleSkillCastEvent, float> started = new Dictionary<FightBattleSkillCastEvent, float>();
        private readonly Dictionary<FightBattleMovementEvent, float> ordinaryStarted = new Dictionary<FightBattleMovementEvent, float>();
        private readonly HashSet<int> ordinaryMoving = new HashSet<int>();
        private float clock;
        private bool disposed, transferred;
        internal event Action Disposed;

        public OfficialBattleLiveGraphDriver(FightBattleCheckpoint checkpoint,
            Func<FightBattleCheckpoint, FightBattleExecutor> restore, OfficialBattleSkillEntityPlayback views,
            Func<int, bool> hasSkill, Func<int, Transform> body, Func<int, Transform> pet,
            Action<FightBattleDamageEvent, FightBattleUnitState> damageShown,
            Action<FightBattleSkillCastEvent, FightBattleUnitState> costShown = null, bool observeStatusViews = false)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.hasSkill = hasSkill ?? throw new ArgumentNullException(nameof(hasSkill));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
            this.pet = pet;
            this.damageShown = damageShown ?? throw new ArgumentNullException(nameof(damageShown));
            this.costShown = costShown;
            health = new FightBattleLiveHealthProjection(checkpoint);
            views.BindDamageUITargets(body);
            session = new OfficialBattleLiveImpactSession(checkpoint, restore, views,
                checkpoint.Sequence, checkpoint.Counter + 1, observeMovements: true, observeStatusViews: observeStatusViews);
        }

        public bool IsCompleted => session.IsCompleted;
        public bool StatusResolutionBlocked => session.StatusResolutionBlocked;
        public FightBattlePreparedMovement PendingMovement => session.PendingMovement;
        public FightBattleSettledImpact PendingSettlement => session.PendingSettlement;
        public FightBattleUnitState DisplayUnit(int unitId) => health.Find(unitId);
        public event Action<FightBattleMovementEvent> BlockedContact;
        public event Action<FightBattleStatusViewEvent> SecondsStatusChanged
        { add { session.SecondsStatusChanged += value; } remove { session.SecondsStatusChanged -= value; } }
        public event Action<FightBattleStatusViewEvent> StatusViewChanged
        { add { session.StatusViewChanged += value; } remove { session.StatusViewChanged -= value; } }
        public bool TryAdvanceStatusSeconds(double seconds, out Task<bool> completion)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveGraphDriver));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            return session.TryAdvanceStatusSeconds(seconds, out completion);
        }
        public event Action<FightBattleSkillCastEvent, EcEntityIdentifier, GameObject, MyPlayable> SkillGraphPlayableStarted
        { add { session.SkillGraphPlayableStarted += value; } remove { session.SkillGraphPlayableStarted -= value; } }

        public bool Pump(float elapsed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveGraphDriver));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            clock += elapsed;
            if (float.IsInfinity(clock)) throw new InvalidOperationException("The live presentation clock overflowed.");
            return session.Pump(elapsed, StartCast, ShowDamage, StartMovement, StartBlockedMovement, CanShowDamage);
        }

        private bool StartCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
        {
            id = default;
            if (cast?.Presentation == null || !hasSkill(cast.SkillEntityId) || started.ContainsKey(cast)
                || !cast.IsPetSupport && cast.Slot >= 0 && ordinaryMoving.Contains(cast.ActorId)) return false;
            Transform source = cast.IsPetSupport ? pet?.Invoke(cast.ActorId) : body(cast.ActorId);
            int targetId = cast.Presentation.TargetId;
            Transform target = targetId > 0 ? body(targetId) : null;
            if (!source || targetId > 0 && !target || cast.HasSkillCostHp && !health.CanApplyCost(cast)) return false;
            if (cast.HasSkillCostHp && !health.TryApplyCost(cast))
                throw new InvalidOperationException("The actual cast cost could not be projected.");
            id = views.Start(cast, source, target);
            started.Add(cast, clock);
            if (cast.HasSkillCostHp) costShown?.Invoke(cast, health.Find(cast.ActorId));
            return true;
        }

        private bool MovementReady(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement, out Transform source)
        {
            source = null;
            if (movement == null || float.IsNaN(movement.Delay) || float.IsInfinity(movement.Delay)
                || movement.Delay < 0f || !views.OwnsMovement(movement)) return false;
            source = body(movement.UnitId);
            if (!source) return false;
            // An ordinary approach has its own original movement skill, independent
            // of the later attack. Its authored move delay starts with that skill.
            if (cast == null)
            {
                if (movement.OrdinarySkillPresentation == null) return movement.Delay == 0f;
                if (!FightOrdinaryMovementTiming.Supports(movement.MoveSkillEntityId)
                    || !hasSkill(movement.MoveSkillEntityId)
                    || movement.Delay != movement.OrdinaryMoveDelay
                    || float.IsNaN(movement.OrdinarySkillPresentation.ActionDuration)
                    || float.IsInfinity(movement.OrdinarySkillPresentation.ActionDuration)
                    || movement.OrdinarySkillPresentation.ActionDuration < 0f) return false;
                if (!ordinaryStarted.TryGetValue(movement, out float moveStart))
                {
                    if (ordinaryMoving.Contains(movement.UnitId)) return false;
                    var ordinaryCast = new FightBattleSkillCastEvent { ActorId = movement.UnitId, Slot = -1,
                        SkillEntityId = movement.MoveSkillEntityId, Presentation = movement.OrdinarySkillPresentation };
                    ordinaryMoving.Add(movement.UnitId);
                    try { views.Start(ordinaryCast, source, null,
                        onActionCompleted: () => ordinaryMoving.Remove(movement.UnitId)); }
                    catch { ordinaryMoving.Remove(movement.UnitId); throw; }
                    ordinaryStarted.Add(movement, moveStart = clock);
                }
                return clock - moveStart >= movement.OrdinaryMoveDelay;
            }
            else if (!started.TryGetValue(cast, out float castStart) || clock - castStart < movement.Delay)
                return false;
            return true;
        }

        private bool StartMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement)
        {
            if (!MovementReady(cast, movement, out var source)) return false;
            views.StartMovement(movement, source);
            return true;
        }

        private bool StartBlockedMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint blockedPosition, Action contact)
        {
            if (!MovementReady(cast, movement, out var source)) return false;
            bool contacted = false;
            views.StartMovement(movement, source, blockedPosition, action => {
                if (disposed || transferred || contacted) return;
                contacted = true;
                BlockedContact?.Invoke(action);
                contact();
            });
            return true;
        }

        private bool CanShowDamage(FightBattleDamageEvent damage)
        {
            if (damage == null) return false;
            if (damage.HasDamageValue) return health.CanApplyDamage(damage);
            var unit = health.Find(damage.TargetId);
            return unit != null && unit.Hp == damage.BeforeHp && damage.AfterHp == damage.BeforeHp;
        }

        private void ShowDamage(FightBattleDamageEvent damage)
        {
            if (damage.HasDamageValue && !health.TryApplyDamage(damage))
                throw new InvalidOperationException("The actual damage no longer matches displayed health.");
            damageShown(damage, health.Find(damage.TargetId));
        }

        public bool TryTakeCompleted(out FightBattleExecutor executor, out FightBattleExecutionResult result)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveGraphDriver));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (!session.TryTakeCompleted(out executor, out result)) return false;
            transferred = true;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { session.Dispose(); }
            finally { started.Clear(); ordinaryStarted.Clear(); ordinaryMoving.Clear(); Disposed?.Invoke(); Disposed = null; }
        }
    }
}

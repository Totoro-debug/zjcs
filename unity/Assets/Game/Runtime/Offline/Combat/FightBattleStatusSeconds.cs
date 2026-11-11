using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusSecondsState
    {
        public bool DefinitionResolved;
        public double DurationSeconds;
        public double RemainingSeconds;
        public bool Started;
        public bool ResetOnNonMoveDamage;
        public bool EndOnBlockDamage;
        public List<FightBattleStatusStateTrigger> Successors = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusSecondsState Copy()
        {
            var copy = (FightBattleStatusSecondsState)MemberwiseClone();
            copy.Successors = new List<FightBattleStatusStateTrigger>();
            foreach (var successor in Successors) copy.Successors.Add(successor?.Copy());
            return copy;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusSecondsHit
    {
        public long Serial;
        public double DueSeconds;
        public int TargetId;
        public bool SuccessfulDamage;
        public bool Block;
        public bool Displacement;
        // The original hit's roll and source properties are captured when it settles.
        // Playback never re-rolls a hit, including after a checkpoint restore.
        public FightBattleStatusState LaunchStatus;
    }

    [Serializable]
    public sealed class FightBattleStatusSecondsClock
    {
        public double ElapsedSeconds;
        public long Serial;
        public List<FightBattleStatusSecondsHit> PendingHits = new List<FightBattleStatusSecondsHit>();
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusSecondsState ParseStatusSeconds(Dictionary<string, object> components)
        {
            var actionComponent = Component(components, "ActionComponent");
            int action = actionComponent == null ? 0 : Int(actionComponent, "ActionType");
            // Only the two user-confirmed airborne actions are migrated here. Other
            // second-based server components have separate, unconfirmed event rules.
            if (action != 66 && action != 67) return null;
            var damage = Component(components, "FightStatusDamageSkillComponent");
            var timerOwner = action == 66 ? damage : Component(components, "StatusAutoRemoveComponent");
            var timer = timerOwner == null ? null : Component(timerOwner, "AutoRemove");
            if (timer == null || Float(timer, "Duration", -1f) < 0f
                || Int(timer, "RemoveTarget") != 6) return null;
            var removeComponent = Component(components, "FightStatusDamageRemoveComponent");
            var remove = removeComponent == null ? null : Component(removeComponent, "StatusDamageTypeCfg");
            var result = new FightBattleStatusSecondsState {
                DefinitionResolved = true, DurationSeconds = Float(timer, "Duration", -1f),
                ResetOnNonMoveDamage = action == 66 && damage != null && Bool(timer, "ResetTimerWhenInvoke")
                    && Int(damage, "DamageMove") == 2 && Bool(damage, "Damage"),
                EndOnBlockDamage = remove != null && Bool(remove, "IsDamage") && Bool(remove, "IsBlock")
                    || action == 67 && damage != null && Bool(damage, "Damage") && Bool(damage, "Block")
                        && Bool(damage, "AutoDestory") && Int(damage, "MaxCount", -1) == 1
            };
            AddTriggerStatuses(timer, "RemoveStatusCfgs", result.Successors);
            foreach (var successor in result.Successors)
                if (successor.ApplyTarget != 2 || successor.BasePercent != 1f || successor.AffectedByProp)
                    throw new InvalidOperationException("Unconfirmed airborne successor rule.");
            return result;
        }
    }

    /// <summary>
    /// User-confirmed airborne seconds, independent of wall time and Unity. Signals
    /// carry their final nested-cast offsets; ties retain original hit order.
    /// Status application and removal use the existing official lifetime chain.
    /// </summary>
    public sealed class FightBattleStatusSeconds
    {
        private readonly FightBattleCheckpoint checkpoint;
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightRandomStream random;
        public event Action<FightBattleUnitState, FightBattleStatusState, bool> StatusChanged;
        public event Action<FightBattleUnitState, FightBattleStatusState> StatusInvoked;

        public static double NextDelay(FightBattleCheckpoint checkpoint)
        {
            var clock = checkpoint?.StatusSeconds;
            if (clock == null) return double.PositiveInfinity;
            double delay = clock.PendingHits.Count == 0 ? double.PositiveInfinity
                : Math.Max(0d, clock.PendingHits[0].DueSeconds - clock.ElapsedSeconds);
            foreach (var owner in checkpoint.Units)
                foreach (var status in owner.Statuses)
                    if (status.Components?.Seconds is FightBattleStatusSecondsState state && state.Started)
                        delay = Math.Min(delay, Math.Max(0d, state.RemainingSeconds));
            return delay;
        }

        public FightBattleStatusSeconds(FightBattleCheckpoint checkpoint, FightOfficialCombatCatalog catalog,
            FightRandomStream random)
        {
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (checkpoint.StatusSeconds == null) checkpoint.StatusSeconds = new FightBattleStatusSecondsClock();
        }

        public void Enqueue(double delay, int targetId, bool successfulDamage, bool block,
            bool displacement, FightBattleStatusState launchStatus = null)
        {
            RequireDelta(delay);
            var clock = checkpoint.StatusSeconds;
            clock.PendingHits.Add(new FightBattleStatusSecondsHit {
                Serial = ++clock.Serial, DueSeconds = clock.ElapsedSeconds + delay,
                TargetId = targetId, SuccessfulDamage = successfulDamage, Block = block,
                Displacement = displacement, LaunchStatus = launchStatus
            });
            clock.PendingHits.Sort((a, b) => {
                int time = a.DueSeconds.CompareTo(b.DueSeconds);
                return time != 0 ? time : a.Serial.CompareTo(b.Serial);
            });
        }

        public void Advance(double seconds, bool paused = false)
        {
            RequireDelta(seconds);
            if (paused) return;
            var clock = checkpoint.StatusSeconds;
            double until = clock.ElapsedSeconds + seconds;
            for (;;)
            {
                double next = until;
                if (clock.PendingHits.Count > 0) next = Math.Min(next, clock.PendingHits[0].DueSeconds);
                foreach (var owner in checkpoint.Units)
                    foreach (var status in owner.Statuses)
                    {
                        var state = status.Components?.Seconds;
                        if (state != null && state.Started)
                            next = Math.Min(next, clock.ElapsedSeconds + Math.Max(0d, state.RemainingSeconds));
                    }
                double delta = Math.Max(0d, next - clock.ElapsedSeconds);
                foreach (var owner in checkpoint.Units)
                    foreach (var status in owner.Statuses)
                        if (status.Components?.Seconds is FightBattleStatusSecondsState state && state.Started)
                            state.RemainingSeconds = clock.ElapsedSeconds + state.RemainingSeconds <= next
                                ? 0d : Math.Max(0d, state.RemainingSeconds - delta);
                clock.ElapsedSeconds = next;
                // Expiration wins an exact-time tie: a later hit cannot revive a
                // timer which already reached zero. Newly created successors do not
                // receive the damage event which ended their predecessor.
                foreach (var owner in checkpoint.Units)
                    foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                        if (status.Components?.Seconds is FightBattleStatusSecondsState state
                            && state.Started && state.RemainingSeconds <= 0d)
                            End(owner, status);
                while (clock.PendingHits.Count > 0 && clock.PendingHits[0].DueSeconds <= next)
                {
                    var hit = clock.PendingHits[0];
                    clock.PendingHits.RemoveAt(0);
                    ApplyHit(hit);
                }
                if (clock.ElapsedSeconds >= until) return;
            }
        }

        private void ApplyHit(FightBattleStatusSecondsHit hit)
        {
            if (!hit.SuccessfulDamage) return;
            var owner = checkpoint.Units.Find(unit => unit.Id == hit.TargetId);
            if (owner == null || owner.Ended) return;
            FightBattleStatusState created = null;
            if (hit.LaunchStatus != null)
            {
                var previous = new HashSet<FightBattleStatusState>(owner.Statuses);
                var applied = FightStatusLifecycle.Apply(owner, hit.LaunchStatus,
                    -1, NextInstanceId(), random);
                if (applied != null && !previous.Contains(applied))
                {
                    created = applied;
                    Start(owner, applied);
                }
            }
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
            {
                var state = status.Components?.Seconds;
                if (state == null || !state.Started || status.ForbiddenType != 0 || status.StackCount <= 0) continue;
                if (hit.Block && state.EndOnBlockDamage) End(owner, status);
                else if (status != created && !hit.Displacement && state.ResetOnNonMoveDamage)
                {
                    state.RemainingSeconds = state.DurationSeconds;
                    StatusInvoked?.Invoke(owner, status);
                }
            }
        }

        private void Start(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var state = status.Components?.Seconds;
            if (state == null || state.Started) return;
            state.Started = true;
            state.RemainingSeconds = state.DurationSeconds;
            StatusChanged?.Invoke(owner, status, true);
        }

        private void End(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (!owner.Statuses.Contains(status)) return;
            FightStatusLifecycle.End(owner, status);
            StatusChanged?.Invoke(owner, status, false);
            foreach (var successor in status.Components.Seconds.Successors)
            {
                var template = catalog.CreateStatus(successor.EntityId, owner.Id,
                    status.CreatorId > 0 ? status.CreatorId : status.OwnerId, NextInstanceId(),
                    status.RootSkillId, status.ApplyingSkillItemId, status.SourceSkillRank,
                    status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                    replacementProperties: successor.StatusReplacementProperties ?? status.ReplacementProperties,
                    blessPropertyScales: status.BlessPropertyScales);
                template.Origin = FightBattleStatusOrigin.Resolve(status).Copy();
                template.ReportSource = FightBattleStatusReportSource.Resolve(status).Copy();
                var applied = FightStatusLifecycle.Apply(owner, template, -1, template.InstanceId, random);
                if (applied != null) Start(owner, applied);
            }
        }

        private int NextInstanceId()
        {
            foreach (var owner in checkpoint.Units)
                foreach (var status in owner.Statuses)
                    checkpoint.StatusInstanceSerial = Math.Max(checkpoint.StatusInstanceSerial, status.InstanceId);
            return ++checkpoint.StatusInstanceSerial;
        }

        private static void RequireDelta(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

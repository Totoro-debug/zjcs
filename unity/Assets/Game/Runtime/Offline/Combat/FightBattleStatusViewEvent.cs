using System;

namespace Zjcs.Combat.Official
{
    public enum FightBattleStatusViewEventKind { Start, Update, End, Invoke }

    // Immutable view inputs captured at the actual component transition. The
    // settled checkpoint can already contain later refreshes/removal in this step.
    [Serializable]
    public sealed class FightBattleStatusViewEvent
    {
        public FightBattleStatusViewEventKind Kind;
        public float Delay;
        public int OwnerId;
        public FightBattleStatusViewState Status;
    }

    [Serializable]
    public sealed class FightBattleStatusViewState
    {
        public int InstanceId, ClassId, CreatorId, StackCount, RoundStartRemainingCount;
        public int ApplyingSkillItemId;
        public FightStatusEndReason EndReason;
        public bool CanStack, Forbidden, HasRoundStart;
        public int ShareId;
        public bool HasSharedStackCount;
        public int SharedStackCount;
        public long ShieldMaximum, ShieldRemaining;
        public int AbsoluteMaximumCount, AbsoluteTriggerCount;

        public static FightBattleStatusViewState Capture(FightBattleStatusState status, FightBattleUnitState owner = null)
            => new FightBattleStatusViewState {
                InstanceId = status.InstanceId, ClassId = status.ClassId, CreatorId = status.CreatorId,
                ApplyingSkillItemId = status.ApplyingSkillItemId, EndReason = status.EndReason,
                StackCount = status.StackCount, CanStack = status.IsOpenStack,
                Forbidden = status.ForbiddenType != 0, ShareId = status.SharedStackId,
                HasSharedStackCount = owner != null && status.SharedStackId > 0,
                SharedStackCount = owner != null && status.SharedStackId > 0
                    ? FightStatusSharedStacks.Total(owner, status.SharedStackId) : 0,
                HasRoundStart = status.HasRoundStartTrigger,
                RoundStartRemainingCount = FightStatusLifecycle.RoundStartRemainingCount(status),
                ShieldMaximum = status.ShieldValue,
                ShieldRemaining = status.ShieldInitialized ? status.ShieldRemainValue : status.ShieldValue,
                AbsoluteMaximumCount = status.Components?.DamageReduceAbsolute?.MaxCount ?? -1,
                AbsoluteTriggerCount = status.Components?.DamageReduceAbsolute?.TriggeredCount ?? 0
            };

        internal bool SameView(FightBattleStatusViewState other) => other != null
            && ClassId == other.ClassId && CreatorId == other.CreatorId && ApplyingSkillItemId == other.ApplyingSkillItemId
            && StackCount == other.StackCount
            && CanStack == other.CanStack && Forbidden == other.Forbidden && ShareId == other.ShareId
            && HasSharedStackCount == other.HasSharedStackCount && SharedStackCount == other.SharedStackCount
            && HasRoundStart == other.HasRoundStart && RoundStartRemainingCount == other.RoundStartRemainingCount
            && ShieldMaximum == other.ShieldMaximum && ShieldRemaining == other.ShieldRemaining
            && AbsoluteMaximumCount == other.AbsoluteMaximumCount && AbsoluteTriggerCount == other.AbsoluteTriggerCount;
    }
}

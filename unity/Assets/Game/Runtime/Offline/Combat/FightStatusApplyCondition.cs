using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusApplyConditionState
    {
        public bool DefinitionResolved;
        public List<int> StatusTypes = new List<int>();
        public List<int> StatusIds = new List<int>();
        public bool CheckCanDisperse;
        public int CreatorType;
        public int MaxCount;
        public int RejectedCount;
        public bool DestroyOnMaximum;

        public FightBattleStatusApplyConditionState Copy()
        {
            var result = (FightBattleStatusApplyConditionState)MemberwiseClone();
            result.StatusTypes = new List<int>(StatusTypes ?? new List<int>());
            result.StatusIds = new List<int>(StatusIds ?? new List<int>());
            return result;
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private HashSet<int> hiddenImmunityStatusIds = new HashSet<int>();
        internal bool ShowStatusImmunity(int protectionId) => !hiddenImmunityStatusIds.Contains(protectionId);

        private static FightBattleStatusApplyConditionState ParseApplyCondition(Dictionary<string, object> info)
        {
            if (info == null) return null;
            // None of the selected official definitions has these server-side follow-up actions.
            if (FieldArray(info, "StatusTriggerSkillCfgs").Count != 0
                || FieldArray(info, "StatusTriggerStatusCfgs").Count != 0)
                throw new NotSupportedException("Status immunity follow-up actions require an execution definition.");
            return new FightBattleStatusApplyConditionState {
                DefinitionResolved = true,
                StatusTypes = new List<int>(IntArray(info, "LimitStatusTypes")),
                StatusIds = new List<int>(IntArray(info, "LimitStatusEntityClassIds")),
                CheckCanDisperse = Bool(info, "CheckCanDisperse"),
                CreatorType = Int(info, "StatusCreatorType"),
                MaxCount = Int(info, "MaxCount"),
                DestroyOnMaximum = Bool(info, "DestoryOnTriggerMaxCount")
            };
        }

        internal void RestoreApplyConditionDefinition(FightBattleStatusState status)
        {
            var previous = status.Components?.ApplyCondition;
            if (previous != null && previous.DefinitionResolved) return;
            if (!entities.TryGetValue(status.ClassId, out var components)) return;
            var definition = ParseApplyCondition(Component(
                ResolveStatusParameters(components, status.ReplacementProperties), "StatusStatusApplyConditionComponent"));
            if (definition == null) return;
            if (previous != null) definition.RejectedCount = previous.RejectedCount;
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            status.Components.ApplyCondition = definition;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private FightBattleDamageEvent statusApplicationDamage;

        private void InitializeWithStatusApplicationFilter()
        {
            foreach (var unit in StatusOwners())
                unit.StatusApplicationRejected = template => RejectStatusApplication(unit, template);
            try { Initialize(); }
            finally { foreach (var unit in StatusOwners(true)) unit.StatusApplicationRejected = null; }
        }

        private bool RejectStatusApplication(FightBattleUnitState owner, FightBattleStatusState incoming)
        {
            if (owner == null || incoming == null || owner.Statuses == null) return false;
            foreach (var protection in owner.Statuses)
            {
                var condition = protection?.Components?.ApplyCondition;
                if (condition == null || !condition.DefinitionResolved || protection.ForbiddenType != 0
                    || protection.StackCount <= 0
                    || condition.MaxCount > 0 && condition.RejectedCount >= condition.MaxCount
                    || condition.CheckCanDisperse && !incoming.CanDispersed
                    || !condition.StatusTypes.Contains(incoming.StatusType)
                        && !condition.StatusIds.Contains(incoming.ClassId)) continue;
                unitById.TryGetValue(incoming.CreatorId, out var creator);
                bool sourceMatches;
                switch (condition.CreatorType)
                {
                    case 0: sourceMatches = true; break;
                    case 1: sourceMatches = incoming.CreatorId == owner.Id; break;
                    case 2: sourceMatches = creator != null && creator.Side != owner.Side; break;
                    case 3: sourceMatches = creator != null && creator.Side == owner.Side; break;
                    default: throw new NotSupportedException("Unknown status immunity creator type.");
                }
                if (!sourceMatches) continue;
                // Confirmed offline server gap: nonpositive maxima are unlimited. Only a
                // successful refusal consumes a positive maximum, including status renewals.
                if (condition.MaxCount > 0) condition.RejectedCount++;
                if (statusCatalog != null && statusCatalog.ShowStatusImmunity(protection.ClassId))
                {
                    if (currentStepStatusCustomActions == null) owner.StatusImmunityActionPendingCount++;
                    else currentStepStatusCustomActions.Add(new FightBattleStatusCustomActionEvent {
                        UnitId = owner.Id, Action = 1,
                        SourceDamageId = statusApplicationDamage?.DamageId ?? 0,
                        SourceDamageTargetId = statusApplicationDamage?.TargetId ?? 0
                    });
                }
                if (condition.MaxCount > 0 && condition.RejectedCount >= condition.MaxCount
                    && condition.DestroyOnMaximum)
                    FightStatusLifecycle.End(owner, protection);
                return true;
            }
            return false;
        }
    }
}

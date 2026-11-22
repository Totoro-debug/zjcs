using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusRoundBeforeEndComponentState
    {
        public bool RulesSupported;
        public bool InvokeOrderByStack;
        public bool BlockOnInvokeFail;
        public float InvokeInterval;
        public bool AutoReduceStack;
        public bool UseStackApplyAsSkillParent;
        public List<FightBattleStatusStateTrigger> Skills = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusRoundBeforeEndComponentState Copy()
        {
            var result = (FightBattleStatusRoundBeforeEndComponentState)MemberwiseClone();
            result.Skills = Skills.ConvertAll(value => value.Copy());
            return result;
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        internal bool HasRoundBeforeEndDefinition(int entityId) => entities.TryGetValue(entityId, out var components)
            && Component(components, "FightStatusRoundBeforeEndComponent") != null;

        private static FightBattleStatusRoundBeforeEndComponentState ParseRoundBeforeEnd(Dictionary<string, object> source)
        {
            if (source == null) return null;
            var result = new FightBattleStatusRoundBeforeEndComponentState {
                InvokeOrderByStack = Bool(source, "InvokeOrderByStack"),
                BlockOnInvokeFail = Bool(source, "BlockOnInvokeFail"),
                InvokeInterval = Float(source, "InvokeInterval", 0f),
                AutoReduceStack = Bool(source, "AutoReduceStack"),
                UseStackApplyAsSkillParent = Bool(source, "UseStackApplyAsSkillParent")
            };
            AddTriggerSkills(source, "SkillCfg", result.Skills);
            result.RulesSupported = result.InvokeOrderByStack && result.BlockOnInvokeFail && result.AutoReduceStack
                && result.InvokeInterval >= 0 && result.Skills.Count > 0
                && result.Skills.Count == FieldArray(source, "SkillCfg").Count;
            foreach (object value in FieldArray(source, "SkillCfg"))
            {
                var row = Object(value, "round-before-end skill");
                var custom = Component(row, "CustomDamageInfo");
                var propertyTarget = Component(row, "PropTarget");
                result.RulesSupported &= Int(row, "Source") == 2 && Bool(row, "UseAI")
                    && Int(row, "Target") == 0 && Int(row, "TargetPos") == 0
                    && !Bool(row, "AffectedByProp") && !Bool(row, "StackedCountToDamageFactor")
                    && (custom == null || Int(custom, "TargetType") == 0)
                    && (propertyTarget == null || Int(propertyTarget, "PropType") == 0)
                    && IntArray(row, "TargetWithoutStatusIds").Length == 0
                    && ReadReplacementProperties(row, "StatusReplacePropertyValues").Count == 0;
            }
            return result;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private void ExecuteRoundBeforeEndSkills(FightBattleUnitState actor,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float duration, ref float actionDuration)
        {
            if (statusCatalog == null || !FightBattleTargeting.IsSelectable(actor)) return;
            float start = Math.Max(0f, actionDuration);
            foreach (var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var info = status.Components?.RoundBeforeEnd;
                if (info == null || !info.RulesSupported || status.ForbiddenType != 0 || !status.IsOpenStack) continue;
                int remainingAttempts = Math.Max(0, status.StackCount), index = 0;
                while (remainingAttempts-- > 0 && actor.Statuses.Contains(status) && status.StackCount > 0
                    && status.ForbiddenType == 0 && FightBattleTargeting.IsSelectable(actor))
                {
                    int slot = status.StackCount - 1;
                    if (slot >= info.Skills.Count) break;
                    var trigger = info.Skills[slot];
                    if (trigger.BasePercent <= 0 || trigger.BasePercent < 1f && random.Next() >= trigger.BasePercent) break;
                    float delay = start + index * info.InvokeInterval;
                    var invocation = new FightBattleStatusSkillInvocation {
                        Trigger = trigger.WithSource(status), SourceUnitId = actor.Id,
                        TargetUnitId = actor.Id, TargetPositionUnitId = actor.Id, SelectTargets = true, SelectTargetsInSkillRange = true,
                        Delay = delay, SourceRootSkillId = info.UseStackApplyAsSkillParent ? status.RootSkillId : 0
                    };
                    int before = casts.Count;
                    float lastDelay = delay;
                    ExecuteConfiguredStatusSkills(new[] { invocation }, casts, damageEvents, movements, ref lastDelay,
                        out float invocationDuration, out float invocationActionDuration);
                    if (!casts.Skip(before).Any(cast => cast.ActorId == actor.Id && cast.SkillEntityId == trigger.EntityId)) break;
                    duration = Math.Max(duration, invocationDuration);
                    actionDuration = Math.Max(actionDuration, invocationActionDuration);
                    float? previous = gridViewTimeOverride;
                    try
                    {
                        gridViewTimeOverride = delay;
                        RecordStatusView(actor, status, FightBattleStatusViewEventKind.Invoke);
                        FightStatusLifecycle.AddStack(actor, status, -1);
                    }
                    finally { gridViewTimeOverride = previous; }
                    index++;
                }
            }
        }
    }
}

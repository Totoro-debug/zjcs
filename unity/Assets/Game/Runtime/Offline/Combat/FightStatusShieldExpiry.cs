using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusShieldExpiryState
    {
        public bool DefinitionResolved;
        public bool Supported;
        public FightBattleStatusStateTrigger Skill;

        public FightBattleStatusShieldExpiryState Copy() => new FightBattleStatusShieldExpiryState {
            DefinitionResolved = DefinitionResolved, Supported = Supported, Skill = Skill?.Copy()
        };
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusShieldExpiryState ParseShieldExpiry(int statusId,
            Dictionary<string, object> components)
        {
            // Only 24435 has a confirmed offline rule for the missing server enablement.
            if (statusId != 24435) return null;
            var shield = Component(components, "FightStatusShieldPersistentComponent");
            var skill = Component(shield, "SkillCfg");
            var triggers = new List<FightBattleStatusStateTrigger>();
            if (skill != null) AddTriggerSkills(new Dictionary<string, object> {
                {"Skills", new List<object> {skill}}
            }, "Skills", triggers);
            var trigger = triggers.Count == 1 ? triggers[0] : null;
            return new FightBattleStatusShieldExpiryState {
                DefinitionResolved = true, Skill = trigger,
                Supported = shield != null && Bool(shield, "CustomShield")
                    && Bool(shield, "ConvertShieldToHpOnEnd")
                    && Bool(shield, "DonotShieldToHpOnEndByRemove")
                    && !Bool(shield, "IsConvertShieldToHp")
                    && trigger != null && trigger.EntityId == 12750 && trigger.Source == 2
                    && trigger.Target == 2 && trigger.TargetPos == 1 && trigger.BasePercent == 1f
                    && !trigger.AffectedByProp && !trigger.UseAI && !trigger.StackedCountToDamageFactor
            };
        }

        internal void RestoreShieldExpiryDefinition(FightBattleStatusState status)
        {
            if (status == null || status.ClassId != 24435
                || status.Components?.ShieldExpiry?.DefinitionResolved == true
                || !entities.TryGetValue(status.ClassId, out var components)) return;
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            status.Components.ShieldExpiry = ParseShieldExpiry(status.ClassId,
                ResolveStatusParameters(components, status.ReplacementProperties));
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private void ExecuteShieldExpiryCures(float delay, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> events, List<FightBattleMovementEvent> movements,
            ref float lastDelay, ref float maxDuration, ref float maxActionDuration)
        {
            var invocations = new List<FightBattleStatusSkillInvocation>();
            foreach (var owner in new List<FightBattleUnitState>(units))
            {
                var pending = owner.EndedShieldExpiryStatuses;
                if (pending == null || pending.Count == 0) continue;
                var ended = new List<FightBattleStatusState>(pending);
                // Consume before invoking skills: callbacks and restored checkpoints cannot replay it.
                pending.Clear();
                foreach (var status in ended)
                {
                    statusCatalog?.RestoreShieldExpiryDefinition(status);
                    var component = status?.Components?.ShieldExpiry;
                    if (status?.ClassId != 24435 || component?.Supported != true
                        || component.Skill == null || status.ShieldRemainValue <= 0
                        || owner.Hp <= 0 || owner.Ended) continue;
                    invocations.Add(new FightBattleStatusSkillInvocation {
                        Trigger = component.Skill.WithSource(status),
                        SourceUnitId = owner.Id, TargetUnitId = owner.Id, TargetPositionUnitId = owner.Id,
                        Delay = delay, HasCustomCure = true, CustomCure = status.ShieldRemainValue
                    });
                }
            }
            ExecuteConfiguredStatusSkills(invocations, casts, events, movements,
                ref lastDelay, out float nestedDuration, out float nestedActionDuration);
            maxDuration = Math.Max(maxDuration, nestedDuration);
            maxActionDuration = Math.Max(maxActionDuration, nestedActionDuration);
        }
    }
}

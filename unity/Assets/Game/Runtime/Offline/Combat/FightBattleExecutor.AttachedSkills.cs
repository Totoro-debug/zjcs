using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed class FightBattleAttachedSelfSkill
    {
        public int EntityId;
        public float Rate;
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        internal List<FightBattleAttachedSelfSkill> AttachedSelfSkills(int entityId)
        {
            var result = new List<FightBattleAttachedSelfSkill>();
            if (!entities.TryGetValue(entityId, out var components)) return result;
            var component = Component(components, "FightSkillBehAttachOperateComponent");
            if (component == null) return result;
            foreach (object value in FieldArray(component, "AttachOperateSettings"))
            {
                var row = Object(value, "attached skill operation");
                // The original setting explicitly selects the caster when the
                // main target and AI switches are both false. Other operations
                // need their complete owning adapter before they can execute.
                if (StringField(row, "_type") != "SkillAttachChildSkillSetting"
                    || Int(row, "AttachOperateType") != 0 || Bool(row, "IsUseMainSkillTarget")
                    || Bool(row, "IsUseAI") || Int(row, "SkillEntityClassId") <= 0) return null;
                result.Add(new FightBattleAttachedSelfSkill {
                    EntityId = Int(row, "SkillEntityClassId"), Rate = Float(row, "Rate", 0f)
                });
            }
            return result;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private void ExecuteAttachedSelfSkills(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay,
            ref float statusDuration, ref float statusActionDuration)
        {
            if (statusCatalog == null || !FightBattleTargeting.IsSelectable(actor)) return;
            var operations = statusCatalog.AttachedSelfSkills(skill.EntityId);
            if (operations == null || operations.Count == 0) return;
            var invocations = new List<FightBattleStatusSkillInvocation>();
            foreach (var operation in operations)
            {
                if (operation.Rate <= 0 || operation.Rate < 1 && random.Next() >= operation.Rate) continue;
                invocations.Add(new FightBattleStatusSkillInvocation {
                    Trigger = new FightBattleStatusStateTrigger {
                        EntityId = operation.EntityId, SourceSkillItemId = skill.ItemId,
                        SourceSkillRank = skill.Rank, SourceSkillLevel = skill.Level, SourceSubRank = skill.SubRank,
                        Origin = skill.StatusOrigin?.Copy(), BlessPropertyScales = FightStatusPropertyModifiers.Copy(skill.BlessPropertyScales)
                    },
                    SourceUnitId = actor.Id, TargetUnitId = actor.Id, TargetPositionUnitId = actor.Id,
                    SourceRootSkillId = skill.RootEntityId
                });
            }
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, casts, damageEvents, movements, ref delay,
                out float duration, out float actionDuration);
            lastDelay = Math.Max(lastDelay, delay);
            statusDuration = Math.Max(statusDuration, duration);
            statusActionDuration = Math.Max(statusActionDuration, actionDuration);
        }
    }
}

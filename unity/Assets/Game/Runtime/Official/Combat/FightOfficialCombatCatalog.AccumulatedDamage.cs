using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusAccumulatedDamage ReadAccumulatedDamage(
            Dictionary<string, object> components)
        {
            Dictionary<string, object> component = Component(components, "FightStatusAccDmgAndCastCustomDamageComponent");
            if (component == null) return null;
            Dictionary<string, object> trigger = Component(component, "StatusTriggerSkillCfg");
            var result = new FightBattleStatusAccumulatedDamage
            {
                SkillEntityId = trigger == null ? 0 : Int(trigger, "SkillId"),
                AccumulationDamagePer = Long(component, "AccumulationDamagePer", 10000),
                DamageResultType = Int(component, "DamageType"),
                HitDamageTypes = new List<int>(IntArray(component, "HitDamageTypes")),
                OnlyDamageByCreator = Bool(component, "OnlyAccDmgByStatusCreator"),
                TriggerDamageAddToDamageSource = Bool(component, "TriggerDamageAddToDamageSource")
            };
            Dictionary<string, object> custom = trigger == null ? null : Component(trigger, "CustomDamageInfo");
            Dictionary<string, object> prop = trigger == null ? null : Component(trigger, "PropTarget");
            // These are the branches evidenced by the actual 24062 configuration and description.
            result.RulesSupported = trigger != null && result.SkillEntityId == 13148
                && result.AccumulationDamagePer == 3000 && result.DamageResultType == 1
                && result.HitDamageTypes.Count == 7 && new HashSet<int>(result.HitDamageTypes).Count == 7
                && result.HitDamageTypes.TrueForAll(value => value >= 1 && value <= 7)
                && ArrayCount(component, "StatusReplacePropertyValues") == 0
                && Float(trigger, "BasePercent", 1f) == 1f && !Bool(trigger, "AffectedByProp")
                && Int(trigger, "Source") == 3 && Int(trigger, "Target") == 2 && Int(trigger, "TargetPos") == 1
                && !Bool(trigger, "UseAI") && !Bool(trigger, "StackedCountToDamageFactor")
                && ArrayCount(trigger, "StatusReplacePropertyValues") == 0 && ArrayCount(trigger, "TargetWithoutStatusIds") == 0
                && (custom == null || Int(custom, "TargetType") == 0 && Int(custom, "BaseProp") == 0
                    && !Bool(custom, "BaseCurrentHp") && Int(custom, "DamageProp") == 0
                    && Int(custom, "UpperLimitTarget") == 0 && Int(custom, "UpperLimitPropType") == 0
                    && Int(custom, "UpperLimitPercent") == 0)
                && (prop == null || !Bool(prop, "IsEnemy") && ArrayCount(prop, "RoleType") == 0
                    && !Bool(prop, "IncludeApplicator") && Int(prop, "PropType") == 0
                    && !Bool(prop, "Heighest") && !Bool(prop, "SupportCustomFilter"));
            return result;
        }
    }
}

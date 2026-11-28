using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private static bool SupportsKillSkill(Dictionary<string, object> component)
        {
            float rate = Float(component, "Rate", 1f);
            int skill = Int(component, "SkillEntId"), status = Int(component, "StatusEntClassId");
            if (rate < 0f || rate > 1f || skill <= 0 && status <= 0 || skill > 0 && status > 0
                || Int(component, "MaxCount", -1) < -1) return false;
            if ((Bool(component, "IsCustomOverflowDamage") || Bool(component, "IsUseCustomDamage"))
                && skill <= 0) return false;
            if (!Bool(component, "IsUseCustomDamage")) return true;
            var custom = Component(component, "CustomDamageInfo");
            return custom != null && Int(custom, "TargetType") == 5
                && Int(custom, "BaseProp") == 0 && !Bool(custom, "BaseCurrentHp")
                && Int(custom, "DamageProp") > 0 && Int(custom, "UpperLimitTarget") == 0
                && Int(custom, "UpperLimitPropType") == 0 && Int(custom, "UpperLimitPercent") == 0;
        }

        private static int ReadKillCustomDamageProperty(Dictionary<string, object> component)
        {
            var custom = component == null ? null : Component(component, "CustomDamageInfo");
            return custom == null ? 0 : Int(custom, "DamageProp");
        }

        private static List<FightBattleKillDamageScale> ReadKillDamageScales(Dictionary<string, object> component)
        {
            var result = new List<FightBattleKillDamageScale>();
            if (component == null || !component.TryGetValue("CharacterTypeToDamageScales", out object raw)
                || raw == null) return result;
            foreach (var entry in Array(raw, "kill damage character scales"))
            {
                var row = Object(entry, "kill damage character scale");
                result.Add(new FightBattleKillDamageScale
                    { GameCharacterType = Int(row, "GameCharacterType"), Scale = Int(row, "Scale") });
            }
            return result;
        }
    }
}

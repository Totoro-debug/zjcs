using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Builds the confirmed offline pet energy state from the selected skill plan.</summary>
    public static class OfficialBattlePetEnergyAssembler
    {
        public static bool TryConfigure(FightBattleUnitState owner, OfflinePlayerProgress player,
            OfficialCharacterCatalog characterCatalog, FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems)
        {
            if (owner == null || player?.Skills?.Plans == null || characterCatalog == null
                || combat == null || skillItems == null || player.Skills.SelectedPlan < 0
                || player.Skills.SelectedPlan >= player.Skills.Plans.Length) return false;
            OfflineSkillPlan plan = player.Skills.Plans[player.Skills.SelectedPlan];
            OfflinePetInstance pet = plan == null || plan.PetIndex <= 0
                ? null : player.Pets?.Pets?.FirstOrDefault(value => value?.Index == plan.PetIndex);
            if (pet == null) return false;
            Dictionary<string, object> evolution = characterCatalog.Rows("pet_evolution")
                .FirstOrDefault(value => Integer(value, "ClassId") == pet.ClassId
                    && Integer(value, "EvolutionPhase") == pet.EvolutionPhase);
            int skillItemId = Integer(evolution, "EntitySkill");
            if (skillItemId <= 0) return false;
            int skillEntityId = skillItemId;
            if (skillItems.TryGet(skillItemId, out FightOfficialSkillItem item))
                skillEntityId = item.EntityId;
            if (skillEntityId <= 0) return false;

            int rank = PetSkillRank(characterCatalog, pet, skillItemId);
            int level = Math.Max(1, pet.Level);
            int subRank = owner.SubRank;
            var abilityProps = new OfflinePetPropSource(characterCatalog)
                .CalculateAbilityProps(pet.ClassId, pet.Abilities);
            int initPropId = characterCatalog.PropertyId("PetInitEnergy");
            abilityProps.TryGetValue(initPropId, out long abilityEnergy);
            Dictionary<string, object> skillRow = characterCatalog.Find("skill", skillItemId)
                ?? characterCatalog.Find("item", skillItemId);
            double initial = Number(skillRow, "InitEnergy", 0d) + abilityEnergy;
            double need = Number(skillRow, "TriggerNeedEnergy", 0d);
            if (need <= 0d) return false;

            var state = owner.PetEnergy ?? new FightBattlePetEnergyState();
            state.Enabled = true;
            state.OwnerUnitId = owner.Id;
            state.PetUnitId = owner.Id;
            state.SkillEntityId = skillEntityId;
            state.SkillItemId = skillItemId;
            state.SkillRank = Math.Max(1, rank);
            state.SkillLevel = level;
            state.SkillSubRank = subRank;
            state.SkillProperties = new List<FightPropertyValue>(ToProperties(
                combat.CalculateSkillProps(skillEntityId, state.SkillRank, state.SkillLevel, subRank)));
            state.InitialEnergy = Math.Max(0d, initial);
            state.NeedEnergy = need;
            state.NaturalRecoverEnergy = 50d;
            state.MasterLoseHpRecoverEnergy = 6d;
            state.MasterLoseHpTriggerRecover = 0.01d;
            owner.PetEnergy = state;
            return true;
        }

        private static int PetSkillRank(OfficialCharacterCatalog catalog, OfflinePetInstance pet, int skillId)
        {
            OfflinePetSkillRank saved = pet.SkillRanks?.FirstOrDefault(value => value?.SkillId == skillId);
            if (saved != null) return Math.Max(1, saved.Rank);
            int quality = Integer(catalog.Find("item", skillId), "Quality");
            return catalog.Rows("skill_rank").Where(value => Integer(value, "Quality") == quality)
                .Select(value => Integer(value, "Rank")).DefaultIfEmpty(1).Min();
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static double Number(Dictionary<string, object> row, string key, double fallback)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static IEnumerable<FightPropertyValue> ToProperties(IReadOnlyDictionary<int, long> values)
        {
            return (values ?? new Dictionary<int, long>()).Where(value => value.Key > 0)
                .OrderBy(value => value.Key)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value });
        }
    }
}

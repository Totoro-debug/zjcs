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
            var petProps = new OfflinePetPropSource(characterCatalog);
            var abilityProps = petProps.CalculateAbilityProps(pet.ClassId, pet.Abilities);
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
            state.PetClassId = pet.ClassId;
            state.PetEntityClassId = Integer(evolution, "FightEntityClassId");
            state.PetEvolutionPhase = pet.EvolutionPhase;
            state.SkillEntityId = skillEntityId;
            state.SkillItemId = skillItemId;
            state.SkillRank = Math.Max(1, rank);
            state.SkillLevel = level;
            state.SkillSubRank = subRank;
            state.SkillProperties = new List<FightPropertyValue>(OfficialBattlePropertyAssembler.ToProperties(
                combat.CalculateSkillProps(skillEntityId, state.SkillRank, state.SkillLevel, subRank)));
            ConfigureEntitySkillSet(state, evolution, pet, characterCatalog, combat, skillItems,
                owner, petProps, rank, level, subRank);
            ApplySupportSkillsOnce(state, evolution, pet, characterCatalog, combat, skillItems,
                owner, level, subRank);
            state.InitialEnergy = Math.Max(0d, initial);
            state.NeedEnergy = need;
            state.NaturalRecoverEnergy = 50d;
            state.MasterLoseHpRecoverEnergy = 6d;
            state.MasterLoseHpTriggerRecover = 0.01d;
            owner.PetEnergy = state;
            return true;
        }

        private static void ConfigureEntitySkillSet(FightBattlePetEnergyState state,
            Dictionary<string, object> evolution, OfflinePetInstance pet,
            OfficialCharacterCatalog catalog, FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState owner,
            OfflinePetPropSource petProps, int entityRank, int level, int subRank)
        {
            var source = OfficialBattlePropertyAssembler.ToMap(owner.Properties);
            FightSummonPropertyInheritance inheritance = new FightSummonPropertyInheritance
            {
                Fixed = new Dictionary<int, long>(), Summoner = new Dictionary<int, long>()
            };
            if (state.PetEntityClassId > 0 && combat.HasSummonInheritance(state.PetEntityClassId))
                inheritance = combat.CalculateSummonInheritance(state.PetEntityClassId, level,
                    entityRank, subRank, source);

            var entityProps = new Dictionary<int, long>(petProps.CalculatePetEntityBaseProps(
                pet.ClassId, level, inheritance.Fixed));
            Merge(entityProps, inheritance.Summoner);
            state.EntityProperties = new List<FightPropertyValue>(OfficialBattlePropertyAssembler.ToProperties(entityProps));

            state.EntityActiveSkills.Clear();
            foreach (int itemId in IntArray(evolution, "EntityActiveSkills"))
            {
                if (itemId <= 0 || !skillItems.TryGet(itemId, out FightOfficialSkillItem item)
                    || item.EntityId <= 0) continue;
                int skillRank = PetSkillRank(catalog, pet, itemId);
                int skillLevel = Math.Max(1, level);
                state.EntityActiveSkills.Add(combat.CreateSkill(item.EntityId,
                    state.EntityActiveSkills.Count, itemId, skillRank, skillLevel, subRank,
                    OfficialBattlePropertyAssembler.ToProperties(combat.CalculateSkillProps(item.EntityId, skillRank,
                        skillLevel, subRank))));
            }

            state.EntityPassiveStatusIds.Clear();
            var deactivePassiveSkills = new HashSet<int>(IntArray(evolution,
                "DeactiveEntityPassiveSkills"));
            foreach (int itemId in IntArray(evolution, "EntityPassiveSkills"))
                if (itemId > 0 && !deactivePassiveSkills.Contains(itemId)
                    && skillItems.TryGet(itemId, out FightOfficialSkillItem item))
                {
                    Merge(entityProps, combat.CalculatePassiveSkillProps(item.PassiveRankPropId,
                        item.PassiveGroupLevelPropId, item.PassiveRankFactorId, item.PassivePropFactors,
                        PetSkillRank(catalog, pet, itemId), Math.Max(1, level), subRank));
                    state.EntityPassiveStatusIds.AddRange(item.PassiveStatusIds ?? Array.Empty<int>());
                }
            state.EntityProperties = new List<FightPropertyValue>(OfficialBattlePropertyAssembler.ToProperties(entityProps));
        }

        private static void ApplySupportSkillsOnce(FightBattlePetEnergyState state,
            Dictionary<string, object> evolution, OfflinePetInstance pet,
            OfficialCharacterCatalog catalog, FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState owner,
            int level, int subRank)
        {
            if (state.SupportSkillsApplied) return;
            var bindings = new List<FightOfficialPassiveSkillBinding>();
            var propertyMap = OfficialBattlePropertyAssembler.ToMap(owner.Properties);
            foreach (int itemId in IntArray(evolution, "SupportSkills"))
            {
                if (itemId <= 0 || !skillItems.TryGet(itemId, out FightOfficialSkillItem item)) continue;
                int rank = PetSkillRank(catalog, pet, itemId);
                int effectiveLevel = Math.Max(1, level);
                IReadOnlyDictionary<int, long> properties = combat.CalculatePassiveSkillProps(
                    item.PassiveRankPropId, item.PassiveGroupLevelPropId, item.PassiveRankFactorId,
                    item.PassivePropFactors, rank, effectiveLevel, subRank);
                Merge(propertyMap, properties);
                bindings.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = itemId, Rank = rank, Level = effectiveLevel, SubRank = subRank
                });
            }
            if (bindings.Count > 0)
            {
                long oldMaxHp = OfficialBattlePropertyAssembler.Value(owner.Properties, combat.PropertyType("MaxHp"));
                owner.Properties = OfficialBattlePropertyAssembler.ToProperties(propertyMap);
                owner.BaseProperties = OfficialBattlePropertyAssembler.ToProperties(propertyMap);
                long newMaxHp = OfficialBattlePropertyAssembler.Value(owner.Properties, combat.PropertyType("MaxHp"));
                if (newMaxHp > 0 && oldMaxHp > 0)
                {
                    owner.MaxHp = newMaxHp;
                    owner.Hp = Math.Min(newMaxHp, owner.Hp + (newMaxHp - oldMaxHp));
                    owner.LastMaxHp = newMaxHp;
                    owner.Turn.Hp = owner.Hp;
                }
                List<FightOfficialStatusBinding> statuses = combat.CreatePassiveStatusBindings(
                    owner, skillItems, bindings);
                combat.AssembleUnit(owner, null, statuses);
            }
            state.SupportSkillsApplied = true;
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

        private static void Merge(Dictionary<int, long> destination, IEnumerable<KeyValuePair<int, long>> source)
        {
            if (source == null) return;
            foreach (KeyValuePair<int, long> value in source)
                if (value.Key > 0) destination[value.Key] = destination.TryGetValue(value.Key,
                    out long current) ? checked(current + value.Value) : value.Value;
        }

        private static int[] IntArray(Dictionary<string, object> row, string key)
        {
            if (row == null || !row.TryGetValue(key, out object raw) || raw == null)
                return Array.Empty<int>();
            if (raw is List<object> values)
                return values.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
            if (raw is int[] ints) return ints;
            return Array.Empty<int>();
        }
    }
}

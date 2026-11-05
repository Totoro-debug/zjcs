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
        private static OfficialCharacterCatalog LoadBattlePetCatalog(OfficialCharacterCatalog characterCatalog)
        {
            var rules = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Combat/pet-battle-rules");
            var receipt = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Combat/pet-battle-rules-source-manifest");
            if (!rules || !receipt) throw new InvalidOperationException("Original battle pet rules are missing.");
            var source = FightOfficialCombatCatalog.ReadSourceManifest(receipt.text);
            return characterCatalog.WithBattlePetRules(rules.text, source.OutputSha256);
        }

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
            if (!characterCatalog.Rows("pet_evolution").Any())
                characterCatalog = LoadBattlePetCatalog(characterCatalog);
            Dictionary<string, object> evolution = characterCatalog.Rows("pet_evolution")
                .FirstOrDefault(value => Integer(value, "ClassId") == pet.ClassId
                    && Integer(value, "EvolutionPhase") == pet.EvolutionPhase);
            // PassiveStatusIdList is not guaranteed to contain status templates:
            // 16014 points to skill 50165. Its owner-turn-start schedule is an
            // explicitly confirmed offline rule; unknown non-status entries still
            // fail before mutating the owner.
            foreach (int supportId in IntArray(evolution, "SupportSkills"))
                if (skillItems.TryGet(supportId, out FightOfficialSkillItem support))
                    foreach (int statusId in support.PassiveStatusIds ?? Array.Empty<int>())
                        if (!combat.HasComponent(statusId, "FightStatusComponent")
                            && !IsOwnerTurnPassiveSupport(support, combat))
                        {
                            UnityEngine.Debug.LogWarning("Battle pet support entity requires passive skill-agent migration: " + statusId);
                            return false;
                        }
            int skillItemId = Integer(evolution, "EntitySkill");
            if (skillItemId <= 0)
            {
                // Childhood has no transfer/energy skill. Support is assembled
                // independently, including the active skill agent's CD/counts.
                int[] supportIds = IntArray(evolution, "SupportSkills");
                if (supportIds.Length == 0 || supportIds.Any(id =>
                    !skillItems.TryGet(id, out FightOfficialSkillItem support)))
                    return false;
                var supportState = owner.PetEnergy ?? new FightBattlePetEnergyState
                {
                    OwnerUnitId = owner.Id, PetUnitId = owner.Id, PetClassId = pet.ClassId,
                    PetEvolutionPhase = pet.EvolutionPhase
                };
                supportState.OwnerUnitId = owner.Id;
                supportState.PetUnitId = owner.Id;
                supportState.PetClassId = pet.ClassId;
                supportState.PetEvolutionPhase = pet.EvolutionPhase;
                ApplySupportSkillsOnce(supportState, evolution, pet, characterCatalog, combat,
                    skillItems, owner, Math.Max(1, pet.Level), owner.SubRank);
                ConfigureSupportSkills(supportState, evolution, pet, characterCatalog, combat,
                    skillItems, owner, Math.Max(1, pet.Level), owner.SubRank);
                owner.PetEnergy = supportState;
                return true;
            }
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
            ConfigureSupportSkills(state, evolution, pet, characterCatalog, combat, skillItems,
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

        private static void ConfigureSupportSkills(FightBattlePetEnergyState state,
            Dictionary<string, object> evolution, OfflinePetInstance pet,
            OfficialCharacterCatalog catalog, FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState owner,
            int level, int subRank)
        {
            if (state.SupportSkills.Count > 0) return;
            foreach (int itemId in IntArray(evolution, "SupportSkills"))
            {
                FightOfficialSkillItem item = skillItems.Required(itemId);
                if (item.PassiveStatusIds != null && item.PassiveStatusIds.Length > 0
                    && !IsOwnerTurnPassiveSupport(item, combat)) continue;
                int rank = PetSkillRank(catalog, pet, itemId);
                var skill = combat.CreateSkill(item.EntityId, -2 - state.SupportSkills.Count,
                    itemId, rank, level, subRank, OfficialBattlePropertyAssembler.ToProperties(
                        combat.CalculateSkillProps(item.EntityId, rank, level, subRank)));
                state.SupportSkills.Add(skill);
                state.SupportSkillState.Slots.Add(combat.CreateSkillSlot(skill,
                    combat.CalculateSkillCooldown(item.EntityId, rank, level, subRank,
                        OfficialBattlePropertyAssembler.ToMap(owner.Properties))));
                owner.ReportSkillSnapshots.Add(skill);
            }
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
                if (item.PassiveStatusIds == null || item.PassiveStatusIds.Length == 0
                    || IsOwnerTurnPassiveSupport(item, combat)) continue;
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

        private static bool IsOwnerTurnPassiveSupport(FightOfficialSkillItem item,
            FightOfficialCombatCatalog combat)
        {
            // Only this verified skill-shaped passive has a confirmed offline
            // schedule. Its passive property factors are empty in the source.
            return item.ItemId == 16014 && item.EntityId == 50165
                && item.PassiveStatusIds != null && item.PassiveStatusIds.Length == 1
                && item.PassiveStatusIds[0] == 50165
                && combat.HasComponent(50165, "FightSkillComponent");
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

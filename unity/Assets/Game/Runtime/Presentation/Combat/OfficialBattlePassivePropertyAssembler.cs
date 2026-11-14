using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattlePassivePropertyAssembler
    {
        public static void ApplyEquippedPlayerPassives(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog items, IDictionary<int, long> properties,
            OfflinePlayerProgress player, OfflineCharacterProgressionService progression)
        {
            Apply(combat, items, properties, CollectEquippedPlayerPassives(items, player, progression));
        }

        /// <summary>Original valid equipped-item selection, shared by properties and passive graph assembly.</summary>
        public static List<FightOfficialPassiveSkillBinding> CollectEquippedPlayerPassives(
            FightOfficialSkillItemCatalog items, OfflinePlayerProgress player, OfflineCharacterProgressionService progression)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            OfflineSkillProfile profile = player.Skills;
            int[] equipped = OfflineSkillProgression.EquippedPassiveSkills(profile);
            int seasonId = player.SeasonRank;
            int profession = player.Wardrobe == null ? 0 : player.Wardrobe.Profession;
            // PlayerSnapshotData.CheckSkillProfession uses SafeProfession before checking PrePro.
            if (profession <= 0)
            {
                int weapon = items.ItemTypeFor(OfflineEquipmentOperations.EquippedItemIdAtPosition(player.Equipment, 10));
                if (weapon == 0) weapon = player.InitialWeaponType;
                if (weapon == FightOfficialSkillItemCatalog.SwordInitialWeaponType) profession = 1;
                else if (weapon == FightOfficialSkillItemCatalog.WandInitialWeaponType) profession = 10;
            }
            var bindings = new List<FightOfficialPassiveSkillBinding>(equipped.Length);
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                int rank = OfflineSkillProgression.OwnedRank(profile, itemId);
                OfflineSkillDefinition definition = progression.SkillDefinition(itemId);
                if (rank <= 0 || definition == null || definition.ItemType != 101) continue;
                // AvailableProfessions includes the original PrePro inheritance closure.
                if (profession <= 0 ? !definition.AvailableWithoutProfession
                    : !(definition.AvailableProfessions ?? Array.Empty<int>()).Contains(profession)) continue;
                bindings.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = itemId,
                    Rank = rank,
                    Level = progression.SkillProgression.SlotLevel(profile, 101, slot, seasonId),
                    SubRank = player.SubRank
                });
            }
            return bindings;
        }

        public static void Apply(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            IDictionary<int, long> properties, IEnumerable<FightOfficialPassiveSkillBinding> bindings)
        {
            if (combat == null) throw new ArgumentNullException(nameof(combat));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            foreach (FightOfficialPassiveSkillBinding binding in bindings ?? Enumerable.Empty<FightOfficialPassiveSkillBinding>())
            {
                if (binding == null || binding.ItemId <= 0) continue;
                if (!items.TryGet(binding.ItemId, out FightOfficialSkillItem item))
                    throw new InvalidDataException("Official passive skill item is missing: " + binding.ItemId);
                IReadOnlyDictionary<int, long> additions = combat.CalculatePassiveSkillProps(
                    item.PassiveRankPropId, item.PassiveGroupLevelPropId, item.PassiveRankFactorId,
                    item.PassivePropFactors, Math.Max(1, binding.Rank), Math.Max(1, binding.Level), binding.SubRank);
                foreach (KeyValuePair<int, long> addition in additions)
                    properties[addition.Key] = properties.TryGetValue(addition.Key, out long current)
                        ? checked(current + addition.Value) : addition.Value;
            }
        }
    }
}

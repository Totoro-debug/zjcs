using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Resolves the saved player loadout into the common battle skill state.</summary>
    public static class OfficialBattlePlayerLoadoutAssembler
    {
        public static void Assemble(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            FightBattleUnitState unit, OfflinePlayerProgress player,
            OfflineCharacterProgressionService progression, bool preserveZeroProperties = false,
            bool requireActiveSkill = false)
        {
            if (combat == null || items == null || unit == null || player == null || progression == null)
                throw new ArgumentException("Battle player loadout inputs are incomplete.");

            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(player.Skills);
            if (!equipped.Any(value => value > 0))
            {
                int initial = items.InitialActiveSkill(player.InitialWeaponType);
                if (initial > 0) equipped = new[] { initial, 0, 0, 0 };
            }
            IReadOnlyDictionary<int, long> properties = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            int weaponType = items.ItemTypeFor(OfflineEquipmentOperations.EquippedItemIdAtPosition(player.Equipment, 10));
            var active = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                int skinId = player.Wardrobe?.SkillSkins?.FirstOrDefault(value => value.SkillId == itemId)?.SkillSkinId ?? 0;
                FightOfficialSkillItem item = items.RequiredForSkin(itemId, skinId);
                active.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot, ItemId = itemId, UseItemId = item.ItemId,
                    SkillSkinId = skinId, Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, itemId)),
                    Level = Math.Max(1, progression.SkillProgression.SlotLevel(player.Skills, 100, slot,
                        progression.SkillContext().CurrentSeasonId)),
                    ProfessionAvailability = items.ProfessionAvailabilityFor(item.ItemId, unit.Profession,
                        weaponType, player.InitialWeaponType),
                    SubRank = player.SubRank
                });
            }
            if (requireActiveSkill && active.Count == 0)
                throw new InvalidOperationException("Official player has no equipped or initial active skill.");
            var passive = OfflineSkillProgression.EquippedPassiveSkills(player.Skills)
                .Select((value, slot) => new FightOfficialPassiveSkillBinding
                {
                    ItemId = value, Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, value)),
                    Level = Math.Max(1, progression.SkillProgression.SlotLevel(player.Skills, 101, slot,
                        progression.SkillContext().CurrentSeasonId)),
                    SubRank = player.SubRank
                }).Where(value => value.ItemId > 0).ToList();
            passive.AddRange(OfficialEquipmentBattlePassives.Collect(progression, player, unit.SubRank));
            OfficialBattleSkillAssembler.AssembleUnit(combat, items, unit, properties, active, passive,
                preserveZeroProperties);
        }
    }
}

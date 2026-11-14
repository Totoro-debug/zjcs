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
            OfflineCharacterProgressionService progression, bool preserveZeroProperties = false)
        {
            if (combat == null || items == null || unit == null || player == null || progression == null)
                throw new ArgumentException("Battle player loadout inputs are incomplete.");

            unit.EntryCombatRating = progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;

            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(player.Skills);
            IReadOnlyDictionary<int, long> properties = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            int weaponType = items.ItemTypeFor(OfflineEquipmentOperations.EquippedItemIdAtPosition(player.Equipment, 10));
            int profession = player.Wardrobe?.Profession ?? 0;
            if (profession <= 0)
            {
                int weapon = weaponType == 0 ? player.InitialWeaponType : weaponType;
                if (weapon == FightOfficialSkillItemCatalog.SwordInitialWeaponType) profession = 1;
                else if (weapon == FightOfficialSkillItemCatalog.WandInitialWeaponType) profession = 10;
            }
            var context = new OfflineSkillPlayerContext { Profession = profession };
            var active = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                int rank = OfflineSkillProgression.OwnedRank(player.Skills, itemId);
                OfflineSkillDefinition definition = progression.SkillDefinition(itemId);
                // PlayerSkillDataExt.GetValidSkillItems resolves owned items before profession checks.
                if (rank <= 0 || definition == null || definition.ItemType != 100
                    || !OfflineSkillProgression.IsAvailableFor(definition, context)) continue;
                int skinId = player.Wardrobe?.SkillSkins?.FirstOrDefault(value => value.SkillId == itemId)?.SkillSkinId ?? 0;
                FightOfficialSkillItem item = items.RequiredForSkin(itemId, skinId);
                active.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot, ItemId = itemId, UseItemId = item.ItemId,
                    SkillSkinId = skinId, Rank = rank,
                    Level = Math.Max(1, progression.SkillProgression.SlotLevel(player.Skills, 100, slot,
                        player.SeasonRank)),
                    // FightSkillData.Refresh checks the actual skin-resolved skill after selection.
                    ProfessionAvailability = OfflineSkillProgression.IsAvailableFor(
                        progression.SkillDefinition(item.ItemId), context)
                        ? FightSkillProfessionAvailability.Available : FightSkillProfessionAvailability.Mismatch,
                    SubRank = player.SubRank
                });
            }
            var passive = OfficialBattlePassivePropertyAssembler.CollectEquippedPlayerPassives(items, player, progression);
            passive.AddRange(OfficialEquipmentBattlePassives.Collect(progression, player, unit.SubRank));
            OfficialBattleSkillAssembler.AssembleUnit(combat, items, unit, properties, active, passive,
                preserveZeroProperties);
        }
    }
}

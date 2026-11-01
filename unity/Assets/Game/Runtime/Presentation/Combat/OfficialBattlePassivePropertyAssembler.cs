using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
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
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            OfflineSkillProfile profile = player.Skills;
            int[] equipped = OfflineSkillProgression.EquippedPassiveSkills(profile);
            int seasonId = progression.SkillContext().CurrentSeasonId;
            var bindings = new List<FightOfficialPassiveSkillBinding>(equipped.Length);
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                bindings.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = itemId,
                    Rank = OfflineSkillProgression.OwnedRank(profile, itemId),
                    Level = progression.SkillProgression.SlotLevel(profile, 101, slot, seasonId),
                    SubRank = player.SubRank
                });
            }
            Apply(combat, items, properties, bindings);
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

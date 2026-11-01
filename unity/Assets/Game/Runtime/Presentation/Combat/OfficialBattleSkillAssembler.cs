using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Builds the shared skill and passive state used by every battle entry.</summary>
    public static class OfficialBattleSkillAssembler
    {
        public static List<FightOfficialSkillBinding> PrepareActiveBindings(
            FightOfficialCombatCatalog combat, IReadOnlyDictionary<int, long> sourceProperties,
            IEnumerable<FightOfficialSkillBinding> bindings, bool preserveZeroProperties = false)
        {
            if (combat == null) throw new ArgumentNullException(nameof(combat));
            var result = new List<FightOfficialSkillBinding>();
            foreach (FightOfficialSkillBinding binding in bindings ?? Enumerable.Empty<FightOfficialSkillBinding>())
            {
                if (binding == null) throw new ArgumentException("Official active skill binding is null.", nameof(bindings));
                IReadOnlyDictionary<int, long> properties = combat.CalculateSkillProps(
                    binding.EntityId, binding.Rank, binding.Level, binding.SubRank);
                binding.Cooldown = combat.CalculateSkillCooldown(binding.EntityId, binding.Rank,
                    binding.Level, binding.SubRank, sourceProperties);
                binding.Properties = preserveZeroProperties
                    ? OfficialBattlePropertyAssembler.ToProperties(properties)
                    : OfficialBattlePropertyAssembler.ToNonZeroProperties(properties);
                result.Add(binding);
            }
            return result;
        }

        public static void AssembleUnit(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState unit,
            IReadOnlyDictionary<int, long> sourceProperties,
            IEnumerable<FightOfficialSkillBinding> activeSkills,
            IEnumerable<FightOfficialPassiveSkillBinding> passiveSkills,
            bool preserveZeroProperties = false)
        {
            if (combat == null) throw new ArgumentNullException(nameof(combat));
            if (skillItems == null) throw new ArgumentNullException(nameof(skillItems));
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            List<FightOfficialSkillBinding> active = PrepareActiveBindings(combat,
                sourceProperties, activeSkills, preserveZeroProperties);
            List<FightOfficialPassiveSkillBinding> passive = passiveSkills?.ToList()
                ?? new List<FightOfficialPassiveSkillBinding>();
            combat.AssembleUnit(unit, active,
                combat.CreatePassiveStatusBindings(unit, skillItems, passive));
        }
    }
}

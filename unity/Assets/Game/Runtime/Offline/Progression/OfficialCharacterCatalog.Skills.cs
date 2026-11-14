using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfficialCharacterCatalog
    {
        private FightOfficialCombatCatalog skillPropertyRules;
        private FightOfficialSkillItemCatalog skillPropertyItems;

        // Character progression contains only its own property tables. Reuse the
        // verified battle inputs and original CalcSkillPassiveProps implementation.
        public OfficialCharacterCatalog WithSkillPropertyRules(FightOfficialCombatCatalog rules,
            FightOfficialSkillItemCatalog skillItems)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (skillItems == null) throw new ArgumentNullException(nameof(skillItems));
            return new OfficialCharacterCatalog(tables, items, skins, appearances, allowed,
                levelExperience, levelLimits, blessLevels, timedAppearanceItems, propertyIds, levelProps,
                wardrobeTables, wardrobeDefinitions, rankBySubRank, defaultAppearance, appearanceItemTypes,
                props, itemIcons, professionNames, professionIcons, skillMergePieces, partnerProperties)
            {
                skillPropertyRules = rules,
                skillPropertyItems = skillItems
            };
        }

        internal IReadOnlyDictionary<int, long> CalculateEquippedSkillPassiveProps(int skillId,
            int rank, int level, int subRank)
        {
            if (skillPropertyRules == null) return null;
            var item = skillPropertyItems.Required(skillId);
            return skillPropertyRules.CalculatePassiveSkillProps(item.PassiveRankPropId,
                item.PassiveGroupLevelPropId, item.PassiveRankFactorId, item.PassivePropFactors,
                rank, level, subRank);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineCharacterProgressionService
    {
        public List<OfflineEquipmentBattleTerm> EquipmentBattlePassiveTerms(OfflinePlayerProgress player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (EquipmentCatalog == null)
                throw new InvalidOperationException("Official equipment rules are unavailable.");
            int level = Math.Max(1, player.Level);
            int subRank = Math.Max(1, player.SubRank);
            var context = new CharacterPropScoreContext
            {
                Level = level,
                SubRank = subRank,
                Props = propertyCalculator.Props,
                BasicRef = CharacterPropBasicRef.Build(level, subRank, propertyCalculator.Props,
                    propertyCalculator.LevelProps)
            };
            List<OfflineEquipmentBattleTerm> terms = new OfflineEquipmentPropSource(EquipmentCatalog, catalog.RankOfSubRank)
                .CollectBattlePassiveTerms(player, context);
            foreach (OfflineEquipmentBattleTerm term in terms)
            {
                int rank = (SkillRules?.RankDisplays ?? Array.Empty<OfflineSkillRankDisplay>())
                    .Where(value => value != null && value.Quality == term.SkillQuality)
                    .Select(value => value.Rank).DefaultIfEmpty(0).Min();
                if (rank <= 0)
                    throw new InvalidOperationException("Official equipment skill quality has no first rank: "
                        + term.SkillQuality);
                term.SkillRank = rank;
            }
            return terms;
        }
    }
}

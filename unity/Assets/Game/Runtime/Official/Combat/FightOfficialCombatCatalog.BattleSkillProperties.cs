using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // Original CalcSkillPropsOnBattle -> PVPSkillPropsScaleOnBattleProcessor.ScaleProps
        // -> ScaleSrcPropsByPvp. Cultivation is truncated first; CD is exempt.
        public IReadOnlyDictionary<int, long> CalculateSkillPropsOnBattle(int entityId, int rank,
            int level, int subRank, bool isPvp)
        {
            if (!isPvp) return CalculateSkillProps(entityId, rank, level, subRank);
            if (scopedSkillPropertyCatalogs.TryGetValue(entityId, out var selected))
                return selected.CalculateSkillPropsOnBattle(entityId, rank, level, subRank, true);
            var result = CopyProperties(CalculateSkillProps(entityId, rank, level, subRank));
            if (!skillEntityProps.TryGetValue(entityId, out var definition)) return result;
            double scale = definition.PvpPropScale / 10000.0;
            foreach (int property in new List<int>(result.Keys))
                if (property != cooldownPropertyId)
                    result[property] = (long)(result[property] * scale);
            return result;
        }
    }
}

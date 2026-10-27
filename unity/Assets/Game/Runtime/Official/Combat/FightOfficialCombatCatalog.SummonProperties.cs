using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightSummonPropertyInheritance
    {
        public IReadOnlyDictionary<int, long> Fixed;
        public IReadOnlyDictionary<int, long> Summoner;
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private readonly Dictionary<int, FightOfficialEntityProp> summonFixedProps =
            new Dictionary<int, FightOfficialEntityProp>();
        private readonly Dictionary<int, SummonAddition> summonAdditions =
            new Dictionary<int, SummonAddition>();
        private readonly Dictionary<int, Dictionary<int, Dictionary<int, long>>> summonRankFactors =
            new Dictionary<int, Dictionary<int, Dictionary<int, long>>>();

        private sealed class SummonAddition
        {
            public int RankGroup;
            public Dictionary<int, long> Properties;
        }

        private void ParseSummonPropertyTables(Dictionary<string, object> skillCatalog)
        {
            var tables = Object(Required(skillCatalog, "tables"), "skill tables");
            foreach (object raw in Array(Required(tables, "summon_monster_fix_prop"), "summon fixed props"))
            {
                var row = Object(raw, "summon fixed prop");
                int id = Int(row, "EntityId");
                summonFixedProps.Add(id, new FightOfficialEntityProp { EntityId = id,
                    RankPropId = Int(row, "RankPropId"), GroupLevelPropId = Int(row, "GroupLevelPropId"),
                    PropFactors = ReadLongMap(row, "PropFactors") });
            }
            foreach (object raw in Array(Required(tables, "summon_monster_add_prop"), "summon additions"))
            {
                var row = Object(raw, "summon addition");
                summonAdditions.Add(Int(row, "ClassId"), new SummonAddition {
                    RankGroup = Int(row, "RankGroup"), Properties = ReadLongMap(row, "PropAdditions") });
            }
            foreach (object raw in Array(Required(tables, "summon_rank_additive_factor"), "summon rank factors"))
            {
                var row = Object(raw, "summon rank factor");
                int group = Int(row, "RankGroup");
                if (!summonRankFactors.TryGetValue(group, out var ranks))
                    summonRankFactors.Add(group, ranks = new Dictionary<int, Dictionary<int, long>>());
                ranks.Add(Int(row, "Rank"), ReadLongMap(row, "PropFactors"));
            }
        }

        /// <summary>
        /// BattleFormulaHandler.CalcSummonMonsterInheritProp: fixed level/rank properties plus
        /// source raw values × configured addition × exact rank factor. Both multipliers use
        /// Long2Double (10000); the original casts to long once after the two multiplications.
        /// The on-battle wrapper does not add PVP scaling. Missing rank/property factors are zero.
        /// </summary>
        public FightSummonPropertyInheritance CalculateSummonInheritance(int entityId, int skillLevel,
            int skillRank, int subRank, IReadOnlyDictionary<int, long> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!summonFixedProps.TryGetValue(entityId, out var fixedConfig)
                || !summonAdditions.TryGetValue(entityId, out var addition))
                throw new InvalidOperationException("Official summon inheritance configuration is absent: " + entityId);
            IReadOnlyDictionary<int, long> level = EmptyProps;
            if (skillGroupLevels.TryGetValue(fixedConfig.GroupLevelPropId, out var levels)
                && levels.TryGetValue(subRank, out int levelId) && levelId > 0)
                level = PropRows(levelId, skillLevel);
            var fixedProps = fixedConfig.RankPropId > 0
                ? ScaleProps(level, PropRows(fixedConfig.RankPropId, skillRank), false)
                : new Dictionary<int, long>(level);
            fixedProps = ScaleProps(fixedProps, fixedConfig.PropFactors, true);
            IReadOnlyDictionary<int, long> factors = EmptyProps;
            if (summonRankFactors.TryGetValue(addition.RankGroup, out var ranks)
                && ranks.TryGetValue(skillRank, out var exact)) factors = exact;
            var inherited = new Dictionary<int, long>();
            foreach (var property in addition.Properties)
            {
                if (property.Value == 0) continue;
                long value = Value(source, property.Key);
                inherited.Add(property.Key, (long)((property.Value / 10000.0) * value
                    * (Value(factors, property.Key) / 10000.0)));
            }
            return new FightSummonPropertyInheritance { Fixed = fixedProps, Summoner = inherited };
        }
    }
}

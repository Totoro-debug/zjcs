using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private sealed class PassivePropertySource
        {
            public FightOfficialCombatCatalog Catalog;
            public int RankPropId, GroupLevelPropId, RankFactorId;
            public Dictionary<int, long> Factors;
        }

        private readonly Dictionary<int, PassivePropertySource> passivePropertySources =
            new Dictionary<int, PassivePropertySource>();

        // A migrated item keeps its own official cultivation inputs. Other items
        // sharing the same original group ID retain their existing source.
        public void ApplyPassiveSkillRulePack(string json, string sha256, string manifest,
            FightOfficialSkillItemCatalog itemCatalog)
        {
            VerifyPayload("passive skill property pack", json, sha256, manifest);
            var pack = Object(OfficialJson.Parse(json), "passive skill property pack");
            int itemId = Int(pack, "item_id");
            if (itemId <= 0 || passivePropertySources.ContainsKey(itemId))
                throw new InvalidOperationException("Invalid or duplicate passive property item: " + itemId);
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));
            FightOfficialSkillItem configured = itemCatalog.Required(itemId);
            if (!string.Equals(Convert.ToString(Required(pack, "manifest")), manifest, StringComparison.Ordinal))
                throw new InvalidOperationException("Passive property pack source differs from its manifest.");
            var skill = Object(Required(pack, "skill"), "passive skill");
            int[] statusIds = IntArray(skill, "PassiveStatusIdList");
            if (Int(skill, "ClassId") != itemId || Int(skill, "EcEntityId") != configured.EntityId
                || statusIds.Length != configured.PassiveStatusIds.Length)
                throw new FormatException("A passive property pack cannot replace a skill graph.");
            for (int index = 0; index < statusIds.Length; index++)
                if (statusIds[index] != configured.PassiveStatusIds[index])
                    throw new FormatException("A passive property pack cannot replace a status graph.");
            var item = Object(Required(pack, "item"), "passive item");
            var factors = ReadLongMap(skill, "PassivePropFactors");
            if (itemCatalog.ItemTypeFor(itemId) != 101
                || configured.PassiveRankPropId != Int(skill, "PassiveRankPropId")
                || configured.PassiveGroupLevelPropId != Int(skill, "PassiveGroupLevelPropId")
                || configured.PassiveRankFactorId != Int(skill, "PassiveRankFactorId")
                || configured.PassivePropFactors.Count != factors.Count
                || Int(item, "ClassId") != itemId || Int(item, "ItemType") != 101)
                throw new InvalidOperationException("Selected passive properties differ from their existing item binding.");
            foreach (var pair in factors)
                if (!configured.PassivePropFactors.TryGetValue(pair.Key, out long value) || value != pair.Value)
                    throw new InvalidOperationException("Selected passive property factors differ from their item binding.");
            var source = new PassivePropertySource {
                RankPropId = Int(skill, "PassiveRankPropId"),
                GroupLevelPropId = Int(skill, "PassiveGroupLevelPropId"),
                RankFactorId = Int(skill, "PassiveRankFactorId"),
                Factors = ReadLongMap(skill, "PassivePropFactors"),
                Catalog = new FightOfficialCombatCatalog { skillPropertyNames = skillPropertyNames }
            };
            if (source.Factors.Count == 0) throw new FormatException("Passive property factors are empty.");
            source.Catalog.ParseSkillPropertyTables(pack);
            if (!source.Catalog.skillGroupLevels.ContainsKey(source.GroupLevelPropId))
                throw new FormatException("Passive property cultivation group is missing.");
            foreach (int propId in new[] { source.RankPropId, source.RankFactorId })
                if (propId > 0 && !source.Catalog.skillPropTables.ContainsKey(propId))
                    throw new FormatException("Passive property rank table is missing: " + propId);
            foreach (int propId in source.Catalog.skillGroupLevels[source.GroupLevelPropId].Values)
                if (propId > 0 && !source.Catalog.skillPropTables.ContainsKey(propId))
                    throw new FormatException("Passive property level table is missing: " + propId);
            passivePropertySources.Add(itemId, source);
        }

        private bool TryCalculatePassiveSkillProps(int itemId, int rankPropId, int groupLevelPropId,
            int rankFactorId, IReadOnlyDictionary<int, long> factors, int rank, int level, int subRank,
            out IReadOnlyDictionary<int, long> result)
        {
            result = null;
            if (!passivePropertySources.TryGetValue(itemId, out var source)) return false;
            if (rankPropId != source.RankPropId || groupLevelPropId != source.GroupLevelPropId
                || rankFactorId != source.RankFactorId || factors == null || factors.Count != source.Factors.Count)
                throw new InvalidOperationException("Passive property item configuration differs: " + itemId);
            foreach (var pair in source.Factors)
                if (!factors.TryGetValue(pair.Key, out long value) || value != pair.Value)
                    throw new InvalidOperationException("Passive property item factor differs: " + itemId);
            result = source.Catalog.CalculatePassiveSkillProps(rankPropId, groupLevelPropId,
                rankFactorId, factors, rank, level, subRank);
            return true;
        }
    }
}

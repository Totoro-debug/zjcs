using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private readonly HashSet<int> skillRulePackEntities = new HashSet<int>();
        private readonly HashSet<int> skillRulePackItems = new HashSet<int>();

        public bool IsSkillRulePackEntity(int entityId) => skillRulePackEntities.Contains(entityId);

        /// <summary>Installs one verified migration without relabelling the older shared catalog.</summary>
        public void ApplySkillRulePack(string json, string sha256, string manifest)
        {
            VerifyPayload("skill rule pack", json, sha256, manifest);
            var pack = Object(OfficialJson.Parse(json), "skill rule pack");
            int itemId = Int(pack, "item_id");
            if (itemId <= 0) throw new FormatException("Skill rule pack has no item identity.");
            if (skillRulePackItems.Contains(itemId))
                throw new InvalidOperationException("Skill rule pack is already installed: " + itemId);
            if (string.IsNullOrEmpty(manifest) || !string.Equals(
                Convert.ToString(Required(pack, "manifest")), manifest, StringComparison.Ordinal))
                throw new InvalidOperationException("Skill rule pack source metadata differs from its manifest.");
            var incoming = Object(Required(pack, "entities"), "pack entities");
            var previous = Object(Required(pack, "previous_entities"), "previous entities");
            if (incoming.Count == 0 || incoming.Count != previous.Count)
                throw new FormatException("Skill rule pack has an incomplete baseline.");
            var replacements = new Dictionary<int, Dictionary<string, object>>();
            foreach (var pair in incoming)
            {
                int id = IntKey(pair.Key, "pack entity");
                if (id <= 0 || !previous.TryGetValue(pair.Key, out object expected))
                    throw new FormatException("Skill rule pack has an invalid entity baseline.");
                entities.TryGetValue(id, out var installed);
                if (!JsonEqual(installed, expected))
                    throw new InvalidOperationException("Skill rule pack baseline differs for entity " + id);
                replacements.Add(id, Object(pair.Value, "pack entity " + id));
            }
            var reportParents = ReadReportSkillParentOverrides(pack);
            ParseSkillPropertyTables(pack);
            foreach (var pair in replacements)
            {
                entities[pair.Key] = pair.Value;
                skillRulePackEntities.Add(pair.Key);
            }
            foreach (var pair in reportParents) reportSkillParents[pair.Key] = pair.Value;
            skillRulePackItems.Add(itemId);
        }
    }
}

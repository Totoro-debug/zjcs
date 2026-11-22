using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private readonly Dictionary<int, int> reportSkillParents = new Dictionary<int, int>();

        private Dictionary<int, int> ReadReportSkillParentOverrides(Dictionary<string, object> pack)
        {
            var result = new Dictionary<int, int>();
            if (!pack.TryGetValue("report_skill_parent_overrides", out object raw)) return result;
            foreach (object value in Array(raw, "report skill parents"))
            {
                var row = Object(value, "report skill parent");
                int id = Int(row, "Id"), parent = Int(row, "OverrideId");
                if (id <= 0 || result.ContainsKey(id)) throw new FormatException("Invalid report skill parent identity.");
                if (reportSkillParents.TryGetValue(id, out int previous) && previous != parent)
                    throw new InvalidOperationException("Report skill parent source conflicts: " + id);
                result.Add(id, parent);
            }
            return result;
        }

        public int ReportParentSkillId(int entityId) => reportSkillParents.TryGetValue(entityId, out int parent)
            ? parent : entityId;

        internal bool IsReportParentActive(int entityId, FightOfficialSkillItemCatalog items)
            => items != null && items.IsActiveSkillEntity(ReportParentSkillId(entityId));
    }
}

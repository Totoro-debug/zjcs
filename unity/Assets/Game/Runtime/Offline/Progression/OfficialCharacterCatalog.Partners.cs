using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfficialCharacterCatalog
    {
        public bool HasCurrentPartnerRules => partnerProperties != null;
        public IReadOnlyList<Dictionary<string, object>> PartnerRows(string table) => partnerProperties != null
            ? partnerProperties.Rows(table) : Rows(table).ToList();
        public Dictionary<string, object> FindPartner(string table, int id, string key = "Id") =>
            PartnerRows(table).FirstOrDefault(row => row.TryGetValue(key, out var value) && Convert.ToInt32(value) == id);
        public Dictionary<string, object> FindPartnerItem(int id) => partnerProperties != null
            ? FindPartner("item", id, "ClassId") : FindItem(id);
        public bool EvaluatePartnerCondition(Dictionary<string, object> condition, OfficialConditionContext context) =>
            partnerProperties != null ? partnerProperties.Conditions.IsMet(condition, new PartnerPlayer(context))
                : EvaluateCondition(condition, context);
        public bool HasManualMeetPartner(OfficialConditionContext context) =>
            partnerProperties.Conditions.HasManualMeetNpc(new PartnerPlayer(context));
        public NpcMeetStatus CurrentPartnerMeetStatus(int npcClassId, OfficialConditionContext context) =>
            partnerProperties.Conditions.MeetStatus(npcClassId, new PartnerPlayer(context));
        public PartnerConditionRules.MeetRequirement? CurrentPartnerMeetRequirement(int npcClassId) =>
            partnerProperties.Conditions.GetMeetRequirement(npcClassId);

        private sealed class PartnerPlayer : IPartnerConditionPlayer
        {
            private readonly OfficialConditionContext context;
            public PartnerPlayer(OfficialConditionContext context) { this.context = context ?? throw new ArgumentNullException(nameof(context)); }
            public int BaseLevel => context.Level;
            public long GetItemAmount(int classId) => context.ItemCounts.TryGetValue(classId, out long count) ? count : 0;
            public bool IsMapAccessible(int mapClassId) => context.UnlockedMapClassIds.Contains(mapClassId);
            public bool RegionIsActived(int mapClassId, int region) => context.ActivatedMapRegions.Contains((mapClassId, region));
            public bool HasNpc(int npcClassId) => context.NpcFriendshipLevels.ContainsKey(npcClassId);
        }
    }
}

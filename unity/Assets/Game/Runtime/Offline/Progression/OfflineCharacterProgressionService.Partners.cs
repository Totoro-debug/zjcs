using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineCharacterProgressionService
    {
        private OfficialConditionContext NpcConditionContext(OfflinePlayerProgress player)
        {
            var context = new OfficialConditionContext
            {
                Level = player.Level,
                ItemCounts = player.Inventory.Where(value => value != null && value.ItemId > 0)
                    .GroupBy(value => value.ItemId).ToDictionary(group => group.Key, group => group.Sum(value => value.Count)),
                UnlockedMapClassIds = new HashSet<int>(player.UnlockedMapClassIds),
                ActivatedMapRegions = new HashSet<(int, int)>(session.Save.World.ActivatedGoddessRegions
                    .Select(value => (value.MapClassId, value.Region))),
                NpcFriendshipLevels = player.CharacterProperties.NpcFriendships
                    .ToDictionary(value => value.NpcClassId, value => value.Level)
            };
            // HasManualMeetNpcCondition.CheckMeet includes both already met and
            // currently meetable non-auto NPCs, not just the legacy saved flag.
            context.HasManualMeetNpc = catalog.Rows("npc").Any(npc => Integer(npc, "NpcType") == 4
                && !Boolean(npc, "IsAutoMeet") && (context.NpcFriendshipLevels.ContainsKey(Integer(npc, "Id"))
                    || catalog.EvaluateCondition(Map(npc, "CanMeetCondition"), context)));
            return context;
        }

        private NpcMeetStatus NpcMeetStatusOf(Dictionary<string, object> npc, OfficialConditionContext context)
        {
            if (npc == null) return NpcMeetStatus.NotMeet;
            int id = Integer(npc, "Id");
            if (context.NpcFriendshipLevels.ContainsKey(id)) return NpcMeetStatus.HadMeet;
            if (catalog.EvaluateCondition(Map(npc, "CanMeetCondition"), context)) return NpcMeetStatus.CanMeet;
            bool canShow = catalog.EvaluateCondition(Map(npc, "CanShowCondition"), context);
            return NpcRosterRules.MeetStatus(true, false, false, canShow,
                canShow && catalog.EvaluateCondition(Map(npc, "WaitMeetCondition"), context));
        }

        public NpcMeetStatus NpcMeetStatusOf(int npcClassId) => NpcMeetStatusOf(
            catalog.Find("npc", npcClassId, "Id"), NpcConditionContext(Player));

        public bool CanPinNpc => CanPinNpcWithContext(NpcConditionContext(Player));

        private bool CanPinNpcWithContext(OfficialConditionContext context)
        {
            var limit = catalog.Rows("function_limit").FirstOrDefault(row => Integer(row, "Type") == 1011
                && Integer(row, "Key0") == 0 && Integer(row, "Key1") == 0);
            return limit == null || catalog.EvaluateCondition(Map(limit, "Condition"), context);
        }

        public IReadOnlyList<NpcRosterItem> NpcRoster(bool showAll = false,
            NpcRosterSort sort = NpcRosterSort.Friendship, bool reverse = false)
        {
            var player = Player;
            var context = NpcConditionContext(player);
            var records = player.CharacterProperties.NpcFriendships.ToDictionary(value => value.NpcClassId);
            var result = new List<NpcRosterItem>();
            foreach (var npc in catalog.Rows("npc"))
            {
                int id = Integer(npc, "Id");
                if (Integer(npc, "NpcType") != 4 || Integer(npc, "NpcRoleType") == 2) continue;
                // User-confirmed offline policy: the two collaboration groups remain closed.
                if (id >= 11015 && id <= 11022) continue;
                NpcMeetStatus status = NpcMeetStatusOf(npc, context);
                int giftId = npc.TryGetValue("ExclusiveGiftIds", out object rawGifts)
                    && rawGifts is IEnumerable<object> gifts ? Convert.ToInt32(gifts.FirstOrDefault() ?? 0) : 0;
                if (!NpcRosterRules.IsVisible(status, showAll, InventoryCount(player, giftId) > 0)) continue;
                records.TryGetValue(id, out var record);
                // Official transient cards use CreateNpcDataByInfo(..., initialLevel: 1).
                var initial = record == null ? catalog.Rows("npc_friendship_level")
                    .FirstOrDefault(row => Integer(row, "Id") == id && Integer(row, "Level") == 1) : null;
                result.Add(new NpcRosterItem
                {
                    NpcClassId = id, NpcRoleType = Integer(npc, "NpcRoleType"), MeetStatus = status,
                    FriendshipLevel = record?.Level ?? 1,
                    FriendshipExp = record?.FriendshipExp ?? Long(initial, "FriendshipExp"),
                    ActivateTimeTicks = record?.ActivateTimeTicks ?? npcClock().ToUniversalTime().Ticks,
                    IsCollected = record?.IsCollected ?? false, CollectedTimeTicks = record?.CollectedTimeTicks ?? 0,
                    ExclusiveGiftId = giftId, IsAutoMeet = Boolean(npc, "IsAutoMeet")
                });
            }
            bool pinUnlocked = CanPinNpcWithContext(context);
            result.Sort((x, y) => NpcRosterRules.Compare(x, y, sort, reverse, pinUnlocked));
            return result;
        }

        public ProgressionResult SetNpcCollected(int npcClassId, bool collected, string eventId)
        {
            if (npcClassId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid NPC and collection event ID are required.");
            session.Reload();
            return session.ApplyCharacterMutationIfChanged(player =>
            {
                var record = player.CharacterProperties.NpcFriendships.FirstOrDefault(value => value.NpcClassId == npcClassId);
                if (record == null || !CanPinNpcWithContext(NpcConditionContext(player)))
                    throw new InvalidOperationException("Only met NPCs can be pinned after the official function unlock.");
                if (record.IsCollected == collected) return false;
                record.IsCollected = collected;
                record.CollectedTimeTicks = collected ? npcClock().ToUniversalTime().Ticks : 0;
                return true;
            }, eventId);
        }
    }
}

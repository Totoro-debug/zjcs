using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.PartnersUI
{
    // Original talent predicates read the current save on every query. A local
    // transaction or Reload may replace Save.Player and its nested records.
    public sealed class OfflinePartnerTalentPlayer : IPartnerTalentPlayer
    {
        private readonly Func<OfflinePlayerProgress> player;
        private readonly HashSet<int> giftItemIds;

        public OfflinePartnerTalentPlayer(Func<OfflinePlayerProgress> player, OfficialPartnerCatalog catalog)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            giftItemIds = new HashSet<int>(catalog.Rows("item").Where(row => Convert.ToInt32(row["ItemType"]) == 328)
                .Select(row => Convert.ToInt32(row["ClassId"])));
        }

        private OfflinePlayerProgress Current => player()
            ?? throw new InvalidOperationException("Bind the current local player before reading partner talent state.");

        public long GetItemAmount(int id) => Current.Inventory.FirstOrDefault(item => item.ItemId == id)?.Count ?? 0;
        public bool TryGetNpcFriendshipLevel(int id, out int level)
        {
            var record = Current.CharacterProperties.NpcFriendships.FirstOrDefault(npc => npc.NpcClassId == id);
            level = record?.Level ?? 0;
            return record != null;
        }

        public IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int id)
        {
            var record = Current.CharacterProperties.NpcTalentTrees.FirstOrDefault(tree => tree.NpcClassId == id);
            return record == null ? Array.Empty<int>() : record.ActiveNodeIds.ToArray();
        }

        public long GetNpcTalentExp(int id) => Current.CharacterProperties.NpcTalentTrees
            .FirstOrDefault(tree => tree.NpcClassId == id)?.TalentExp ?? 0;

        // NpcTalentTreeUIUtils uses FindItemsByType(NpcGift), independently of
        // gift preference and the broader friendship-gift/unlock-item list.
        public IEnumerable<KeyValuePair<int, long>> GetNpcGifts() => Current.Inventory
            .Where(item => giftItemIds.Contains(item.ItemId))
            .Select(item => new KeyValuePair<int, long>(item.ItemId, item.Count)).ToArray();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.PartnersUI
{
    // NpcInfoParser.ProcessNpcExclusiveGift and NpcHelper.CanSendGiftToNpc;
    // the local store supplies the same selected item/NPC/unlock projections.
    public sealed class OfflinePartnerGiftInventoryRules
    {
        private readonly Dictionary<int, int> itemTypes = new Dictionary<int, int>();
        private readonly Dictionary<int, bool> unlocks = new Dictionary<int, bool>();
        private readonly Dictionary<int, int> exclusiveGifts = new Dictionary<int, int>();
        private readonly OfflinePartnerGiftConfiguration configuration;
        public OfflinePartnerGiftInventoryRules(OfficialPartnerCatalog catalog, OfflinePartnerGiftConfiguration configuration)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            foreach (var row in catalog.Rows("item")) itemTypes.Add(Int(row["ClassId"]), Int(row["ItemType"]));
            foreach (var row in catalog.Rows("npc_unlock_item")) unlocks.Add(Int(row["ClassId"]), (bool)row["CanSendGift"]);
            foreach (var row in catalog.Rows("npc"))
                foreach (object gift in (List<object>)row["ExclusiveGiftIds"])
                    exclusiveGifts.Add(Int(gift), Int(row["Id"]));
        }
        public bool IsGiftType(int itemId) => itemTypes.TryGetValue(itemId, out int type) && (type == 328 || type == 433);
        public bool CanSendGiftToNpc(int giftId, int npcClassId)
        {
            if (!itemTypes.TryGetValue(giftId, out int itemType)) return false;
            var npcInfo = configuration.GetNpcInfo(npcClassId);
            if (npcInfo == null) return false; // Reject an invalid local command before invoking the original rules.
            if (npcInfo.IsStarGodNpc && !npcInfo.GiftPreferenceDict.ContainsKey(giftId)) return false;
            if (itemType == 433)
            {
                if (!unlocks.TryGetValue(giftId, out bool canSendGift) || !canSendGift) return false;
                if (exclusiveGifts.TryGetValue(giftId, out int npcId)) return npcId == npcClassId;
            }
            return true;
        }
        private static int Int(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Live player state and selected table projections for the original dialog.
    public sealed class OfflinePartnerFriendshipDialogServices : IPartnerFriendshipDialogUiServices
    {
        private readonly OfflinePartnerFriendshipProperties properties;
        private readonly OfflinePartnerGiftConfiguration configuration;
        private readonly Func<int> subRank;
        private readonly Func<int, Transform, ItemIcon> borrowGiftIcon;
        private readonly Action<ItemIcon> recycleGiftIcon;
        private readonly Func<string, object> localizedName;
        private readonly Dictionary<int, Dictionary<string, object>> npcs;
        private readonly Dictionary<int, IReadOnlyDictionary<int, PartnerFriendshipDialogLevel>> levels;
        private readonly Dictionary<int, PartnerRewardItemInfo> items;
        private readonly PartnerRewardPreviewItemComparer comparer;

        public OfflinePartnerFriendshipDialogServices(OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration configuration, OfflinePartnerFriendshipProperties properties,
            Func<int> subRank, Func<int, Transform, ItemIcon> borrowGiftIcon,
            Action<ItemIcon> recycleGiftIcon, Func<string, object> localizedName)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.subRank = subRank ?? throw new ArgumentNullException(nameof(subRank));
            this.borrowGiftIcon = borrowGiftIcon ?? throw new ArgumentNullException(nameof(borrowGiftIcon));
            this.recycleGiftIcon = recycleGiftIcon ?? throw new ArgumentNullException(nameof(recycleGiftIcon));
            this.localizedName = localizedName ?? throw new ArgumentNullException(nameof(localizedName));
            npcs = catalog.Rows("npc").ToDictionary(row => Int(row["Id"]));
            levels = catalog.Rows("npc_friendship_level").GroupBy(row => Int(row["Id"]))
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<int, PartnerFriendshipDialogLevel>)
                    group.ToDictionary(row => Int(row["Level"]), row => new PartnerFriendshipDialogLevel { SubRankLimit = Int(row["SubRankLimit"]) }));
            items = catalog.Rows("item").ToDictionary(row => Int(row["ClassId"]), row => new PartnerRewardItemInfo {
                ClassId = Int(row["ClassId"]), ItemType = Int(row["ItemType"]), Quality = Int(row["Quality"]), Rank = Int(row["Rank"]) });
            comparer = new PartnerRewardPreviewItemComparer(Int(catalog.Rows("game_settings").Single()["PlayerExpItemId"]));
        }

        public IProfessionPropertyBindings Properties => properties.Properties;
        public List<ProfessionPropertyType> TravelNpcPropTypeList => properties.TravelNpcPropTypeList;
        public bool IsTravelFunctionLimited => properties.IsTravelFunctionLimited;
        public int SubRank => subRank();
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetFriendshipLevelPropDiff(PartnerFriendshipNpcInfo npc, int fromLevel, int toLevel)
            => properties.GetFriendshipLevelPropDiff(npc, fromLevel, toLevel);
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetLevelProps(int levelPropId, int level)
            => properties.GetLevelProps(levelPropId, level);
        public IReadOnlyDictionary<int, PartnerFriendshipDialogLevel> GetFriendshipLevels(int npcId) => levels[npcId];
        public int CompareRewardItems(int firstId, int secondId) => comparer.RewardItemComparer(items[firstId], items[secondId]);
        public ItemIcon BorrowGiftIcon(int itemId, Transform slot) => borrowGiftIcon(itemId, slot);
        public void RecycleGiftIcon(ItemIcon icon) => recycleGiftIcon(icon);

        public PartnerFriendshipDialogNpc GetNpc(int npcId)
        {
            var info = configuration.GetNpcInfo(npcId) ?? throw new ArgumentOutOfRangeException(nameof(npcId));
            var row = npcs[npcId];
            var condition = row["CanMeetCondition"] as Dictionary<string, object>;
            return new PartnerFriendshipDialogNpc {
                Properties = info.Properties, GiftPreferenceDict = info.GiftPreferenceDict,
                IsStarGodNpc = info.IsStarGodNpc, Name = localizedName((string)row["NameKey"]),
                AssistFriendshipLevel = condition != null && (string)condition["_type"] == "NpcFriendshipLevelCondition"
                    ? Int(condition["FriendshipLevel"]) : (int?)null
            };
        }

        private static int Int(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}

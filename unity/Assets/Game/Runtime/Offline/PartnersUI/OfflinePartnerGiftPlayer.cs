using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // A player view stays bound to the current Save.Player even when a local
    // transaction replaces that object. No copied inventory is kept by the UI.
    public sealed class OfflinePartnerGiftPlayer : IPartnerGiftPlayer
    {
        private sealed class NpcView : IPartnerGiftNpcData
        {
            private readonly OfflinePartnerGiftPlayer owner;
            public NpcView(OfflinePartnerGiftPlayer owner, int id) { this.owner = owner; NpcClassId = id; }
            private OfflineNpcFriendship Current => owner.Friendship(NpcClassId);
            public int NpcClassId { get; }
            public int FriendShipLevel => Current?.Level ?? 0;
            public long FriendShip => owner.FriendshipExperience(Current);
            public string CustomFriendShipTag => owner.readTag(NpcClassId);
        }
        private sealed class ItemView : IProfessionItemData
        {
            private readonly OfflinePartnerGiftPlayer owner;
            public ItemView(OfflinePartnerGiftPlayer owner, int id) { this.owner = owner; Id = id; }
            private OfflineItemStack Current => owner.session.Save.Player.Inventory.FirstOrDefault(item => item.ItemId == Id);
            public int Id { get; }
            public long Count => Current?.Count ?? 0;
            public IProfessionItemInfo Info => owner.getItemInfo(Id)
                ?? throw new InvalidOperationException("Original partner item presentation is unbound: " + Id);
            public Quality GetQuality() => Info.Quality; // Gifts have no IItemParamAccessorQuality override.
            public bool CanExpire() => Current?.UnitRecords.Any(record => record.ExpiresAtUnixSeconds > 0) == true;
        }
        private sealed class Snapshot
        {
            public Dictionary<int, long> Items;
            public Dictionary<int, (int Level, long Exp)> Npcs;
        }
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflinePartnerGiftConfiguration configuration;
        private readonly OfflinePartnerGiftInventoryRules inventoryRules;
        private readonly Func<int, IProfessionItemInfo> getItemInfo;
        private readonly Func<bool> addressListLimited;
        private readonly Func<int, string> readTag;
        private readonly Action<int, string> writeTag;
        private readonly Func<long> clock;
        private readonly Dictionary<int, int> meetTypes;
        private readonly Dictionary<int, NpcView> npcViews = new Dictionary<int, NpcView>();
        private readonly Dictionary<int, ItemView> itemViews = new Dictionary<int, ItemView>();
        private Snapshot known;
        public OfflinePartnerGiftPlayer(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration configuration, Func<int, IProfessionItemInfo> getItemInfo,
            Func<bool> addressListLimited, Func<long> clock = null)
            : this(session, catalog, configuration, getItemInfo, addressListLimited,
                new OfflinePartnerFriendshipTags(session, configuration), clock) { }
        private OfflinePartnerGiftPlayer(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration configuration, Func<int, IProfessionItemInfo> getItemInfo,
            Func<bool> addressListLimited, OfflinePartnerFriendshipTags tags, Func<long> clock)
            : this(session, catalog, configuration, getItemInfo, addressListLimited, tags.Get, tags.SetFromPlayer, clock) { }
        public OfflinePartnerGiftPlayer(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration configuration, Func<int, IProfessionItemInfo> getItemInfo,
            Func<bool> addressListLimited, Func<int, string> readTag, Action<int, string> writeTag, Func<long> clock = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.getItemInfo = getItemInfo ?? throw new ArgumentNullException(nameof(getItemInfo));
            this.addressListLimited = addressListLimited ?? throw new ArgumentNullException(nameof(addressListLimited));
            this.readTag = readTag ?? throw new ArgumentNullException(nameof(readTag));
            this.writeTag = writeTag ?? throw new ArgumentNullException(nameof(writeTag));
            this.clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            inventoryRules = new OfflinePartnerGiftInventoryRules(catalog, configuration);
            meetTypes = catalog.Rows("npc").ToDictionary(row => Convert.ToInt32(row["Id"]), row => (bool)row["IsAutoMeet"] ? 0 : 1);
            known = Capture();
        }
        public int SubRank => session.Save.Player.SubRank;
        public bool IsAddressListFunctionLimited => addressListLimited();
        public IEnumerable<KeyValuePair<long, IPartnerGiftNpcData>> Npcs => session.Save.Player.CharacterProperties.NpcFriendships
            .Select(npc => new KeyValuePair<long, IPartnerGiftNpcData>(npc.NpcClassId, GetNpcByClassId(npc.NpcClassId))).ToArray();
        public event Action<int, int> OnAddNewNpc;
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        public event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        public event Action<int> OnNpcFriendshipUpdate;
        public IPartnerGiftNpcData GetNpcByClassId(int id)
        {
            if (Friendship(id) == null) return null;
            if (!npcViews.TryGetValue(id, out var view)) npcViews.Add(id, view = new NpcView(this, id));
            return view;
        }
        public IEnumerable<IProfessionItemData> GetAllCanSendToNpcGift(int id) => configuration.GetNpcInfo(id) == null
            ? Enumerable.Empty<IProfessionItemData>() : session.Save.Player.Inventory
                .Where(item => item.Count > 0 && inventoryRules.IsGiftType(item.ItemId) && inventoryRules.CanSendGiftToNpc(item.ItemId, id))
                .Select(item => (IProfessionItemData)Item(item.ItemId)).ToArray();
        public long GetItemAmount(int id) => session.Save.Player.Inventory.FirstOrDefault(item => item.ItemId == id)?.Count ?? 0;
        public void SetNpcFriendshipTag(int id, string tag) => writeTag(id, tag);

        public ProgressionResult GiveGifts(int id, IReadOnlyDictionary<int, long> gifts, string eventId, bool confirmOverflow = false)
        {
            if (id <= 0 || gifts == null || gifts.Count == 0 || string.IsNullOrWhiteSpace(eventId)) return Invalid("Select partner gifts and supply a transaction identity.");
            // Copy the command before reloading or invoking any event subscriber.
            var selected = gifts.ToDictionary(gift => gift.Key, gift => gift.Value);
            Reload();
            if (session.Save.Player.AppliedCharacterEventIds.Contains(eventId)) return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            var npc = configuration.GetNpcInfo(id);
            var friendship = Friendship(id);
            if (npc == null || friendship == null) return Invalid("The partner has not been met.");
            int maximum = configuration.GetMaxSubRankLevel(id, SubRank);
            if (maximum <= 0 || friendship.Level >= maximum) return Invalid("Partner friendship is limited by the current rank.");
            foreach (var gift in selected)
                if (gift.Key <= 0 || gift.Value <= 0 || GetItemAmount(gift.Key) < gift.Value
                    || !inventoryRules.IsGiftType(gift.Key) || !inventoryRules.CanSendGiftToNpc(gift.Key, id)
                    || configuration.GetGiftInfo(gift.Key) == null) return Invalid("Selected partner gifts are unavailable.");
            int added = configuration.GetAllExpByDict(selected, npc.GiftPreferenceDict);
            if (added <= 0) return Invalid("Selected partner gifts have no valid friendship experience.");
            // Keep the existing local transaction's overflow rejection. The UI
            // preview continues to execute the original unchecked parser body.
            try
            {
                long checkedTotal = 0;
                foreach (var gift in selected)
                {
                    double factor = npc.GiftPreferenceDict.TryGetValue(gift.Key, out double preferred)
                        ? preferred : configuration.NpcGiftDefaultPreferenceFactor;
                    checkedTotal = checked(checkedTotal + NpcGiftRules.Experience(configuration.GetGiftInfo(gift.Key).Friendship, gift.Value, factor));
                }
                if (checkedTotal > int.MaxValue || checkedTotal != added) return Invalid("Partner gift experience exceeds the supported range.");
            }
            catch (OverflowException) { return Invalid("Partner gift experience exceeds the supported range."); }
            catch (ArgumentOutOfRangeException) { return Invalid("Partner gift experience exceeds the supported range."); }
            long current = FriendshipExperience(friendship);
            long next;
            try { next = checked(current + added); }
            catch (OverflowException) { return Invalid("Partner friendship experience is exhausted."); }
            var last = configuration.GetMaxLevelInfo(id);
            if (last == null) return Invalid("The selected partner friendship maximum is missing.");
            if (next > last.FriendshipExp && !confirmOverflow) return new ProgressionResult {
                Status = ProgressionResultStatus.NotReady, Message = "Confirm gifts beyond the maximum friendship experience." };
            var before = Capture();
            long now = clock();
            var result = session.ApplyCharacterMutation(player => {
                foreach (var gift in selected)
                    if (!OfflineInventoryOperations.Consume(player, gift.Key, gift.Value, now).IsApplied)
                        throw new InvalidOperationException("Partner gift inventory changed before saving.");
                var target = player.CharacterProperties.NpcFriendships.First(value => value.NpcClassId == id);
                target.FriendshipExp = next;
                target.Level = configuration.GetFriendshipLevelByExp(id, next, player.SubRank);
            }, eventId);
            if (result.IsApplied) Publish(before);
            return result;
        }
        public void Reload()
        {
            session.Reload();
            Publish(known);
        }
        private OfflineNpcFriendship Friendship(int id) => session.Save.Player.CharacterProperties.NpcFriendships.FirstOrDefault(npc => npc.NpcClassId == id);
        private long FriendshipExperience(OfflineNpcFriendship friendship)
        {
            if (friendship == null) return 0;
            // Retain the existing local gift transaction's legacy-save recovery,
            // and expose the same effective value to the original progress bar.
            return friendship.FriendshipExp == 0 && friendship.Level > 0
                ? configuration.Get(friendship.NpcClassId, friendship.Level)?.FriendshipExp ?? 0 : friendship.FriendshipExp;
        }
        private ItemView Item(int id)
        {
            if (!itemViews.TryGetValue(id, out var item)) itemViews.Add(id, item = new ItemView(this, id));
            return item;
        }
        private Snapshot Capture() => new Snapshot {
            Items = session.Save.Player.Inventory.ToDictionary(item => item.ItemId, item => item.Count),
            Npcs = session.Save.Player.CharacterProperties.NpcFriendships.ToDictionary(npc => npc.NpcClassId, npc => (npc.Level, npc.FriendshipExp)) };
        private void Publish(Snapshot before)
        {
            var after = Capture();
            known = after; // Reentrant observers see a complete committed snapshot.
            foreach (int id in before.Items.Keys.Union(after.Items.Keys))
            {
                before.Items.TryGetValue(id, out long previous); after.Items.TryGetValue(id, out long current);
                if (previous != current && inventoryRules.IsGiftType(id)) OnItemCountUpdate?.Invoke(Item(id), previous, null);
            }
            foreach (var npc in after.Npcs)
            {
                if (!before.Npcs.TryGetValue(npc.Key, out var previous)) OnAddNewNpc?.Invoke(npc.Key, meetTypes[npc.Key]);
                else
                {
                    if (previous.Level != npc.Value.Level) OnNpcFriendshipLevelUpdate?.Invoke(npc.Key, previous.Level, npc.Value.Level);
                    if (previous.Level != npc.Value.Level || previous.Exp != npc.Value.Exp) OnNpcFriendshipUpdate?.Invoke(npc.Key);
                }
            }
        }
        private static ProgressionResult Invalid(string message) => new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };
    }
}

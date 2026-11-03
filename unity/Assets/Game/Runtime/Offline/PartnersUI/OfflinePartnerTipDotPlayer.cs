using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Replaces message delivery at the original tip-node player boundary. The
    // owning window synchronizes after a local transaction or explicit Reload.
    public sealed class OfflinePartnerTipDotPlayer : IPartnerTipDotPlayer
    {
        private sealed class Snapshot
        {
            public Dictionary<int, long> Items;
            public Dictionary<int, PartnerTipDotNpcRecord> Npcs;
        }

        private readonly OfflineMainlineProgressSession session;
        private readonly OfflinePartnerGiftConfiguration gifts;
        private readonly OfflinePartnerTalentPlayer talents;
        private readonly Func<int, bool> functionLimited;
        private readonly Func<int, ItemType> getItemType;
        private readonly Dictionary<int, NpcMeetType> meetTypes;
        private Snapshot known;

        public OfflinePartnerTipDotPlayer(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerGiftConfiguration gifts, Func<int, bool> functionLimited, Func<int, ItemType> getItemType)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.gifts = gifts ?? throw new ArgumentNullException(nameof(gifts));
            this.functionLimited = functionLimited ?? throw new ArgumentNullException(nameof(functionLimited));
            this.getItemType = getItemType ?? throw new ArgumentNullException(nameof(getItemType));
            talents = new OfflinePartnerTalentPlayer(() => session.Save.Player, catalog);
            meetTypes = catalog.Rows("npc").ToDictionary(row => Convert.ToInt32(row["Id"]),
                row => (bool)row["IsAutoMeet"] ? NpcMeetType.Auto : NpcMeetType.Manual);
            NpcManager = new PartnerTipDotNpcManager(ReadNpcs);
            known = Capture();
        }

        public int SubRank => session.Save.Player.SubRank;
        public PartnerTipDotNpcManager NpcManager { get; }
        public bool IsFunctionLimit(int function) => functionLimited(function);
        public long GetItemAmount(int id) => talents.GetItemAmount(id);
        public IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int id) => talents.GetNpcTalentTreeUnlockedNodeIds(id);
        public event Action<int, NpcMeetType> OnAddNewNpc;
        public event Action<int> OnRemoveNpc;
        public event Action<PartnerTipDotItem, long, object> OnItemCountUpdate;
        public event Action<int, int> OnReceiveNpcProfileReward;
        public event Action<int, int, int> OnNpcFriendshipLevelUpdate;

        private IEnumerable<PartnerTipDotNpcRecord> ReadNpcs() => session.Save.Player.CharacterProperties.NpcFriendships
            .Select(npc => new PartnerTipDotNpcRecord {
                NpcClassId = npc.NpcClassId, FriendShipLevel = npc.Level,
                // Match the existing gift/save adapter's legacy effective XP.
                FriendShip = npc.FriendshipExp == 0 && npc.Level > 0
                    ? gifts.Get(npc.NpcClassId, npc.Level)?.FriendshipExp ?? 0 : npc.FriendshipExp,
                ReceivedProfileRewards = new HashSet<int>(npc.ReceivedProfileRewards)
            }).ToArray();

        private Snapshot Capture() => new Snapshot {
            Items = session.Save.Player.Inventory.ToDictionary(item => item.ItemId, item => item.Count),
            Npcs = ReadNpcs().ToDictionary(npc => npc.NpcClassId)
        };

        public void Reload()
        {
            session.Reload();
            Synchronize();
        }

        public void Synchronize()
        {
            Snapshot before = known, after = Capture();
            // Publish only complete current state, including during reentrant
            // graph queries. Synchronizing an unchanged save emits no events.
            known = after;
            foreach (int id in before.Npcs.Keys)
                if (!after.Npcs.ContainsKey(id)) OnRemoveNpc?.Invoke(id);
            foreach (var entry in after.Npcs)
            {
                var npc = entry.Value;
                bool existed = before.Npcs.TryGetValue(entry.Key, out var old);
                if (!existed) OnAddNewNpc?.Invoke(entry.Key, meetTypes[entry.Key]);
                else if (old.FriendShipLevel != npc.FriendShipLevel)
                    OnNpcFriendshipLevelUpdate?.Invoke(entry.Key, old.FriendShipLevel, npc.FriendShipLevel);
                // An initial received collection comes with OnAddNewNpc. A
                // later receipt corresponds to the original successful ACK.
                if (existed)
                    foreach (int chapter in npc.ReceivedProfileRewards)
                        if (!old.ReceivedProfileRewards.Contains(chapter)) OnReceiveNpcProfileReward?.Invoke(entry.Key, chapter);
            }
            foreach (int id in before.Items.Keys.Union(after.Items.Keys))
            {
                before.Items.TryGetValue(id, out long previous);
                after.Items.TryGetValue(id, out long current);
                if (previous == current) continue;
                ItemType type = getItemType(id);
                OnItemCountUpdate?.Invoke(new PartnerTipDotItem { Info = new PartnerTipDotItemInfo { ItemType = type } }, previous, null);
            }
        }
    }
}

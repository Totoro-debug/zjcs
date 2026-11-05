using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerDetailWindowServices : IPartnerDetailWindowServices
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflinePartnerGiftPlayer gifts;
        private readonly OfflinePartnerProfileUiServices profiles;
        private readonly OfflinePartnerDetailConfiguration configuration;
        private readonly Action<GameObject> close;
        private readonly Dictionary<int, NpcData> npcs = new Dictionary<int, NpcData>();
        public event Action<int> OnRemoveNpc;
        public OfflinePartnerDetailWindowServices(OfflineMainlineProgressSession session, OfflinePartnerGiftPlayer gifts,
            OfflinePartnerProfileUiServices profiles, OfflinePartnerDetailConfiguration configuration, Action<GameObject> close)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.gifts = gifts ?? throw new ArgumentNullException(nameof(gifts));
            this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.close = close ?? throw new ArgumentNullException(nameof(close));
        }
        public IPartnerDetailWindowNpcData GetNpcByClassId(int npcId)
        {
            var gift = gifts.GetNpcByClassId(npcId);
            if (gift == null) return null;
            if (!npcs.TryGetValue(npcId, out var result)) npcs.Add(npcId, result = new NpcData(
                new OfflinePartnerDetailNpcData(session, gift), profiles.GetNpcData(npcId)));
            return result;
        }
        public PartnerDetailPresentationInfo GetNpcInfo(int npcId) => configuration.GetNpcInfo(npcId);
        public void Close(GameObject window) => close(window);
        public void NotifyRemoved(int npcId) { npcs.Remove(npcId); OnRemoveNpc?.Invoke(npcId); }
        private sealed class NpcData : IPartnerDetailWindowNpcData
        {
            private readonly IPartnerDetailNpcData detail;
            private readonly IPartnerProfileNpcData profile;
            internal NpcData(IPartnerDetailNpcData detail, IPartnerProfileNpcData profile) { this.detail = detail; this.profile = profile; }
            public int NpcClassId => detail.NpcClassId;
            public int FriendShipLevel => detail.FriendShipLevel;
            public long FriendShip => detail.FriendShip;
            public string CustomFriendShipTag => detail.CustomFriendShipTag;
            public bool IsCollected => detail.IsCollected;
            public IReadOnlyCollection<int> ReceivedProfileRewards => profile.ReceivedProfileRewards;
        }
    }
}

using System;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using ConditionType = Zjcs.Official.Partners.PartnerConditionRules.ConditionType;

namespace Zjcs.Offline.PartnersUI
{
    // Owner-scoped bridge to the original condition event entry points.
    public sealed class OfflinePartnerTipDotConditions : IDisposable
    {
        private readonly IPartnerTipDotPlayer player;
        private bool disposed;
        public PartnerConditionHandler Handler { get; }

        public OfflinePartnerTipDotConditions(IPartnerTipDotPlayer player,
            Action<Exception, ConditionType> reportFailure, Action onConditionMapChange = null)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            Handler = new PartnerConditionHandler(player, reportFailure, onConditionMapChange);
            player.OnAddNewNpc += OnAddNewNpc;
            player.OnNpcFriendshipLevelUpdate += Handler.MPlayer_OnNpcFriendshipLevelUpdate;
            player.OnItemCountUpdate += Handler.MPlayer_OnItemCountUpdate;
        }

        private void OnAddNewNpc(int id, NpcMeetType type) => Handler.MPlayer_OnAddNewNpc(id, (int)type);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Handler.Destroy();
            player.OnAddNewNpc -= OnAddNewNpc;
            player.OnNpcFriendshipLevelUpdate -= Handler.MPlayer_OnNpcFriendshipLevelUpdate;
            player.OnItemCountUpdate -= Handler.MPlayer_OnItemCountUpdate;
        }
    }
}

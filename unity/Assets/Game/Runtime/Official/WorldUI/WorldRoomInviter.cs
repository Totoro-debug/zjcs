using System;
using System.Threading.Tasks;
using Zjcs.Official.PlayerPlay;

namespace Zjcs.Official.WorldUI
{
    // Original RoomInviter async operation, retaining both checks before RPC.
    public sealed class WorldRoomInviter : WorldInviterBase
    {
        private IWorldContextInviteServices services;
        private static readonly PlayerPlayRoomAction RoomAction = new PlayerPlayRoomAction();
        public void Initialize(IWorldContextInviteServices value)
        { services = value ?? throw new ArgumentNullException(nameof(value)); }

        public override Task<bool> CheckCanInvite(PlayerPlayStateModule module)
        { return module.TryDoAction(RoomAction); }

        public override Task<int> Invite(WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        { return Execute(services, actor, target); }

        public static async Task<int> Execute(IWorldContextInviteServices services,
            WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        {
            if (actor.IsNpc) return 0;
            int code = !services.RoomPlayerCount.HasValue ? 24603 :
                services.RoomPlayerCount.Value >= target.GetTeamMaxPlayer() ? 24604 : 0;
            if (code != 0)
            {
                services.ShowError(code, null, true);
                return code;
            }
            WorldContextInviteResult result = await services.OperPlayerRoomInvite(actor, target);
            if (result.ErrorCode != 0) services.ShowError(result.ErrorCode, result.Data, true);
            else services.Team.SystemMessage(services.Team.Format("room_Invite_tip"));
            return result.ErrorCode;
        }
    }
}

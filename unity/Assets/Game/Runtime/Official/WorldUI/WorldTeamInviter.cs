using System;
using System.Threading.Tasks;
using Zjcs.Official.PlayerPlay;

namespace Zjcs.Official.WorldUI
{
    // Recovered from TeamInviter's original async state machine. RPC scheduling is
    // replaced by Task, retaining the checks, await boundary and error/tip order.
    public sealed class WorldTeamInviter : WorldInviterBase
    {
        private IWorldTeamInviteServices services;
        private static readonly PlayerPlayTeamAction TeamAction = new PlayerPlayTeamAction();

        public void Initialize(IWorldTeamInviteServices value)
        { services = value ?? throw new ArgumentNullException(nameof(value)); }

        public override async Task<bool> CheckCanInvite(PlayerPlayStateModule playerPlayStateModule)
        { return await playerPlayStateModule.TryDoAction(TeamAction); }

        public override Task<int> Invite(WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        { return Execute(services, actor, target); }

        public static async Task<int> Execute(IWorldTeamInviteServices services,
            WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (CheckTeamFullByTarget(services, target))
            {
                services.ShowError(6102, true);
                return 6102;
            }
            // TeamUtils.CheckAllowInvite: leader first; member permission is not
            // read when the original leader branch has already succeeded.
            if (!services.IsLeader && !services.AllowMemberInvite)
            {
                services.ShowError(6168, false);
                return 6168;
            }
            WorldTeamInviteResult result = await services.OperPlayerTeamInvite(actor, target);
            int code = result.ErrorCode;
            if (code == 0)
            {
                if (actor.IsNpc)
                    services.SystemMessage(services.Format("TeamOperBoradcast.JoinMsg",
                        services.NpcName(actor.NpcClassId)));
                else
                    services.SystemMessage(services.Format("TeamInviteSelectPlayerPanel.InviteMsg"));
            }
            else if (target != null && target.TryGetLimitErrorTip(false, code, out string tip))
                services.SystemMessage(tip);
            else
                services.ShowError(code, true);
            return code;
        }
    }
}

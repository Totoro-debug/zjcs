using System;
using System.Threading.Tasks;
using Zjcs.Official.PlayerPlay;

namespace Zjcs.Official.WorldUI
{
    // Original ChallengeInviter: append the response before broadcasting.
    public sealed class WorldChallengeInviter : WorldInviterBase
    {
        private IWorldContextInviteServices services;
        private static readonly PlayerPlayTeamAction TeamAction = new PlayerPlayTeamAction();
        public void Initialize(IWorldContextInviteServices value)
        { services = value ?? throw new ArgumentNullException(nameof(value)); }

        public override Task<bool> CheckCanInvite(PlayerPlayStateModule module)
        { return module.TryDoAction(TeamAction); }

        public override Task<int> Invite(WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        { return Execute(services, actor, target); }

        public static async Task<int> Execute(IWorldContextInviteServices services,
            WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        {
            if (actor.IsNpc) return 0;
            if (CheckTeamFullByTarget(services.Team, target))
            {
                services.ShowError(6102, null, true);
                return 6102;
            }
            WorldContextInviteResult result = await services.OperPlayerTeamChallengeInviteJoin(actor.TargetId);
            if (result.ErrorCode != 0) services.ShowError(result.ErrorCode, result.Data, true);
            else
            {
                if (result.Data != null) services.AppendChallengeInvitation(result.Data);
                services.Team.SystemMessage(services.Team.Format("TeamInviteSelectPlayerPanel.InviteMsg"));
            }
            return result.ErrorCode;
        }
    }
}

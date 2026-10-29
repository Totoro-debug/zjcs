using System;
using System.Threading.Tasks;
using Zjcs.Official.PlayerPlay;

namespace Zjcs.Official.WorldUI
{
    // Original CompanionInviter's name lookup, popup and invitation await order.
    public sealed class WorldCompanionInviter : WorldInviterBase
    {
        private IWorldContextInviteServices services;
        public void Initialize(IWorldContextInviteServices value)
        { services = value ?? throw new ArgumentNullException(nameof(value)); }

        public override Task<bool> CheckCanInvite(PlayerPlayStateModule module)
        { return Task.FromResult(true); }

        public override Task<int> Invite(WorldTeamInviteActor actor, IWorldTeamInviteTarget target)
        { return Execute(services, actor); }

        public static async Task<int> Execute(IWorldContextInviteServices services, WorldTeamInviteActor actor)
        {
            if (actor.IsNpc) return 0;
            string name;
            if (!services.TryGetLiteInfoNameFromCache(actor, out name) || string.IsNullOrEmpty(name))
                name = await services.GetLiteInfoName(actor) ?? string.Empty;
            var confirmation = new TaskCompletionSource<bool>();
            await services.ShowCompanionInviteConfirmation(name,
                () => confirmation.TrySetResult(true), () => confirmation.TrySetResult(false));
            if (!await confirmation.Task) return 112;
            int code = await services.SendCompanionInvite(actor.TargetId);
            if (code != 0) services.ShowError(code, null, true);
            else services.Team.SystemMessage(services.Team.Format("ActivityCompanion.InviteSent"));
            return code;
        }
    }
}

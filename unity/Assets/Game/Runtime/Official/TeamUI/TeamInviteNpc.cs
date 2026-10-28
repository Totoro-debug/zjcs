using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zjcs.Official.TeamUI
{
    // Original TeamInviteNpc, including its async MoveNext body. Only parser,
    // player manager, logging and RPC transport calls use injected boundaries.
    public sealed class TeamInviteNpc : ITeamInvitePlayer
    {
        private readonly ITeamInviteNpcServices services;
        private List<TeamInviteSubPanelBase.TeamInviteSortType> mSortTypes;
        public TeamInviteNpc(ITeamInviteNpcServices services) { this.services = services; }
        public string GetEmptyDescKey() => "teaminvitepanel_no_indigenous_people";
        public int GetAssistCount(int playType, long id) => services.GetNpcAssistCount(playType, id);
        public async Task<List<TeamInvitePlayerInfoData>> LoadPlayers(TeamTargetData target, CancellationToken token)
        {
            List<TeamInviteNpcTarget> targets = GetAssistNpcActorTargetDatas();
            if (targets.Count > 0)
                return await services.GetTeamInviteInfoData(targets, target, token);
            return null;
        }
        public IReadOnlyList<TeamInviteSubPanelBase.TeamInviteSortType> GetSortTypes()
        {
            if (mSortTypes == null)
                mSortTypes = new List<TeamInviteSubPanelBase.TeamInviteSortType>
                {
                    TeamInviteSubPanelBase.TeamInviteSortType.FriendShip,
                    TeamInviteSubPanelBase.TeamInviteSortType.CombatRating,
                    TeamInviteSubPanelBase.TeamInviteSortType.LightAdd,
                    TeamInviteSubPanelBase.TeamInviteSortType.DarkAdd,
                    TeamInviteSubPanelBase.TeamInviteSortType.FireAdd,
                    TeamInviteSubPanelBase.TeamInviteSortType.WaterAdd,
                    TeamInviteSubPanelBase.TeamInviteSortType.WindAdd,
                    TeamInviteSubPanelBase.TeamInviteSortType.LightReduce,
                    TeamInviteSubPanelBase.TeamInviteSortType.DarkReduce,
                    TeamInviteSubPanelBase.TeamInviteSortType.FireReduce,
                    TeamInviteSubPanelBase.TeamInviteSortType.WaterReduce,
                    TeamInviteSubPanelBase.TeamInviteSortType.WindReduce
                };
            return mSortTypes;
        }
        private List<TeamInviteNpcTarget> GetAssistNpcActorTargetDatas()
        {
            var result = new List<TeamInviteNpcTarget>();
            foreach (int npcClassId in services.GetNpcsExcludingMapExploreBoss())
            {
                if (!services.IsFightAssist(npcClassId) || services.IsNpcAssistLimit(npcClassId)) continue;
                TeamInviteNpcRecord npc = services.GetNpc(npcClassId);
                if (npc == null)
                {
                    npc = services.CreateNpcDataByInfo(services.MainPlayerId, npcClassId);
                    long id = unchecked(services.MainPlayerId * 1000000 + npcClassId);
                    if (id > 0) npc.NpcId = id;
                    else services.Error($"TeamInviteNpc: 生成的NPC队友ID小于0, 很可能是溢出了！playerId = {services.MainPlayerId}, npcId = {npcClassId}");
                    services.Info($"TeamInviteNpc: 本次邀请的NpcClassId: {npcClassId} 生成的NpcId为：{npc.NpcId}");
                }
                result.Add(new TeamInviteNpcTarget(services, npc.NpcId, npc.NpcClassId,
                    npc.PlayerId, services.PlanesRank));
            }
            return result;
        }
    }
}

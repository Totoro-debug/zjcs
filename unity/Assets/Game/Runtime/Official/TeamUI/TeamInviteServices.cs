using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    // Local boundaries for the original manager/parser calls. These do not decide
    // eligibility, sort priorities, invitation state, or any server quota.
    public interface ITeamInviteNpcServices
    {
        long MainPlayerId { get; }
        int PlanesRank { get; }
        IEnumerable<int> GetNpcsExcludingMapExploreBoss();
        bool IsFightAssist(int npcClassId);
        bool IsNpcAssistLimit(int npcClassId);
        TeamInviteNpcRecord GetNpc(int npcClassId);
        TeamInviteNpcRecord CreateNpcDataByInfo(long playerId, int npcClassId);
        Task<List<TeamInvitePlayerInfoData>> GetTeamInviteInfoData(
            List<TeamInviteNpcTarget> targets, TeamTargetData target, CancellationToken token);
        int GetNpcAssistCount(int playType, long id);
        string GetNpcFigurePath(int npcClassId, Sex sex);
        bool CheckNpcCanAssist(int playType, long id);
        void Error(string message);
        void Info(string message);
    }

    public sealed class TeamInviteNpcRecord
    {
        public long NpcId, PlayerId;
        public int NpcClassId;
        public long FriendShip;
        public int FriendShipLevel;
        public bool IsCollected;
        public DateTime CollectedTime;
    }

    // Common.NpcTargetData: preserve the unfilled level and the inherited -1
    // server ID of the four-argument constructor used by TeamInviteNpc.
    public sealed class TeamInviteNpcTarget : IProfessionInfoNpcTarget
    {
        private readonly ITeamInviteNpcServices services;
        public ActorTargetType TargetType => ActorTargetType.Npc;
        public long TargetId { get; set; }
        public long ServerId { get; set; } = -1;
        public bool IsNpc => true;
        public long OwnerPlayerId { get; set; }
        public int NpcClassId { get; set; }
        public int NpcLevel, NpcSubRank, CreatePlanesRank;
        public bool IsValidForLevelAndRank => NpcLevel > 0 && NpcSubRank != 0;
        public TeamInviteNpcTarget(ITeamInviteNpcServices services, long id, int npcClassId,
            long owner, int createRank)
        {
            this.services = services;
            TargetId = id; NpcClassId = npcClassId; OwnerPlayerId = owner;
            CreatePlanesRank = createRank;
        }
        public string GetFigurePath(Sex sex) => services.GetNpcFigurePath(NpcClassId, sex);
        public bool CheckActorCanAssist(int playType) => services.CheckNpcCanAssist(playType, TargetId);
        public override string ToString() => $"{ServerId}.{TargetType}.{OwnerPlayerId}.{TargetId}.{NpcClassId}|{NpcLevel}|{NpcSubRank}|{CreatePlanesRank}";
    }

    public enum TeamInviteActorType { RealPlayer, AssistPlayer, AssistNpc }
    public enum TeamInvitePlayerTeamStatus { None, Leader, Member }

    // Consumed Common.TeamInvitePlayerInfoData fields. The host builds NPC stats
    // with NpcFightData and the approved player-level/subrank projection.
    public sealed class TeamInvitePlayerInfoData
    {
        public IProfessionInfoTarget TargetData;
        public string Name, Icon;
        public Sex Sex;
        public object AppearanceData;
        public int Profession, SubRank, Level, ServerId;
        public TeamTargetData MatchTeamTarget;
        public long CombatRating, AccountId;
        public bool TeamIsLimit, RoomIsLimit, ChallengeUnlocked, Online;
        public TeamInvitePlayerTeamStatus TeamStatus;
        public Dictionary<PropType, long> PropTypes;
        public TeamInviteActorType ActorType;
        public long PlayerId => TargetData.TargetId;
    }

    public interface ITeamInvitePlayer
    {
        Task<List<TeamInvitePlayerInfoData>> LoadPlayers(TeamTargetData target, CancellationToken token);
        string GetEmptyDescKey();
        IReadOnlyList<TeamInviteSubPanelBase.TeamInviteSortType> GetSortTypes();
        int GetAssistCount(int playType, long id);
    }

    public interface ITeamInviteComparerServices
    {
        int MainPlayerProfession { get; }
        bool IsNpcPinLimited { get; }
        TeamInviteNpcRecord GetNpc(int npcClassId);
        bool CanJointTogether(int targetProfession, int mainProfession);
        bool IsNpcAssistLimit(int npcClassId);
        (long ToTarget, long ToPlayer)? ToFriendshipData(IProfessionInfoTarget target);
        void RefreshFestivalActivityData();
        bool HasFestivalBossNpc(int activityId, int npcClassId);
        void Error(string message);
    }
}

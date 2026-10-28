using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // Consumed fields of Common.Team and TeamPlayerInfo. List order is significant.
    public sealed class TeamPanelPlayer
    {
        public long PlayerId;
        public IProfessionInfoTarget TargetData;
        public int Profession;
    }

    public sealed class TeamPanelTeam
    {
        public long TeamLeaderId;
        public TeamTargetData Target;
        public readonly List<TeamPanelPlayer> Players = new List<TeamPanelPlayer>();
        public TeamPanelPlayer GetPlayer(long id) => Players.Find(value => value.PlayerId == id);
    }

    public sealed class TeamPanelBriefInfo
    {
        public IProfessionInfoTarget TargetData;
        public long Id;
        public int Level, SubRank;
        // Exact Main(-1,-1) equipped ActiveSkill dictionary, projected by the host.
        public IDictionary<int, object> ActiveSkills;
        public Func<int, int> ActiveSkillSlotLevel;
    }

    public interface ITeamPanelSkillIcon
    {
        bool IsSkill { get; }
        bool IsEmpty { get; }
        void SetClickPopupInfoDialog(Action<GameObject> callback);
        void SetSampleInfoPropDataProvider(GameObject dialog, long id, int level, int subRank);
        void ShowSkillLevel(bool show, int level, bool overBaseLevel);
        void ShowEmptyLevel(bool show, int level, bool overBaseLevel);
    }

    public enum TeamPanelInviteType { Player, Robot, Challenge, Room, Companion }

    public interface ITeamPanelServices
    {
        bool ManagerAvailable { get; }
        bool MainPlayerAvailable { get; }
        long MainPlayerId { get; }
        int MainPlayerProfession { get; }
        TeamPanelTeam MainTeam { get; }
        TeamPanelBriefInfo MainPlayerBriefInfo { get; }
        bool IsOnlyOnPrivateMap { get; }
        bool AllowAcceptJoinTeamWhenGame { get; }
        bool AllowTeamOperationByGame { get; }
        bool CheckAllowInvite();
        TeamTargetData RecordedTarget { get; set; }
        TeamTargetData NoneTarget { get; }
        ITeamIconServices Icons { get; }
        ITeamPlayerHpServices Hp { get; }
        ICharacterPortraitManager Portraits { get; }
        ITeamTargetServices Targets { get; }
        IWorldMemberInviteServices MemberInvite { get; }
        PlayerPlayStateModule PlayerPlayStateModule { get; }
        event Action TeamUpdated;
        event Action<TeamPanelPlayer> TeamPlayerAdded;
        event Action<TeamPanelPlayer> TeamPlayerRemoved;
        event Action<long, long> TeamLeaderUpdated;
        event Action TeamTargetUpdated;
        Task<TeamPanelBriefInfo> GetPlayerBriefInfo(IProfessionInfoTarget target, bool invokeByPlayer);
        void QueryTargets(Action<List<TeamTargetData>, int> complete);
        void UpdateTeamTarget(TeamTargetData target, Action<TeamTargetData> complete);
        void OpenInvite(TeamPanelInviteType page, TeamTargetData target, List<long> excludedPlayers = null);
        // Original chat and nearby callbacks reach the approved offline access policy.
        void OpenAssemble(TeamTargetData target, List<int> professions);
        void OpenNearby(TeamTargetData target, List<TeamTargetData> targets);
        void Close(GameObject panel);
        void Confirm(Component owner, string key, Action confirmed);
        void OperPlayerTeamDismiss();
        void OperPlayerTeamKick(long targetId);
        void OperPlayerTeamLeave();
        void ShowError(int errorCode, bool useDefaultTip);
        void Error(string message);
        void SystemMessage(string text);
        string Localize(string key);
        string Format(string key, params object[] args);
        void RecycleSkillIcons(UnityEngine.Object owner);
        int ActiveSkillSlotsCount { get; }
        int MaxSkillSlotBaseLevel(int subRank);
        ITeamPanelSkillIcon GetSkillIcon(object item, Transform parent, UnityEngine.Object owner);
    }
}

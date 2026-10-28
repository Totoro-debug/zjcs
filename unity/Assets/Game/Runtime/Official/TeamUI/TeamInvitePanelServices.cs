using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    // Original manager/configuration boundaries shared by the five invite modes.
    // The host supplies existing local rules; no quota is inferred here.
    public interface ITeamInvitePanelServices : ITeamInviteUiServices
    {
        long LegionId { get; }
        long? TeamLeaderId { get; }
        int MainPlayerServerId { get; }
        bool NpcFightAssistLimited { get; }
        bool MapRootAvailable { get; }
        int? MapClassId { get; }
        event Action<long, long> LegionUpdated;
        event Action<long> CompanionBound;
        event Action CompanionDataSynced;
        ITeamInvitePlayer CreateFriendsProvider();
        ITeamInvitePlayer CreateNpcProvider();
        ITeamInvitePlayer CreateRecentBuddiesProvider();
        ITeamInvitePlayer CreateLegionProvider();
        TeamInviteRobotRule GetRobotInvite(int playType);
        int GetAllFriendAssistCount(int playType);
        int GetActorAssistCount(IProfessionInfoTarget target, int playType);
        bool CheckPlayTypeCanAssist(IProfessionInfoTarget target, int playType);
        void ShowError(int error, bool log);
        void SetUiGray(GameObject target, bool gray);
        void CloseInviteWindow(Component owner);
    }

    // Fields read from the original player_robot_invite table, without defaults.
    public sealed class TeamInviteRobotRule
    {
        public int PlayType, OnePlayerMaxInviteCount, MaxInviteCount, CdType;
        public int MaxPlayer, RewardCoin;
        public bool NeedTeam;
    }

    public interface ITeamHonorTitleServices
    {
        IProfessionImageResourceLoader Resources { get; }
        GameObject InstantiateKeepScale(GameObject prefab, Transform parent);
        bool TryGetPlayableGuid(int itemId, out string guid);
        Component Play(GameObject playable, GameObject owner, GameObject binding);
        void Stop(Component playable);
    }

    // Framework.CultureTextArg evaluates localization when formatted.
    public sealed class TeamInviteCultureTextArg
    {
        private readonly Func<string, string> localize;
        private readonly bool needTranslation;
        public string KeyOrText { get; }
        public object[] Args { get; }
        public TeamInviteCultureTextArg(Func<string, string> localize, string key, params object[] args)
            : this(localize, key, args, true) { }
        public TeamInviteCultureTextArg(Func<string, string> localize, string key, object[] args, bool translate)
        { this.localize = localize; KeyOrText = key; Args = args; needTranslation = translate; }
        public override string ToString()
        {
            if (!needTranslation) return KeyOrText;
            if (Args != null && Args.Length != 0) return string.Format(localize(KeyOrText), Args);
            return localize(KeyOrText);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // Original TeamInvitePlayerItem, including both awaits of DoInviteReq.MoveNext.
    public sealed class TeamInvitePlayerItem : TeamInviteItemBase
    {
        public enum TeamInviteState { Invited, Joined, CanInvite, FriendshipLimit, Offline, ProfessionLimit }
        private enum InviteFlag { CanInvite, CannotInvite }
        [SerializeField] private Button mInviteBtn;
        [SerializeField] private WorldInviterBase mInviter;
        [SerializeField] private WorldMultiLanguageText mNpcFriendshipText;
        [SerializeField] private NpcFriendshipLevelShow mNpcFriendshipLevelShow;
        [SerializeField] private WorldStateSwitcher<TeamInviteState> mStateSwitcher;
        [SerializeField] private WorldStateSwitcher<InviteFlag> mInviteFlagSwitcher;
        [SerializeField] private GameObject mOnlineStatusRoot;
        [SerializeField] private WorldStateSwitcher<bool> mOnlineStatusRootSwitcher;
        private CancellationTokenSource mTokenSource;
        public Func<TeamTargetData> TeamTargetFunc { get; private set; }

        public override void Configure(ITeamInviteUiServices value)
        {
            base.Configure(value);
            if (mInviter is WorldTeamInviter team) team.Initialize(value.InviteServices);
            if (mInviter is WorldRoomInviter room) room.Initialize(value.ContextInviteServices);
            if (mInviter is WorldChallengeInviter challenge) challenge.Initialize(value.ContextInviteServices);
            if (mInviter is WorldCompanionInviter companion) companion.Initialize(value.ContextInviteServices);
            mNpcFriendshipLevelShow?.Configure(value);
            foreach (var hp in GetComponentsInChildren<TeamPlayerUIBase>(true)) hp.Configure(value.Hp);
            foreach (var head in GetComponentsInChildren<TeamHeadCharacterPortrait>(true)) head.Bind(value.Portraits);
        }
        private void Awake() { if (mInviteBtn != null) mInviteBtn.onClick.AddListener(OnInviteBtnClick); }
        protected override void OnEnable()
        {
            base.OnEnable();
            services.TeamPlayerAdded += OnTeamPlayerAdd;
            services.TeamPlayerRemoved += OnTeamPlayerRemove;
        }
        protected override void OnDisable()
        {
            if (services.MainPlayerAvailable)
            {
                services.TeamPlayerAdded -= OnTeamPlayerAdd;
                services.TeamPlayerRemoved -= OnTeamPlayerRemove;
            }
            StopInviteTask();
            base.OnDisable();
        }
        public void Init(TeamInvitePlayerInfoData playerInfo, TeamInviteSubPanelBase.TeamInviteSortType sort,
            Func<TeamTargetData> teamTarget)
        {
            base.Init(playerInfo, sort);
            TeamTargetFunc = teamTarget;
            RefreshState();
            if (mNpcFriendshipLevelShow != null)
            {
                mNpcFriendshipLevelShow.gameObject.SetActive(mTargetData.IsNpc);
                if (mTargetData.IsNpc) ShowNpcFriendshipContent();
            }
        }
        public void RefreshState(TeamInviteState? inviteState = null)
        {
            TeamInviteState state = inviteState ?? GetCurrentInviteState();
            mStateSwitcher.SetState(state);
            mInviteFlagSwitcher.SetState(GetInviteFlag(state));
            RefreshOnlineStatus();
        }
        private void RefreshOnlineStatus()
        {
            bool show = mPlayerInfo != null && mPlayerInfo.ActorType == TeamInviteActorType.RealPlayer
                && CanInviteOfflineTarget();
            if (mOnlineStatusRoot != null) mOnlineStatusRoot.SetActive(show);
            if (show) mOnlineStatusRootSwitcher.SetState(mPlayerInfo.Online);
        }
        private TeamInviteState GetCurrentInviteState()
        {
            if (mPlayerInfo.ActorType == TeamInviteActorType.RealPlayer && !mPlayerInfo.Online && !CanInviteOfflineTarget())
                return TeamInviteState.Offline;
            TeamTargetData target = GetTeamTarget();
            if (target.TargetCatalog == TeamTargetCatalog.TeamChallenge) return TeamInviteState.CanInvite;
            if (target.TargetCatalog == TeamTargetCatalog.JointStrike && !services.IsJointStrikeInvitedAvailable(mPlayerInfo))
                return TeamInviteState.ProfessionLimit;
            if (services.TeamHasPlayer(mPlayerInfo.PlayerId)) return TeamInviteState.Joined;
            if (services.RoomHasPlayer(mPlayerInfo.PlayerId)) return TeamInviteState.Joined;
            if (mTargetData is IProfessionInfoNpcTarget npc)
            {
                if (services.IsNpcAssistLimit(npc.NpcClassId)) return TeamInviteState.FriendshipLimit;
                if (services.TeamHasNpc(npc.NpcClassId)) return TeamInviteState.Joined;
                if (services.MapHasNpcFollower(npc.NpcClassId)) return TeamInviteState.Joined;
            }
            return TeamInviteState.CanInvite;
        }
        private TeamTargetData GetTeamTarget() => TeamTargetFunc?.Invoke() ?? services.NoneTarget;
        private async Task DoInviteReq(CancellationToken token)
        {
            bool allowed = await mInviter.CheckCanInvite(services.PlayerPlayStateModule);
            if (!token.IsCancellationRequested)
            {
                if (allowed)
                {
                    TeamTargetData target = GetTeamTarget();
                    var npc = mTargetData as IProfessionInfoNpcTarget;
                    var npcIdentity = mTargetData as TeamInviteNpcTarget;
                    var actor = new WorldTeamInviteActor
                    {
                        TargetId = mTargetData.TargetId, ServerId = unchecked((int)mTargetData.ServerId),
                        IsNpc = mTargetData.IsNpc, NpcClassId = npc?.NpcClassId ?? 0,
                        OwnerPlayerId = npcIdentity?.OwnerPlayerId ?? 0
                    };
                    int result = await mInviter.Invite(actor, target);
                    if (!token.IsCancellationRequested)
                    {
                        if (result == 0)
                        {
                            if (mTargetData.TargetType == ActorTargetType.Player) RefreshState(TeamInviteState.Invited);
                            else mPlayerInfo.TeamStatus = TeamInvitePlayerTeamStatus.Member;
                        }
                        StopInviteTask();
                    }
                }
                else StopInviteTask();
            }
        }
        private void OnInviteBtnClick()
        {
            if (mTargetData == null) services.Error("OnInviteBtnClick: mTargetData is null");
            else if (mTokenSource == null)
            {
                mTokenSource = new CancellationTokenSource();
                ForgetSuppressCancellation(DoInviteReq(mTokenSource.Token));
            }
            else services.SystemMessage(services.Localize("TeamInvitePanelWaitInviteResult"));
        }
        private static async void ForgetSuppressCancellation(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private void ShowNpcFriendshipContent()
        {
            if (!(mTargetData is IProfessionInfoNpcTarget npc))
            { services.Error("TeamInvitePlayerItem ShowNpcFriendshipContent Failed, mTargetData is not NpcTargetData"); return; }
            TeamInviteNpcRecord data = services.GetNpc(npc.NpcClassId);
            if (data != null) mNpcFriendshipLevelShow.Init(data.NpcClassId, data.FriendShip, data.FriendShipLevel, 0L);
            else mNpcFriendshipLevelShow.Init(npc.NpcClassId, 0L, 1, 0L);
            if (services.IsNpcAssistLimit(npc.NpcClassId))
                mNpcFriendshipText.Format(data.FriendShipLevel, services.RequiredNpcAssistFriendshipLevel(npc.NpcClassId));
        }
        public Button GetIniviteButton() => mInviteBtn;
        private void OnTeamPlayerAdd(TeamPanelPlayer player)
        { if (player.PlayerId == PlayerInfo.PlayerId) RefreshState(); }
        private void OnTeamPlayerRemove(TeamPanelPlayer player)
        { if (player.PlayerId == PlayerInfo.PlayerId) RefreshState(); }
        private void StopInviteTask()
        {
            if (mTokenSource == null) return;
            mTokenSource.Cancel(); mTokenSource.Dispose(); mTokenSource = null;
        }
        private InviteFlag GetInviteFlag(TeamInviteState state)
        {
            switch (state)
            {
                case TeamInviteState.CanInvite: return InviteFlag.CanInvite;
                case TeamInviteState.Offline:
                    if (CanInviteOfflineTarget()) return InviteFlag.CanInvite;
                    break;
            }
            return InviteFlag.CannotInvite;
        }
        private bool CanInviteOfflineTarget()
        {
            TeamTargetData target = GetTeamTarget();
            if (!target.CanInviteOffline()) return false;
            if (target.TargetCatalog == TeamTargetCatalog.TeamChallenge) return services.TeamChallengeHasSignin;
            return true;
        }
    }
}

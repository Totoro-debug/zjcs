using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // Original TeamPanel. Global managers and RPC completion are local host boundaries.
    public sealed class TeamPanel : UIBase
    {
        [SerializeField] private Button mTargetBtn;
        [SerializeField] private WorldMultiLanguageText mTargetText, mTeamMemberCountText;
        [SerializeField] private TeamPanelTarget mTeamPanelTarget;
        [SerializeField] private Button mAssembleBtn, mNeighborhoodBtn, mInviteBtn, mDismissBtn;
        [SerializeField] private WorldStateSwitcher<bool> mTargetChangeableState;
        [SerializeField] private Transform mMemberItemContent;
        [SerializeField] private TeamPanelItem mMemberItemTemp;
        [SerializeField] private WorldAllowMemberInviteToggle mAllowMemberInviteToggle;
        private TeamPanelTeam mTeam;
        private bool mTargetChangeable;
        private readonly List<TeamPanelItem> mMemberItemList = new List<TeamPanelItem>();
        private GameObjectPool<TeamPanelItem> mMemberItemPool;
        private readonly Dictionary<long, TeamPanelBriefInfo> mPlayerBriefInfoDict =
            new Dictionary<long, TeamPanelBriefInfo>();
        private TeamPanelInviteType mInvitePageType;
        private ITeamPanelServices services;

        public void Configure(ITeamPanelServices value)
        {
            services = value;
            mTeamPanelTarget.Configure(value.Targets);
            mAllowMemberInviteToggle.Initialize(value.MemberInvite);
            mMemberItemTemp.Configure(value);
        }
        private TeamTargetData TeamTarget => mTeam?.Target ?? services.RecordedTarget ?? services.NoneTarget;
        private bool IsTeamLeader => mTeam == null || services.MainPlayerId == mTeam.TeamLeaderId;

        private void Awake()
        {
            mTargetBtn.onClick.AddListener(OnTargetBtnClicked);
            mAssembleBtn.onClick.AddListener(OnAssembleBtnClick);
            mNeighborhoodBtn.onClick.AddListener(OnNeighborhoodClick);
            mInviteBtn.onClick.AddListener(OnInviteBtnClick);
            mDismissBtn.onClick.AddListener(OnDismissBtnClick);
            mMemberItemPool = new GameObjectPool<TeamPanelItem>(transform, mMemberItemContent, mMemberItemTemp);
        }
        private void OnTargetBtnClicked()
        {
            if (mTeamPanelTarget.gameObject.activeSelf) mTeamPanelTarget.gameObject.SetActive(false);
            else
            {
                if (!mTargetChangeable) return;
                if (services.MainTeam != null && services.MainTeam.TeamLeaderId != services.MainPlayerId)
                { services.ShowError(6133, false); return; }
                services.QueryTargets((targets, error) =>
                {
                    if (error != 0) services.ShowError(error, true);
                    else if (targets.Count <= 0) services.SystemMessage(services.Localize("TeamPanel.TargetsEmpty"));
                    else
                    {
                        mTeamPanelTarget.Init(TeamTarget, targets, OnTeamTargetChanged);
                        mTeamPanelTarget.gameObject.SetActive(true);
                    }
                });
            }
        }
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            mTeamPanelTarget.gameObject.SetActive(false);
            services.TeamUpdated += HandleTeamUpdated;
            services.TeamPlayerAdded += MyTeam_OnPlayerAdd;
            services.TeamPlayerRemoved += MyTeam_OnPlayerRemove;
            services.TeamLeaderUpdated += MyTeam_OnTeamLeaderUpdate;
            services.TeamTargetUpdated += MyTeam_OnTeamTargetUpdate;
            Refresh();
        }
        protected override void OnDisableAndInited()
        {
            if (services.ManagerAvailable)
            {
                services.TeamUpdated -= HandleTeamUpdated;
                services.TeamPlayerAdded -= MyTeam_OnPlayerAdd;
                services.TeamPlayerRemoved -= MyTeam_OnPlayerRemove;
                services.TeamLeaderUpdated -= MyTeam_OnTeamLeaderUpdate;
                services.TeamTargetUpdated -= MyTeam_OnTeamTargetUpdate;
            }
            base.OnDisableAndInited();
        }
        protected override void OnDisable() { mTeam = null; base.OnDisable(); }
        private void HandleTeamUpdated() { mTeam = services.MainTeam; Refresh(); }
        private void AppendBriefInfo(TeamPanelBriefInfo[] infos)
        { if (infos != null) foreach (TeamPanelBriefInfo info in infos) AppendBriefInfo(info); }
        private void AppendBriefInfo(TeamPanelBriefInfo info)
        {
            if (info == null) services.Error("TeamPanel 收到空的 BriefInfo");
            else mPlayerBriefInfoDict[info.Id] = info;
        }
        private TeamPanelBriefInfo GetBriefInfo(long playerId)
            => mPlayerBriefInfoDict.TryGetValue(playerId, out TeamPanelBriefInfo value) ? value : null;
        public void Init(TeamPanelTeam team, TeamPanelInviteType pageType, TeamTargetData teamTarget,
            TeamPanelBriefInfo[] playerBriefInfos, bool targetChangeable = false)
        {
            InitWrapper(() =>
            {
                mPlayerBriefInfoDict.Clear();
                mTeam = team;
                mInvitePageType = pageType;
                RecordDefaultTarget(teamTarget);
                mTargetChangeable = targetChangeable;
                AppendBriefInfo(playerBriefInfos);
                AppendBriefInfo(services.MainPlayerBriefInfo);
            });
        }
        private void Refresh() { RefreshMember(); }
        private void RefreshMember()
        {
            int count = mTeam?.Players.Count ?? 1;
            mMemberItemPool.RecycleMany(mMemberItemList, Math.Max(count, TeamTarget.GetTeamMaxPlayer()));
            for (int i = 0; i < mMemberItemList.Count; i++)
            {
                mMemberItemList[i].Configure(services);
                InitMemberItem(mMemberItemList[i], i);
            }
            RefreshButtons();
            RefreshMatchTeamTargets();
            mTeamMemberCountText.SetKey("ui_team_member_count", count, TeamTarget.GetTeamMaxPlayer());
        }
        private void RefreshButtons()
        {
            bool isTeamLeader = IsTeamLeader;
            mAssembleBtn.gameObject.SetActive(CanAssembleTeam());
            mInviteBtn.gameObject.SetActive(CanInviteOther());
            mNeighborhoodBtn.gameObject.SetActive(CanInviteOther());
            mDismissBtn.gameObject.SetActive((mTeam != null) & isTeamLeader);
            mAllowMemberInviteToggle.RefreshAllow(TeamTarget.GetTeamMaxPlayer());
        }
        private bool CanInviteOther() => !IsTeamFullInTarget();
        private bool CanAssembleTeam()
        {
            if (services.IsOnlyOnPrivateMap) return false;
            return !IsTeamFullInTarget();
        }
        private bool IsTeamFullInTarget()
        {
            int max = TeamTarget.GetTeamMaxPlayer();
            return mTeam == null ? max <= 1 : mTeam.Players.Count >= max;
        }
        private void RefreshMatchTeamTargets()
        {
            mTargetText.SetKey(TeamTarget.GetTargetName());
            mTargetChangeableState.SetState(mTargetChangeable);
        }
        private void MyTeam_OnTeamTargetUpdate() { RefreshMember(); }
        private void MyTeam_OnTeamLeaderUpdate(long newLeaderId, long oldLeaderId) { RefreshMember(); }
        private void MyTeam_OnPlayerRemove(TeamPanelPlayer player)
        {
            if (player.PlayerId == services.MainPlayerId) services.Close(gameObject);
            else Refresh();
        }
        private async void MyTeam_OnPlayerAdd(TeamPanelPlayer player)
        {
            TeamPanelBriefInfo result = await services.GetPlayerBriefInfo(player.TargetData, false);
            if (result != null) { AppendBriefInfo(result); RefreshMember(); }
            else services.Error("TeamPanel 没有找到 " + player.PlayerId + " 的 BriefInfo");
        }
        private void RecordDefaultTarget(TeamTargetData target)
        { services.RecordedTarget = target ?? services.NoneTarget; }
        private void RecordDefaultTargetAndBroadcast(TeamTargetData target)
        {
            RecordDefaultTarget(target);
            services.SystemMessage(services.Format("TeamPanel.TargetUpdateTip", services.Localize(target.GetTargetName())));
        }
        private void OnAssembleBtnClick()
        {
            if (!services.AllowAcceptJoinTeamWhenGame) { services.ShowError(1169, false); return; }
            if (!services.CheckAllowInvite()) { services.ShowError(6168, false); return; }
            var professions = new List<int>();
            if (mTeam != null)
                for (int i = 0; i < mTeam.Players.Count; i++) professions.Add(mTeam.Players[i].Profession);
            else professions.Add(services.MainPlayerProfession);
            services.OpenAssemble(TeamTarget, professions);
        }
        private void OnNeighborhoodClick()
        {
            if (!services.AllowTeamOperationByGame) { services.ShowError(1169, false); return; }
            if (!services.CheckAllowInvite()) { services.ShowError(6168, false); return; }
            services.QueryTargets((targets, error) => { if (error == 0) services.OpenNearby(TeamTarget, targets); });
        }
        private void OnInviteBtnClick()
        {
            if (!services.AllowAcceptJoinTeamWhenGame) { services.ShowError(1169, false); return; }
            if (!services.CheckAllowInvite()) { services.ShowError(6168, false); return; }
            services.OpenInvite(TeamPanelInviteType.Player, TeamTarget, null);
        }
        private void OnTeamTargetChanged(TeamTargetData target)
        {
            if (!TeamTarget.ToTeamTargetKey().Equals(target.ToTeamTargetKey()))
            {
                services.UpdateTeamTarget(target, RecordDefaultTargetAndBroadcast);
                if (services.MainTeam == null) RefreshMember();
            }
        }
        private async void OnDismissBtnClick()
        {
            if (!services.MainPlayerAvailable)
            { services.Error("尝试离开队伍！但是 Player 为空"); services.ShowError(1100, false); return; }
            if (services.MainTeam == null) { services.ShowError(1162, false); return; }
            if (services.MainTeam.TeamLeaderId != services.MainPlayerId)
            { services.ShowError(6105, false); return; }
            Action execFunc = services.OperPlayerTeamDismiss;
            Action confirmFunc = () => services.Confirm(this, "TeamDismiss.ConfirmTip", execFunc);
            await PlayerPlayStateClientUtils.DoTeamActionWithConfirm(services.PlayerPlayStateModule,
                execFunc, confirmFunc, type => new PlayerPlayText($"PlayState.{type}ConfirmTeamDismiss"));
        }
        private void InitMemberItem(TeamPanelItem item, int idx)
        {
            if (mTeam == null)
            {
                if (idx == 0) item.Init(GetBriefInfo(services.MainPlayerId), true, HandleAddMember);
                else item.Init(null, false, HandleAddMember);
            }
            else if (idx < mTeam.Players.Count)
            {
                TeamPanelPlayer player = mTeam.Players[idx];
                item.Init(GetBriefInfo(player.TargetData.TargetId), mTeam.TeamLeaderId == player.PlayerId, HandleAddMember);
            }
            else item.Init(null, false, HandleAddMember);
        }
        private void HandleAddMember() { services.OpenInvite(mInvitePageType, TeamTarget); }
    }
}

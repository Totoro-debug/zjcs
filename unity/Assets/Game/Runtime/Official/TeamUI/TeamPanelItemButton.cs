using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // Original TeamPanelItemButton. Preserve guards and the direct leave operation.
    public sealed class TeamPanelItemButton : UIBase
    {
        private enum Status { None, Dismiss, Kickout, Leave }
        [SerializeField] private Button mBtnDismiss, mBtnKickout, mBtnLeave;
        [SerializeField] private WorldStateSwitcher<Status> mStateSwitcher;
        private IProfessionInfoTarget mActorTargetData;
        private ITeamPanelServices services;
        public void Configure(ITeamPanelServices value) { services = value; }

        private void Awake()
        {
            mBtnDismiss.onClick.AddListener(OnDismissBtnClicked);
            mBtnKickout.onClick.AddListener(OnKickoutBtnClicked);
            mBtnLeave.onClick.AddListener(OnLeaveBtnClicked);
        }

        private int MainPlayerIsLeader()
        {
            if (!services.MainPlayerAvailable)
            {
                services.Error("尝试离开队伍！但是 Player 为空");
                return 1100;
            }
            if (services.MainTeam == null) return 1162;
            if (services.MainTeam.TeamLeaderId != services.MainPlayerId) return 6105;
            return 0;
        }

        private async void OnDismissBtnClicked()
        {
            int error = MainPlayerIsLeader();
            if (error != 0) { services.ShowError(error, false); return; }
            Action execFunc = services.OperPlayerTeamDismiss;
            Action confirmFunc = () => services.Confirm(this, "TeamDismiss.ConfirmTip", execFunc);
            await PlayerPlayStateClientUtils.DoTeamActionWithConfirm(services.PlayerPlayStateModule,
                execFunc, confirmFunc, type => new PlayerPlayText($"PlayState.{type}ConfirmTeamDismiss"));
        }

        private async void OnKickoutBtnClicked()
        {
            int error = MainPlayerIsLeader();
            if (error != 0) { services.ShowError(error, false); return; }
            if (mActorTargetData == null) { services.ShowError(1100, false); return; }
            await PlayerPlayStateClientUtils.DoTeamAction(services.PlayerPlayStateModule,
                () => services.OperPlayerTeamKick(mActorTargetData.TargetId));
        }

        private void OnLeaveBtnClicked()
        {
            if (!services.MainPlayerAvailable) { services.ShowError(1100, false); return; }
            if (services.MainTeam == null) { services.ShowError(1162, false); return; }
            long id = services.MainPlayerId;
            if (id == services.MainTeam.TeamLeaderId) services.ShowError(6141, false);
            else if (services.MainTeam.GetPlayer(id) == null) services.ShowError(6125, false);
            else services.OperPlayerTeamLeave();
        }

        public void Init(IProfessionInfoTarget actor)
        { InitWrapper(() => { mActorTargetData = actor; Refresh(); }); }
        private void Refresh() { mStateSwitcher.SetState(GetStatus()); }
        private Status GetStatus()
        {
            if (services.MainTeam == null || !services.MainPlayerAvailable || mActorTargetData == null)
                return Status.None;
            long id = services.MainPlayerId;
            long targetId = mActorTargetData.TargetId;
            if (id == services.MainTeam.TeamLeaderId)
                return id == targetId ? Status.None : Status.Kickout;
            return id == targetId ? Status.Leave : Status.None;
        }
    }
}

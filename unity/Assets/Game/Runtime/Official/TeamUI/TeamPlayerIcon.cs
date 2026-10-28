using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // Original PlayerIcon; UIBase and the self portrait are shared official ports.
    public sealed class TeamPlayerIcon : UIBase
    {
        public PlayerIconMe myIconMe;
        public TeamPlayerIconOther myIconOther;
        [SerializeField] private WorldStateSwitcher<bool> mIsSelfSwitcher;
        private IProfessionInfoTarget mTargetData;
        private ITeamIconServices services;

        public void Configure(ITeamIconServices value)
        {
            services = value;
            if (myIconMe != null)
            {
                myIconMe.ConfigureInfo(value.PlayerInfo);
                if (myIconMe.InfoUI != null) myIconMe.InfoUI.ConfigureInfo(value.PlayerInfo);
            }
            if (myIconOther != null) myIconOther.Configure(value);
        }

        public bool InitFinished => (myIconMe != null && myIconMe.InitFinished)
            || (myIconOther != null && myIconOther.InitFinished);
        public IProfessionInfoTarget TargetData => mTargetData;
        public bool IsSelfPlayer => mTargetData.TargetType == ActorTargetType.Player
            && mTargetData.TargetId == services.MainPlayerId;

        private void Reset()
        { myIconMe = GetComponent<PlayerIconMe>(); myIconOther = GetComponent<TeamPlayerIconOther>(); }

        public void Init(IProfessionInfoTarget data, bool keepUpdate = false, bool forceShowStatus = false,
            object playerLiteInfo = null, bool isNameWithPlanesName = false)
        {
            InitWrapper(() =>
            {
                mTargetData = data;
                bool self = IsSelfPlayer;
                if (myIconMe != null) myIconMe.enabled = self;
                if (myIconOther != null) myIconOther.enabled = !self;
                if (self) myIconMe.Init(playerLiteInfo, mTargetData, isNameWithPlanesName);
                else myIconOther.Init(mTargetData, keepUpdate, forceShowStatus, playerLiteInfo, isNameWithPlanesName);
                mIsSelfSwitcher.SetState(self);
            });
        }

        public override void ResetInitState()
        {
            base.ResetInitState();
            if (myIconMe != null) myIconMe.ResetInitState();
            if (myIconOther != null) myIconOther.ResetInitState();
        }
        public PlayerInfoData GetPlayerInfoData() => GetPlayerInfoUI().PlayerInfoData;
        public PlayerInfoUI GetPlayerInfoUI() => IsSelfPlayer ? myIconMe.InfoUI : myIconOther.InfoUI;
        public void SetShowCollectionImg(bool show)
        {
            if (myIconMe != null && myIconMe.InfoUI != null) myIconMe.InfoUI.SetShowCollectionImg(show);
            if (myIconOther != null && myIconOther.InfoUI != null) myIconOther.InfoUI.SetShowCollectionImg(show);
        }
        public void SetNameTextActive(bool active)
        { if (IsSelfPlayer) myIconMe.SetNameTextActive(active); else myIconOther.SetNameTextActive(active); }
        public void SetStatus(int status)
        { if (IsSelfPlayer) myIconMe.SetStatus(status); else myIconOther.SetStatus(status); }
    }
}

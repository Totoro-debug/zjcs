using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    // Original PlayerIconOther, recovered from its async state machines.
    // All three selected Team prefab status references are null; a real status
    // implementation is still required if another prefab supplies that reference.
    public sealed class TeamPlayerIconOther : UIBase
    {
        public Component myIconStatus;
        public bool myKeepUpdate, myForceShowStatus;
        public PlayerInfoUI InfoUI;
        public bool ShowInfoOnClick = true, IsShowNpcInfo = true;
        [SerializeField] private int mPanelPopupSceneType = 4;
        public PlayerInfoData mInfoData;
        private IProfessionInfoTarget mTargetData;
        private CancellationTokenSource cancellation;
        private bool mDataInited;
        private object mPlayerLiteInfo;
        private Action<object> mOnLiteInfoUpdated;
        private ITeamIconServices services;
        private ITeamIconStatus Status => (ITeamIconStatus)myIconStatus;
        private long PlayerId => mTargetData.TargetType == ActorTargetType.Player ? mTargetData.TargetId : 0L;
        public string Name => mInfoData.Name;
        public bool InitFinished => mDataInited && (myIconStatus == null || !Status.IsVisible || Status.InitFinished);

        public void Configure(ITeamIconServices value)
        { services = value; if (InfoUI != null) InfoUI.ConfigureInfo(value.PlayerInfo); }
        private void Reset() { InfoUI = GetComponent<PlayerInfoUI>(); }

        public void Init(IProfessionInfoTarget data, bool keepUpdate = false, bool forceShowStatus = false,
            object playerLiteInfo = null, bool isNameWithPlanesName = false, Action<object> onLiteInfoUpdated = null)
        {
            InitWrapper(() =>
            {
                mPlayerLiteInfo = playerLiteInfo; mOnLiteInfoUpdated = onLiteInfoUpdated; mTargetData = data;
                myKeepUpdate = keepUpdate; myForceShowStatus = forceShowStatus;
                if (myIconStatus != null) { Status.Init(data); UpdateIconStatus(); }
                InfoUI.IsNameWithPlanesName = isNameWithPlanesName;
            });
        }

        public override void ResetInitState()
        {
            if (InfoUI != null && InfoUI.PlayerHeadPortrait != null)
                ((IProfessionPlayerInfoHead)InfoUI.PlayerHeadPortrait).ResetInitState();
            base.ResetInitState();
        }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            if (mPlayerLiteInfo == null)
            { cancellation = new CancellationTokenSource(); Observe(RequestPlayerLiteInfo(cancellation.Token)); }
            else InitByInfo(mPlayerLiteInfo);
            if (myIconStatus == null) return;
            UpdateIconStatus();
            if (!services.MainPlayerAvailable) Debug.LogError("PlayerIconOther OnEnableAndInited MeManager.MainPlayer == null", this);
            if (mTargetData.TargetType == ActorTargetType.Player)
            { services.RelationAdded += OnRelationChanged; services.RelationRemoved += OnRelationChanged; }
        }

        protected override void OnDisableAndInited()
        {
            mDataInited = false; Clear();
            if (myIconStatus != null && mTargetData.TargetType == ActorTargetType.Player && services.RelationshipAvailable)
            { services.RelationAdded -= OnRelationChanged; services.RelationRemoved -= OnRelationChanged; }
            base.OnDisableAndInited();
        }

        private async Task RequestPlayerLiteInfo(CancellationToken token)
        {
            object info = await services.GetLiteInfo(mTargetData, token);
            InitByInfo(info);
            if (!myKeepUpdate) { Clear(); return; }
            while (!token.IsCancellationRequested)
            {
                await services.WaitForSeconds(9f, token);
                info = await services.GetLiteInfo(mTargetData, token, latest: true);
                InitByInfo(info);
            }
        }

        private void Clear()
        { if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); } cancellation = null; }

        public void InitByInfo(object info)
        {
            if (info == null) { Debug.LogWarning("PlayerIconOther received empty info, player: " + mTargetData.TargetId, this); return; }
            if (services.LiteInfoId(info) != mTargetData.TargetId)
                Debug.LogWarning("PlayerIconOther received mismatched info, player: " + mTargetData.TargetId + ", info: " + services.LiteInfoId(info), this);
            mInfoData = services.PlayerInfo.BuildLiteData(info, mTargetData);
            InfoUI.Init(mInfoData, HandleOnClick); InfoUI.Refresh();
            mDataInited = true; mOnLiteInfoUpdated?.Invoke(info);
        }

        // The original method is intentionally empty.
        public void SetStatus(int status) { }
        public void HandleOnClick()
        { if (InitFinished && ShowInfoOnClick && (!mTargetData.IsNpc || IsShowNpcInfo)) ShowPlayerBaseInfo(); }
        public void SetNameTextActive(bool active) => InfoUI.SetNameTextActive(active);

        private void ShowPlayerBaseInfo()
        {
            if (!mDataInited) Debug.LogError("PlayerIconOther info is not ready: " + mTargetData, this);
            if (mTargetData.TargetType == ActorTargetType.Npc)
                services.PopupNpc(this, mTargetData as IProfessionInfoNpcTarget);
            else Observe(ShowPlayerInfo());
        }
        private async Task ShowPlayerInfo()
        {
            object info = await services.GetPlayerBriefInfo(mTargetData.TargetId);
            if (info != null && gameObject != null && gameObject.activeInHierarchy)
                services.PopupPlayer(this, info, mPanelPopupSceneType, services.PlayerInfo.DuelSkillGroup(-1L, -1L));
        }
        private void OnRelationChanged(long id) { if (PlayerId == id) UpdateIconStatus(); }
        private void UpdateIconStatus()
        {
            if (myForceShowStatus || services.IsFriend(PlayerId)) ((Behaviour)myIconStatus).enabled = true;
            else { ((Behaviour)myIconStatus).enabled = false; Status.UpdateFriendshipStatus(); }
            Status.SetFriendInfo(((Behaviour)myIconStatus).enabled);
        }
        public GameObject GetModelGo() => InfoUI.GetModelGo();
        private async void Observe(Task task)
        { try { await task; } catch (OperationCanceledException) { } catch (Exception error) { Debug.LogException(error, this); } }
    }
}

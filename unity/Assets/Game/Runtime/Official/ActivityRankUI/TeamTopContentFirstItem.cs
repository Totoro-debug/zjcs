using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ActivityRankUI
{
    // Original TeamTopContentFirstItem. Callback release uses a scoped cancellation
    // token; the original lite-info/resource operations use existing actor bindings.
    public sealed class TeamTopContentFirstItem : UIBase
    {
        [SerializeField] private TeamPlayerIcon mPlayerIcon;
        [SerializeField] private Transform mNpcSlot;
        [SerializeField] private WorldStateSwitcher<bool> mEmptyStateSwitcher;
        private IProfessionInfoTarget mTargetData;
        private ActivityRankBindings bindings;
        private CancellationTokenSource mLiteInfoCallback;
        private IDisposable mModelHandle;

        public void Configure(ActivityRankBindings value)
        { bindings = value; mPlayerIcon.Configure(value.Icons); }

        public void Init(IProfessionInfoTarget targetData)
        {
            InitWrapper(() =>
            {
                mTargetData = targetData;
                if (targetData == null) mPlayerIcon.ResetInitState();
                mEmptyStateSwitcher.SetState(targetData == null);
            });
        }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            if (mTargetData != null)
            {
                mLiteInfoCallback = new CancellationTokenSource();
                _ = RefreshLiteInfo(mTargetData, mLiteInfoCallback.Token);
            }
        }

        private async Task RefreshLiteInfo(IProfessionInfoTarget target, CancellationToken cancellation)
        {
            try
            {
                object info = await bindings.Icons.GetLiteInfo(target, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (target.IsNpc)
                {
                    for (int i = mNpcSlot.childCount - 1; i >= 0; i--)
                        Destroy(mNpcSlot.GetChild(i).gameObject);
                    var npc = (ActivityRankNpcTarget)target;
                    PlayerInfoData data = bindings.Icons.PlayerInfo.BuildLiteData(info, target);
                    npc.NpcLevel = data.Level; npc.NpcSubRank = data.SubRank;
                    var definition = bindings.Icons.PlayerInfo.GetNpc(npc.NpcClassId);
                    var handle = bindings.Icons.PlayerInfo.Resources.LoadAsset<GameObject>(definition.FigureModelUI);
                    mModelHandle = handle;
                    GameObject model = bindings.Icons.PlayerInfo.InstantiateKeepScale(handle.Result, mNpcSlot);
                    bindings.Icons.PlayerInfo.OverrideNpcInitialAnimation(model, definition.Id);
                }
                mPlayerIcon.Init(target);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        protected override void OnDisableAndInited()
        {
            mLiteInfoCallback?.Cancel(); mLiteInfoCallback?.Dispose(); mLiteInfoCallback = null;
            mModelHandle?.Dispose(); mModelHandle = null;
            mTargetData = null;
            base.OnDisableAndInited();
        }
    }
}

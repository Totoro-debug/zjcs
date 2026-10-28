// Migrated complete original stage operation; see stage operation receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityWorldStages;
using StageDialog = Zjcs.Official.WorldUI.StageDialog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zjcs.Official.EntityStageOperations
{
public class ECWorldStageInteractionOperationWindow : ECWorldStageInteractionOperationBase, IWorldStageInteractionOperationWindowHandlerBase
{
	[SerializeField]
	private bool mIsReport;

	[SerializeField]
	private SDKEventPanelId mSDKEventPanelId;

	[SerializeField]
	private string mWindowName;

	[SerializeField]
	private List<WorldStageInteractionOperationWindowClickData> mClickDatas;

	protected IServiceProvider mServiceProvider;

	protected IWorldStageWindowMapRoot mMapRootMgr;

	public override bool DoOper(IServiceProvider serviceProvider, ECWorldStageStateData worldStage)
	{
		if (worldStage == null || string.IsNullOrEmpty(mWindowName))
		{
			return false;
		}
		mServiceProvider = serviceProvider;
		mMapRootMgr = (IWorldStageWindowMapRoot)serviceProvider.GetService(typeof(IWorldStageWindowMapRoot));
		UniTaskExtensions.Forget(DoPopupWindow(worldStage));
		return true;
	}

	private async UniTask DoPopupWindow(ECWorldStageStateData worldStage)
    {
        var id = WorldStageOperationServices.State(worldStage).Entity.Id;
        using (var broker = mMapRootMgr.SubscribeHookMapInput(id))
        {
            try
            {
                StagePopupWindowOperation operation;
                if (!mIsReport) operation = await WorldStageOperationServices.RequireWindowHost.Popup(mWindowName);
                else operation = await WorldStageOperationServices.RequireWindowHost.PopupAndReport(mWindowName, mSDKEventPanelId, SDKEventPanelSource.Manual);
                if (mMapRootMgr.Initialized && operation != null)
                {
                    if (operation.GameObject.TryGetComponent<StageDialog>(out var dialog))
                        dialog.Init(WorldStageOperationServices.RequireWindowHost.GetUiContext(worldStage));
                    DoWindowInit(operation.GameObject, worldStage);
                }
            }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
    }

	public virtual void OnClick(ECWorldStageStateData worldStage, WorldStageInteractionOperationWindowClickType type)
	{
		if (mClickDatas.Count <= 0)
		{
			return;
		}
		foreach (WorldStageInteractionOperationWindowClickData mClickData in mClickDatas)
		{
			if (mClickData.Type == type)
			{
				mClickData.OperationData.InteractionOperation?.DoOper(mServiceProvider, worldStage);
				return;
			}
		}
		UnityEngine.Debug.LogError("Official playable diagnostic");
	}

	public override bool ExistOperation<T>()
	{
		if (OperationIsThis<T>())
		{
			return true;
		}
		foreach (WorldStageInteractionOperationWindowClickData mClickData in mClickDatas)
		{
			if (mClickData.OperationData.InteractionOperation != null)
			{
				bool flag = mClickData.OperationData.InteractionOperation.ExistOperation<T>();
				if (flag)
				{
					return flag;
				}
			}
		}
		return false;
	}

	protected virtual void DoWindowInit(GameObject panel, ECWorldStageStateData worldStage)
	{
		panel.GetComponent<IWorldStageUiBase>()?.InitWindowClick(worldStage, this);
	}

	public override void Release()
	{
		if (mClickDatas != null)
		{
			foreach (WorldStageInteractionOperationWindowClickData mClickData in mClickDatas)
			{
				mClickData.OperationData.InteractionOperation?.Release();
			}
		}
		base.Release();
	}
}

}

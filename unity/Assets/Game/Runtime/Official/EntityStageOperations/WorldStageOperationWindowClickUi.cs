// Migrated complete original stage operation; see stage operation receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityWorldStages;
using StageDialog = Zjcs.Official.WorldUI.StageDialog;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityStageOperations
{
public class WorldStageOperationWindowClickUi : MonoBehaviour, IWorldStageUiBase
{
	[SerializeField]
	private List<WorldStageUiClickData> mDatas;

	private ECWorldStageStateData mWorldStage;

	private IWorldStageInteractionOperationWindowHandlerBase mWindowHandler;

	private void Awake()
	{
		foreach (WorldStageUiClickData mData in mDatas)
		{
			mData.Init(this);
		}
	}

	private void OnDestroy()
	{
		foreach (WorldStageUiClickData mData in mDatas)
		{
			mData.Release();
		}
	}

	public void InitWindowClick(ECWorldStageStateData worldStage, IWorldStageInteractionOperationWindowHandlerBase windowsHandler)
	{
		mWorldStage = worldStage;
		mWindowHandler = windowsHandler;
	}

	public void OnClick(WorldStageInteractionOperationWindowClickType type)
	{
		if (mWindowHandler != null)
		{
			mWindowHandler.OnClick(mWorldStage, type);
		}
	}
}

}

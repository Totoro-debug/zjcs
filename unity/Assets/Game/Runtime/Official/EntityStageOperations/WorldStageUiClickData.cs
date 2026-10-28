// Migrated complete original stage operation; see stage operation receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityWorldStages;
using StageDialog = Zjcs.Official.WorldUI.StageDialog;
using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.EntityStageOperations
{
[Serializable]
public struct WorldStageUiClickData
{
	public WorldStageInteractionOperationWindowClickType ClickType;

	public Button Button;

	private IWorldStageUiBase mWorldStageUi;

	public void Init(IWorldStageUiBase worldStageUi)
	{
		mWorldStageUi = worldStageUi;
		Button.onClick.AddListener(OnClick);
	}

	public void Release()
	{
		Button.onClick.RemoveListener(OnClick);
	}

	private void OnClick()
	{
		mWorldStageUi.OnClick(ClickType);
	}
}

}

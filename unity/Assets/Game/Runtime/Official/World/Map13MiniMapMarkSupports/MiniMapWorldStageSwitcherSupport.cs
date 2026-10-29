using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class MiniMapWorldStageSwitcherSupport : MiniMapMarkSupportBase, IEcEntityWorldStageComponentStateUpdateEventReceiver, IEventReceiver
{
	[SerializeField]
	private StateSwitcher<int> mStateSwitcher;

	private MiniMapMarkUIBase mMarkUI;

	public override void Init(MiniMapMarkUIBase markUi)
	{
		mMarkUI = markUi;
		AddStageListen(markUi.Mark);
		Refresh();
	}

	public override void Release(MiniMapMarkUIBase markUi)
	{
		RemoveStageListen(markUi.Mark);
	}

	private void AddStageListen(MiniMapMarkBase mark)
	{
		if (mark is MiniMapEntityMark { StageComponent: var stageComponent })
		{
			((IMiniMapManagedStage)stageComponent).StateUpdateEvent += (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
		}
	}

	private void RemoveStageListen(MiniMapMarkBase mark)
	{
		if (mark is MiniMapEntityMark { StageComponent: var stageComponent })
		{
			((IMiniMapManagedStage)stageComponent).StateUpdateEvent -= (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
		}
	}

	void IEcEntityWorldStageComponentStateUpdateEventReceiver.WorldStageComponent_OnStateUpdate(IMiniMapManagedStage comp, int preState, object param)
	{
		Refresh();
	}

	private void Refresh()
	{
		mStateSwitcher.SetState(mMarkUI.Mark.GetEntityState());
	}
}

}

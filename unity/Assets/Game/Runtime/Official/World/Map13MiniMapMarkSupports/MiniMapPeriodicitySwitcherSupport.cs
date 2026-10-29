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
public class MiniMapPeriodicitySwitcherSupport : MiniMapMarkSupportBase, IEcEntityWorldStageComponentStateUpdateEventReceiver, IEventReceiver, IMiniMapWayPointBlockHandler
{
	public enum Status
	{
		HasCount,
		NotCount,
		NotPeriodicity
	}

	private const Status kSuccessStatus = Status.NotCount;

	[Header("激活时为True")]
	[SerializeField]
	private StateSwitcher<Status> mHasPeriodicityCountSwitcher;

	private MiniMapMarkUIBase mMarkUI;
	private IMiniMapMarkSupportContext Context => (IMiniMapMarkSupportContext)mMarkUI.MiniMap;

	private IMiniMapManagedStage mStageComponent;

	public override void Init(MiniMapMarkUIBase markUi)
	{
		mMarkUI = markUi;
		if (mMarkUI.Mark is MiniMapEntityMark miniMapEntityMark)
		{
			mStageComponent = miniMapEntityMark.Entity.GetComponent<IMiniMapManagedStage>();
		}
		AddStageListen(markUi.Mark);
		Refresh();
	}

	public override void Release(MiniMapMarkUIBase markUi)
	{
		RemoveStageListen(markUi.Mark);
	}

	bool IMiniMapWayPointBlockHandler.CanUse()
	{
		return CheckPeriodicityCount() == Status.NotCount;
	}

	private void AddStageListen(MiniMapMarkBase mark)
	{
		Context.SupportPlayer.OnPeriodicityInteractionChanged += MainPlayer_OnPeriodicityInteractionChanged;
		if (mStageComponent != null)
		{
			mStageComponent.StateUpdateEvent += (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
		}
	}

	private void RemoveStageListen(MiniMapMarkBase mark)
	{
		if (Context.SupportPlayer != null)
		{
			Context.SupportPlayer.OnPeriodicityInteractionChanged -= MainPlayer_OnPeriodicityInteractionChanged;
		}
		if (mStageComponent != null)
		{
			mStageComponent.StateUpdateEvent -= (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;
			mStageComponent = null;
		}
	}

	void IEcEntityWorldStageComponentStateUpdateEventReceiver.WorldStageComponent_OnStateUpdate(IMiniMapManagedStage stageComp, int preState, object param)
	{
		Refresh();
	}

	private void MainPlayer_OnPeriodicityInteractionChanged(int groudId, int count)
	{
		Refresh();
	}

	private void Refresh()
	{
		mHasPeriodicityCountSwitcher.SetState(CheckPeriodicityCount());
	}

	private Status CheckPeriodicityCount()
	{
		int periodicityInteractionId;
		long ownerId;
		if (mMarkUI.Mark is MiniMapEntityMark miniMapEntityMark)
		{
			periodicityInteractionId = Context.SupportQueries.GetPeriodicityInteractionId(miniMapEntityMark.Entity);
			ownerId = MiniMapMarkSupportRules.GetEntityOwnerId(miniMapEntityMark.Entity, EntityOwnershipType.Player);
		}
		else
		{
			if (!(mMarkUI.Mark is MiniMapOfflineMark miniMapOfflineMark))
			{
				return Status.NotPeriodicity;
			}
			periodicityInteractionId = Context.SupportQueries.GetOfflinePeriodicityInteractionId(miniMapOfflineMark.MapClassId, miniMapOfflineMark.LoEntityId);
			ownerId = Context.SupportPlayer.Id;
		}
		if (!Context.SupportQueries.TryGetPeriodicity(periodicityInteractionId, out var _))
		{
			return Status.NotPeriodicity;
		}
		if (MiniMapMarkSupportRules.CheckCanPeriodicityInteraction(Context, ownerId, periodicityInteractionId) != 0)
		{
			return Status.NotCount;
		}
		return Status.HasCount;
	}
}

}

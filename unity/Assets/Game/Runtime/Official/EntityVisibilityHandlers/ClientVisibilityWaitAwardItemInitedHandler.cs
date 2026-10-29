// Migrated original application source; see Map14 visibility-handlers receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver=Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Awaiter=Cysharp.Threading.Tasks.UniTask.Awaiter;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StorySourceFade;

namespace Zjcs.Official.EntityVisibilityHandlers
{
public class ClientVisibilityWaitAwardItemInitedHandler : ClientVisibilityBaseHandler, IEcEntityStageResultItemUpdatedEventReceiver, IEventReceiver
{
	private VisibilityAwardItemData mVisibilityAwardItemData;

	public override ClientVisibilityType VisibilityType => ClientVisibilityType.WaitAwardItemInited;

	public override void Init(ECViewEntityData ent, bool isNew)
	{
		base.Init(ent, isNew);
		mVisibilityAwardItemData = Entity.GetComponent<VisibilityAwardItemData>();
		if (mVisibilityAwardItemData == null)
		{
			Visible = true;
			return;
		}
		if (mVisibilityAwardItemData.TryGetAwardItemInfo(0, out var _))
		{
			Visible = true;
			return;
		}
		Visible = false;
		mVisibilityAwardItemData.OnStageResultAwardItemUpdated += (IEcEntityStageResultItemUpdatedEventReceiver)this;
	}

	public void EntityStageResultItemComponent_Updated(VisibilityAwardItemData stageResultAwardItemComponent)
	{
		if (mVisibilityAwardItemData.TryGetAwardItemInfo(0, out var _))
		{
			Visible = true;
			mVisibilityAwardItemData.OnStageResultAwardItemUpdated -= (IEcEntityStageResultItemUpdatedEventReceiver)this;
		}
	}

	public override void Release()
	{
		if (!Visible)
		{
			mVisibilityAwardItemData.OnStageResultAwardItemUpdated -= (IEcEntityStageResultItemUpdatedEventReceiver)this;
		}
		base.Release();
	}
}

}

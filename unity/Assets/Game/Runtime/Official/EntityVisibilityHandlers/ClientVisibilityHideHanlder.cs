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
using UnityEngine;

namespace Zjcs.Official.EntityVisibilityHandlers
{
public class ClientVisibilityHideHanlder : ClientVisibilityBaseHandler
{
	private IEntityFadeAnimationBridge mEntityFadeAnimationBridge;

	public IVisibilityMapRoot MapRootManagerProxy { get; set; }

	public override ClientVisibilityType VisibilityType => ClientVisibilityType.Hide;

	public override void Init(ECViewEntityData entity, bool isNew)
	{
		base.Init(entity, isNew);
		MyEntity entity2 = MapRootManagerProxy.MyContainer.GetEntity(entity.Id);
		mEntityFadeAnimationBridge = ((Component)entity2).GetComponent<IEntityFadeAnimationBridge>();
		if (mEntityFadeAnimationBridge != null)
		{
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.ImmediateFadeOut();
		}
	}

	public override void Release()
	{
		if (mEntityFadeAnimationBridge != null)
		{
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.ImmediateFadeIn();
			mEntityFadeAnimationBridge = null;
		}
	}
}

}

// Original client container implementation; see entity container source receipt.
using System;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;

namespace Zjcs.Official.EntityContainers
{
public sealed class MyContainerAddEntityEventDelegate : EventDelegateBase<MyContainerAddEntityEventDelegate, IMyContainerAddEntityEventReceiver, MyEntity>
{
	protected override void FireOnReceiver(IMyContainerAddEntityEventReceiver receiver, MyEntity entity)
	{
		receiver.MyContainer_OnEntityAdd(entity);
	}

	public override void FireEvent(MyEntity entity)
	{
		base.FireEvent(entity);
	}
}

}

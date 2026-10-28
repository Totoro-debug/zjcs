// Complete original client lifecycle class; see entity lifecycle source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;

namespace Zjcs.Official.EntityLifecycle
{
public sealed class MyEntityRemoveEventDelegate : EventDelegateBase<MyEntityRemoveEventDelegate, IMyEntityRemoveEventReceiver, MyEntity>
{
	protected override void FireOnReceiver(IMyEntityRemoveEventReceiver receiver, MyEntity entity)
	{
		receiver.MyEntity_OnRemove(entity);
	}

	public override void FireEvent(MyEntity entity)
	{
		base.FireEvent(entity);
	}
}

}

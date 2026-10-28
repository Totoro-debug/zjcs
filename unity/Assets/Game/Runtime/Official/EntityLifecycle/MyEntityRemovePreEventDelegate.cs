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
public sealed class MyEntityRemovePreEventDelegate : EventDelegateBase<MyEntityRemovePreEventDelegate, IMyEntityRemovePreEventReceiver, MyEntity>
{
	protected override void FireOnReceiver(IMyEntityRemovePreEventReceiver receiver, MyEntity entity)
	{
		receiver.MyEntity_RemovePre(entity);
	}

	public override void FireEvent(MyEntity entity)
	{
		base.FireEvent(entity);
	}
}

}

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
public sealed class MyEntityStartEventDelegate : EventDelegateBase<MyEntityStartEventDelegate, IMyEntityStartEventReceiver, MyEntity, bool>
{
	protected override void FireOnReceiver(IMyEntityStartEventReceiver receiver, MyEntity entity, bool newEntity)
	{
		receiver.MyEntity_OnStart(entity, newEntity);
	}

	public override void FireEvent(MyEntity entity, bool newEntity)
	{
		base.FireEvent(entity, newEntity);
	}
}

}

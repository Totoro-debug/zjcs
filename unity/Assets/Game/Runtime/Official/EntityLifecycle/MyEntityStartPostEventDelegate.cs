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
public sealed class MyEntityStartPostEventDelegate : EventDelegateBase<MyEntityStartPostEventDelegate, IMyEntityStartPostEventReceiver, MyEntity>
{
	protected override void FireOnReceiver(IMyEntityStartPostEventReceiver receiver, MyEntity entity)
	{
		receiver.MyEntity_OnStartPost(entity);
	}

	public override void FireEvent(MyEntity entity)
	{
		base.FireEvent(entity);
	}
}

}

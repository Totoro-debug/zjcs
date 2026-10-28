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
public sealed class MyEntityReplaceEventDelegate : EventDelegateBase<MyEntityReplaceEventDelegate, IMyEntityReplaceEventReceiver, MyEntity, EcEntityIdentifier>
{
	protected override void FireOnReceiver(IMyEntityReplaceEventReceiver receiver, MyEntity entity, EcEntityIdentifier preEntId)
	{
		receiver.MyEntity_OnReplace(entity, preEntId);
	}

	public override void FireEvent(MyEntity entity, EcEntityIdentifier preEntId)
	{
		base.FireEvent(entity, preEntId);
	}
}

}

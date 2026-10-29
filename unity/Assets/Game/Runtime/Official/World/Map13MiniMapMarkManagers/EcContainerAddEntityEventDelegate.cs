
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public sealed class EcContainerAddEntityEventDelegate : EventDelegateBase<EcContainerAddEntityEventDelegate, IEcContainerAddEntityEventReceiver, IMiniMapSpawnEntity>
{
	protected override void FireOnReceiver(IEcContainerAddEntityEventReceiver receiver, IMiniMapSpawnEntity entity)
	{
		receiver.Container_OnEntityAdd(entity);
	}

	public override void FireEvent(IMiniMapSpawnEntity entity)
	{
		base.FireEvent(entity);
	}
}

}

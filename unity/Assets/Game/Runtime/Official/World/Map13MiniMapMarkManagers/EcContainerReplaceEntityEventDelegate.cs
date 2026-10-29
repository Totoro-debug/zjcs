
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public sealed class EcContainerReplaceEntityEventDelegate : EventDelegateBase<EcContainerReplaceEntityEventDelegate, IEcContainerReplaceEntityEventReceiver, IMiniMapSpawnEntity, EcEntityIdentifier>
{
	protected override void FireOnReceiver(IEcContainerReplaceEntityEventReceiver receiver, IMiniMapSpawnEntity entity, EcEntityIdentifier preEntId)
	{
		receiver.Container_OnEntityReplace(entity, preEntId);
	}

	public override void FireEvent(IMiniMapSpawnEntity entity, EcEntityIdentifier preEntId)
	{
		base.FireEvent(entity, preEntId);
	}
}

}

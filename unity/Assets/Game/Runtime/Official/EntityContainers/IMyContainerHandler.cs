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
public interface IMyContainerHandler
{
	MyContainer MyContainer { get; }

	IClientEntityContainer Container { get; }

	EntityGridGroundData GridGround { get; }

	MyEntity MyRootEntity { get; }

	ECViewEntityData RootEntity { get; }

	void RegisterContainer(MyContainer myContainer, IClientEntityContainer container);

	void UnregisterContainer();
}

}

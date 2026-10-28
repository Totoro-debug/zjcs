// Original client radar implementation; see entity radar source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.EntityContainers;
using MyContainer = Zjcs.Official.EntityContainers.MyContainer;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using IGameObjectPoolItem = Zjcs.Official.StoryPlayables.IGameObjectPoolItem;
namespace Zjcs.Official.EntityRadar
{
public abstract class MapControllerEntityRadarTipProviderBase
{
	public MapControllerEntityRadar MyOwnerRadar { get; protected set; }

	public MapControllerEntityRadarTipProviderBase(IMapControllerEntityRadarSupport radarSupport)
	{
	}

	public abstract void EntityEnter(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support, bool isInstance = false);

	public abstract void EntityLeave(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support);

	public virtual void OnDisable()
	{
	}
}

}

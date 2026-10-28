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

public static class MapControllerEntityRadarSupportUtils
{
	public static IMapControllerEntityRadarSupport GetMapControllerEntityRadarSupport(MyEntity entity)
	{
		IMapControllerEntityRadarSupport mapControllerEntityRadarSupport = null;
		ECViewDriver componentInChildren = ((Component)entity).GetComponentInChildren<ECViewDriver>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			mapControllerEntityRadarSupport = componentInChildren.GetView<ECMapControllerEntityRadarSupportView>();
		}
		if (mapControllerEntityRadarSupport == null)
		{
			mapControllerEntityRadarSupport = ((Component)entity).GetComponent<IMapControllerEntityRadarSupport>();
		}
		return mapControllerEntityRadarSupport;
	}
}

}

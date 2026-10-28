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

public class MapControllerEntityRadarTipProvider : MapControllerEntityRadarTipProviderBase, IRadarRemoveEntityReceiver, IEventReceiver
{
	protected IMapControllerEntityRadarSupport mRadarSupport;

	public MapControllerEntityRadarTipProvider(IMapControllerEntityRadarSupport radarSupport)
		: base(radarSupport)
	{
		mRadarSupport = radarSupport;
	}

	public override void EntityEnter(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support, bool isInstance = false)
	{
		MyOwnerRadar = radar;
		radar.AddTip(support, support.RadarTipPrefab);
		mRadarSupport.RemovalEvents.Add(this);
	}

	public override void EntityLeave(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support)
	{
		RemoveEntityTip(radar, support, immidietaly: false);
	}

	private void RemoveEntityTip(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support, bool immidietaly)
	{
		if (!((Object)(object)radar == (Object)null))
		{
			mRadarSupport.RemovalEvents.Remove(this);
			radar.RemoveTip(support.MyEntity, immidietaly);
			MyOwnerRadar = null;
		}
	}

	public override void OnDisable()
	{
		mRadarSupport.RemovalEvents.Remove(this);
		base.OnDisable();
	}

	void IRadarRemoveEntityReceiver.Container_OnEntityRemove(ECViewEntityData entity, object data)
	{
		if (!(mRadarSupport.MyEntity.Id != entity.Id))
		{
			RemoveEntityTip(MyOwnerRadar, mRadarSupport, immidietaly: true);
		}
	}
}

}

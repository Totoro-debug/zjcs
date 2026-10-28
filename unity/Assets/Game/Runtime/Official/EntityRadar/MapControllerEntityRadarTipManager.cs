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

[DefaultExecutionOrder(-1)]
public class MapControllerEntityRadarTipManager : MonoBehaviour
{
	public static MapControllerEntityRadarTipManager Instance;

	private Dictionary<string, GameObjectPool<MapControllerEntityRadarTipBase>> mTipPoolDict = new Dictionary<string, GameObjectPool<MapControllerEntityRadarTipBase>>();

	private void Awake()
	{
		Instance = this;
	}

	public MapControllerEntityRadarTipBase GetTip(Transform slot, MapControllerEntityRadarTipBase prefab)
	{
		if (!mTipPoolDict.TryGetValue(((Object)prefab).name, out var value))
		{
			GameObjectPool<MapControllerEntityRadarTipBase> gameObjectPool = (mTipPoolDict[((Object)prefab).name] = new GameObjectPool<MapControllerEntityRadarTipBase>(((Component)this).transform, prefab));
			value = gameObjectPool;
		}
		return value.Borrow(slot);
	}

	public void RecycleTip(MapControllerEntityRadarTipBase tip)
	{
		if (mTipPoolDict.TryGetValue(((Object)tip).name, out var value))
		{
			value.Return(tip);
		}
	}

	private void OnDestroy()
	{
		Instance = null;
	}
}

}

// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
public sealed class GamePrefabTypeSetPools<T, V> : IGamePrefabTypeSetPools<T, V> where T : Component, IGameObjectPoolItem where V : GameObjectPoolBase<T>
{
	private Transform mRootNode;

	private readonly Dictionary<int, V> mPoolsDict = new Dictionary<int, V>();

	private readonly Func<Transform, GameObject, V> mCreateFunc;

	public GamePrefabTypeSetPools(Transform rootSlot, Func<Transform, GameObject, V> createFunc)
	{
		mRootNode = rootSlot;
		mCreateFunc = createFunc;
	}

	public V GetPoolByPreFab(GameObject prefab)
	{
		if ((Object)(object)prefab == (Object)null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return null;
		}
		int instanceID = ((Object)prefab).GetInstanceID();
		if (!mPoolsDict.TryGetValue(instanceID, out var value))
		{
			value = mCreateFunc(mRootNode, prefab);
			mPoolsDict.Add(instanceID, value);
		}
		return value;
	}

	public void Release()
	{
		foreach (KeyValuePair<int, V> item in mPoolsDict)
		{
			item.Value.Release();
		}
	}
}

}

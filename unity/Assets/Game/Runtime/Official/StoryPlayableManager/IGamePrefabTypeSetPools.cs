// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
public interface IGamePrefabTypeSetPools<T, V> where T : Component, IGameObjectPoolItem where V : GameObjectPoolBase<T>
{
	V GetPoolByPreFab(GameObject prefab);

	void Release();
}

}

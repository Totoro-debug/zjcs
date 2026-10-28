// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
	public interface IMyPlayableManager
	{
		Transform PlayableSlot { get; }

		MyPlayable Play(GameObject prefab, GameObject source, GameObject target, float speed = 1f, GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null);

		void RemoveAllPlayable(bool isReleasePool);

		void SetObjectPool(GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> pools);

		void ForceReleaseObjectPool();
	}
}

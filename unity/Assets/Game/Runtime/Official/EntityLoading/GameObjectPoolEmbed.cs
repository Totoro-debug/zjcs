// Original client/framework implementation; see entity loading source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityLoading
{
public class GameObjectPoolEmbed<T> : GameObjectPoolBase<T> where T : Component
{
	public static Func<Transform, Transform, GameObject, bool, GameObject> EmbedDefaulCreateFunc => (Transform rootNode, Transform slot, GameObject goPrefab, bool active) =>
	{
		GameObject val = StoryPlayableInstantiation.Instantiate(rootNode, goPrefab, slot, false);
		val.SetActive(active);
		return val;
	};

	public GameObjectPoolEmbed(Transform goSlot, GameObject goPrefab)
	{
		GameObjectPoolEmbed<T> gameObjectPoolEmbed = this;
		mGoPrefab = goPrefab;
		mGoSlotNode = goSlot;
		mCreateFunc = (Transform slot, GameObject _, bool active) => EmbedDefaulCreateFunc(goSlot, slot, gameObjectPoolEmbed.mGoPrefab, active);
		mPoolTransform = goSlot;
	}

	public override GameObject BorrowGo(Transform slot = null, bool autoActive = true)
	{
		GameObject val = null;
		if (mGoQueue.Count > 0)
		{
			val = mGoQueue.Dequeue();
		}
		if ((Object)(object)val == (Object)null)
		{
			val = mCreateFunc(GetFinalSlotNode(slot), mGoPrefab, autoActive);
		}
		else if (autoActive)
		{
			val.SetActive(true);
		}
		if ((Object)(object)val != (Object)null)
		{
			Reset(val.GetComponent<T>());
		}
		else
		{
			UnityEngine.Debug.LogWarning("Original entity loading diagnostic");
		}
		return val;
	}

	public override void Return(T component)
	{
		if ((Object)(object)component != (Object)null)
		{
			if (component is IGameObjectPoolItem gameObjectPoolItem)
			{
				gameObjectPoolItem.OnGameObjectPoolClear();
			}
			GameObject gameObject = ((Component)component).gameObject;
			if ((Object)(object)gameObject != (Object)null && IsUsable())
			{
				gameObject.SetActive(false);
				mGoQueue.Enqueue(gameObject);
			}
		}
		else
		{
			UnityEngine.Debug.LogWarning("Original entity loading diagnostic");
		}
	}
}

}

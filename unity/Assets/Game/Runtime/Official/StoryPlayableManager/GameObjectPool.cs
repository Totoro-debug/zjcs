// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
public class GameObjectPool<T> : GameObjectPoolBase<T> where T : Component
{
	protected string mGameObjectPoolName;

	protected Transform mRootNode;

	private IDisposable mDisposableBag;

	private GameObjectDestoryListener mPoolTransformGoDestoryListener;

	public GameObjectPool(Transform rootSlot, Transform goSlot, GameObject goPrefab)
	{
		GameObjectPool<T> gameObjectPool = this;
		mRootNode = rootSlot;
		mGoPrefab = goPrefab;
		mGoSlotNode = goSlot;
		mCreateFunc = (Transform slot, GameObject _, bool autoActive) => GameObjectPoolBase<T>.DefaulCreateFunc(rootSlot, slot, gameObjectPool.mGoPrefab, autoActive);
		mGameObjectPoolName = (((Object)(object)mGoPrefab != (Object)null) ? ((Object)mGoPrefab).name : "Unknown");
		InitPool();
	}

	public GameObjectPool(Transform rootSlot, GameObject goPrefab)
		: this(rootSlot, (Transform)null, goPrefab)
	{
	}

	public GameObjectPool(Transform rootSlot, T component)
		: this(rootSlot, (object)component != null ? component.gameObject : null) { }

	public GameObjectPool(Transform rootSlot, Transform goSlot, T component)
		: this(rootSlot, goSlot, (object)component != null ? component.gameObject : null) { }

	public GameObjectPool(Transform rootSlot, Func<Transform, GameObject, bool, GameObject> createFunc, string gameObjectPoolName = "Default")
	{
		mRootNode = rootSlot;
		mCreateFunc = createFunc;
		mGameObjectPoolName = gameObjectPoolName;
		InitPool();
	}

	private void InitPool()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		mPoolTransform = new GameObject("GameObjectPool_For_" + mGameObjectPoolName).transform;
		mPoolTransformGoDestoryListener = mPoolTransform.gameObject.AddComponent<GameObjectDestoryListener>();
		mDisposableBag = mPoolTransformGoDestoryListener.OnDestoryed.Subscribe(Destory);
		mPoolTransform.SetParent(mRootNode, false);
		mPoolTransform.localScale = Vector3.one;
		((Component)mPoolTransform).gameObject.SetActive(false);
	}

	public override void Destory()
	{
		mDisposableBag.Dispose();
		mPoolTransformGoDestoryListener = null;
		if ((Object)(object)mPoolTransform != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)mPoolTransform).gameObject);
			mPoolTransform = null;
		}
		else
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		base.Destory();
	}

	protected override bool IsUsable()
	{
		if (!base.IsUsable())
		{
			return false;
		}
		if ((Object)(object)mPoolTransformGoDestoryListener == (Object)null)
		{
			return false;
		}
		return !mPoolTransformGoDestoryListener.IsBeingDestroyed;
	}
}

}

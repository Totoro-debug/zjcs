// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
	public class MyPlayableManager : IMyPlayableManager, IDisposable
	{
		private Transform mSlot;

		private bool mUsePool;

		private IGamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> mMyPlayablePools;

		private HashSet<MyPlayable> mActivedPlayables;

		private bool mRemovingAll;

		private Action<MyPlayable> mDestroyCallback;

		private IDisposable mDisposableBag;

		public Transform PlayableSlot => mSlot;

		public IStoryPlayableInstanceFactory InstanceFactory { get; set; }

		public GameObject ScopeRoot { get; set; }

		public Action<ActivePlayableManagerInfo> ActivePublisher { get; set; }

		public Action<InactivePlayableManagerInfo> InactivePublisher { get; set; }

		public GAction LowMemory { get; set; }

		public IMyPlayableCenter MyPlayableCenter { get; set; }

		public void Initialize()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			mSlot = new GameObject("MyPlayableManager").transform;
			mUsePool = true;
			mActivedPlayables = new HashSet<MyPlayable>();
			mRemovingAll = false;
			mDestroyCallback = ReturnMyPlayable;
			mDisposableBag = LowMemory.Subscribe(MemoryManager_OnLowMemory);
			mSlot.SetParent(ScopeRoot.transform);
		}

		public void Dispose()
		{
			mDisposableBag.Dispose();
			mSlot = null;
			mUsePool = true;
			mMyPlayablePools = null;
			mActivedPlayables = null;
			mRemovingAll = false;
			mDestroyCallback = null;
		}

		public void SetObjectPool(GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> pools = null)
		{
			if (mMyPlayablePools == null)
			{
				if (pools != null)
				{
					mMyPlayablePools = pools;
				}
				else
				{
					mMyPlayablePools = new GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>>(mSlot, func);
				}
			}
			static GameObjectPool<MyPlayable> func(Transform poolRoot, GameObject prefab)
			{
				return new GameObjectPool<MyPlayable>(poolRoot, prefab);
			}
		}

		public MyPlayable Play(GameObject prefab, GameObject source, GameObject target, float speed = 1f, GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null)
		{
			if (mRemovingAll)
			{
				return null;
			}
			if ((Object)(object)prefab == (Object)null)
			{
				return null;
			}
			SetObjectPool();
			MyPlayable myPlayable = GetMyPlayable(prefab);
			mActivedPlayables.Add(myPlayable);
			((Component)myPlayable).gameObject.SetActive(true);
			myPlayable.Init(source, target, speed, parentGo, param, needStopObj);
			myPlayable.SetDestroyCallback(mDestroyCallback);
			myPlayable.TryPlay();
			return myPlayable;
		}

		public void RemoveAllPlayable(bool isReleasePool)
		{
			try
			{
				if (mRemovingAll || mActivedPlayables == null || mActivedPlayables.Count == 0)
				{
					return;
				}
				mRemovingAll = true;
				try
				{
					foreach (MyPlayable mActivedPlayable in mActivedPlayables)
					{
						try
						{
							mActivedPlayable.Remove();
						}
						catch (Exception exception)
						{
							UnityEngine.Debug.LogException(exception);
						}
					}
					mActivedPlayables.Clear();
				}
				finally
				{
					mRemovingAll = false;
				}
				if (isReleasePool && mMyPlayablePools != null)
				{
					mMyPlayablePools.Release();
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
		}

		public void OnActivate()
		{
			ActivePublisher.Invoke(new ActivePlayableManagerInfo
			{
				MyPlayableManager = this
			});
		}

		public void OnDeactivate()
		{
			InactivePublisher.Invoke(new InactivePlayableManagerInfo
			{
				MyPlayableManager = this
			});
		}

		private void MemoryManager_OnLowMemory()
		{
			if (mMyPlayablePools != null)
			{
				mMyPlayablePools.Release();
			}
		}

		private MyPlayable GetMyPlayable(GameObject prefab)
		{
			MyPlayable myPlayable;
			if (mUsePool)
			{
				myPlayable = mMyPlayablePools.GetPoolByPreFab(prefab).Borrow(mSlot);
				myPlayable.Prefab = prefab;
			}
			else
			{
				GameObject val = InstanceFactory.Instantiate(prefab, mSlot, false);
				val.SetActive(true);
				myPlayable = val.GetComponent<MyPlayable>();
			}
			return myPlayable;
		}

		private void ReturnMyPlayable(MyPlayable myPlayable)
		{
			if ((Object)(object)myPlayable == (Object)null)
			{
				return;
			}
			if (!mRemovingAll)
			{
				mActivedPlayables.Remove(myPlayable);
			}
			myPlayable.ResetData();
			if (mUsePool)
			{
				if ((Object)(object)myPlayable.Prefab != (Object)null)
				{
					mMyPlayablePools.GetPoolByPreFab(myPlayable.Prefab).Return(myPlayable);
				}
			}
			else
			{
				Object.Destroy((Object)(object)((Component)myPlayable).gameObject);
			}
		}

		public void PrepareLoadObjects(IReadOnlyList<PreLoadData> objArr)
		{
			if (!mUsePool)
			{
				UnityEngine.Debug.LogWarning("Official playable diagnostic");
			}
			else
			{
				if (objArr == null || objArr.Count == 0)
				{
					return;
				}
				SetObjectPool();
				foreach (PreLoadData item in objArr)
				{
					if (!((Object)(object)item.Prefab == (Object)null) && item.Count > 0)
					{
						mMyPlayablePools.GetPoolByPreFab(item.Prefab).Extend(item.Count);
					}
				}
			}
		}

		public void ForceReleaseObjectPool()
		{
			mMyPlayablePools?.Release();
		}
	}
}

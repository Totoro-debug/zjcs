// Original client/framework implementation; see entity loading source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityLoading
{
public sealed class TTLGameObjectPool<T> where T : Component
{
	private const float kExpireCheckIntervalS = 1f;

	private readonly float mTTLSecondes = -1f;

	private readonly bool mTTLIgnoreBackground;

	private readonly GameObjectPoolBase<T> mPool;

	private SortedValueDictionary<int, float> mTTLDict = new SortedValueDictionary<int, float>();

	private HashSet<int> mPendingRemoveElemSet = new HashSet<int>();

	private float mLastExpireCheckTime;

	public string RootName
	{
		get
		{
			RemoveExpireElement();
			return mPool.RootName;
		}
		set
		{
			RemoveExpireElement();
			mPool.RootName = value;
		}
	}

	public TTLGameObjectPool(GameObjectPoolBase<T> pool, float ttlSeconds, bool ttlIgnoreBackground = false)
	{
		mTTLSecondes = ttlSeconds;
		mTTLIgnoreBackground = ttlIgnoreBackground;
		mPool = pool;
	}

	public void Extend(int count = 1)
	{
		RemoveExpireElement();
		mPool.Extend(count);
	}

	public void MakeSureCount(int count)
	{
		RemoveExpireElement();
		mPool.MakeSureCount(count);
	}

	public void Release(int count)
	{
		RemoveExpireElement();
		mPool.Release(count);
		RemoveElements(count);
	}

	public void Release()
	{
		RemoveExpireElement();
		mPool.Release();
		mTTLDict.Clear();
	}

	public T Borrow(Transform slot = null, bool autoActive = true)
	{
		T val = mPool.Borrow(slot, autoActive);
		if ((Object)(object)val != (Object)null)
		{
			RemoveTTL(((Object)((Component)val).gameObject).GetInstanceID());
		}
		RemoveExpireElement();
		return val;
	}

	public GameObject BorrowGo(Transform slot = null, bool autoActive = true)
	{
		GameObject val = mPool.BorrowGo(slot, autoActive);
		if ((Object)(object)val != (Object)null)
		{
			RemoveTTL(((Object)val).GetInstanceID());
		}
		RemoveExpireElement();
		return val;
	}

	public void RecycleMany(List<T> list, int count)
	{
		RemoveExpireElement();
		AddTTLs(list);
		mPool.RecycleMany(list, count);
		RemoveTTLs(list);
	}

	public void Reset(T component)
	{
		RemoveExpireElement();
		mPool.Reset(component);
	}

	public void Return(T component)
	{
		RemoveExpireElement();
		mPool.Return(component);
		if ((Object)(object)component != (Object)null)
		{
			AddTTL(((Component)component).gameObject);
		}
	}

	public void ReturnMany(List<T> list)
	{
		RemoveExpireElement();
		AddTTLs(list);
		mPool.ReturnMany(list);
	}

	public void Destory()
	{
		mPool.Destory();
		mTTLDict.Clear();
	}

	public void CheckExpire()
	{
		RemoveExpireElement();
	}

	private void AddTTL(GameObject go)
	{
		int instanceID = ((Object)go).GetInstanceID();
		if (mTTLSecondes > 0f)
		{
			mTTLDict[instanceID] = GetCurrentTime() + mTTLSecondes;
		}
		else
		{
			RemoveTTL(instanceID);
		}
	}

	private void RemoveTTL(int instanceID)
	{
		mTTLDict.Remove(instanceID);
	}

	private void AddTTLs(List<T> list)
	{
		foreach (T item in list)
		{
			if (!((Object)(object)item == (Object)null))
			{
				AddTTL(((Component)item).gameObject);
			}
		}
	}

	private void RemoveTTLs(List<T> list)
	{
		foreach (T item in list)
		{
			if (!((Object)(object)item == (Object)null))
			{
				RemoveTTL(((Object)((Component)item).gameObject).GetInstanceID());
			}
		}
	}

	private void RemoveExpireElement()
	{
		if (mTTLDict.Count <= 0)
		{
			return;
		}
		float currentTime = GetCurrentTime();
		if (currentTime < mLastExpireCheckTime + 1f)
		{
			return;
		}
		mPendingRemoveElemSet.Clear();
		foreach (var item2 in mTTLDict)
		{
			var (item, _) = item2;
			if (item2.Value > currentTime)
			{
				break;
			}
			mPendingRemoveElemSet.Add(item);
		}
		if (mPendingRemoveElemSet.Count <= 0)
		{
			return;
		}
		foreach (int item3 in mPendingRemoveElemSet)
		{
			RemoveTTL(item3);
		}
		mPool.Release(mPendingRemoveElemSet.Count);
	}

	private void RemoveElements(int removeCount)
	{
		if (removeCount <= 0 || mTTLDict.Count <= 0)
		{
			return;
		}
		if (mTTLDict.Count <= removeCount)
		{
			mTTLDict.Clear();
			return;
		}
		mPendingRemoveElemSet.Clear();
		int num = removeCount;
		foreach (var item2 in mTTLDict)
		{
			int item = item2.Key;
			if (num-- <= 0)
			{
				break;
			}
			mPendingRemoveElemSet.Add(item);
		}
		foreach (int item3 in mPendingRemoveElemSet)
		{
			RemoveTTL(item3);
		}
	}

	private float GetCurrentTime()
	{
		if (mTTLIgnoreBackground)
		{
			return Time.realtimeSinceStartup;
		}
		return Time.time;
	}
}

}

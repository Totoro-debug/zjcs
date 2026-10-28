// Original client/framework implementation; see entity loading source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Zjcs.Official.EntityLoading
{
public sealed class TTLGameObjectPoolCollection<TKey, TValue> : IDisposable where TValue : Component
{
	public const int kExpireCheckIntervalS = 2;

	private Dictionary<TKey, TTLGameObjectPool<TValue>> mPoolDict = new Dictionary<TKey, TTLGameObjectPool<TValue>>();

	private readonly IDisposable mExpirySubscription;

	public TTLGameObjectPoolCollection(IEntityPoolScheduler scheduler)
	{
		mExpirySubscription = scheduler.Subscribe(new TimeSpan(0, 0, 2), CheckExpire);
	}

	public void Dispose()
	{
		mExpirySubscription.Dispose();
	}

	public bool TryGetPool(TKey key, out TTLGameObjectPool<TValue> pool)
	{
		return mPoolDict.TryGetValue(key, out pool);
	}

	public bool TryAdd(TKey key, TTLGameObjectPool<TValue> pool)
	{
		return mPoolDict.TryAdd(key, pool);
	}

	public void Clear()
	{
		foreach (KeyValuePair<TKey, TTLGameObjectPool<TValue>> item in mPoolDict)
		{
			var tTLGameObjectPool = item.Value;
			TTLGameObjectPool<TValue> tTLGameObjectPool2 = tTLGameObjectPool;
			tTLGameObjectPool2.Release();
			tTLGameObjectPool2.Destory();
		}
		mPoolDict.Clear();
	}

	public void Release()
	{
		foreach (KeyValuePair<TKey, TTLGameObjectPool<TValue>> item in mPoolDict)
		{
			var tTLGameObjectPool = item.Value;
			tTLGameObjectPool.Release();
		}
	}

	public bool Remove(TKey key)
	{
		if (!mPoolDict.TryGetValue(key, out var value))
		{
			return false;
		}
		value.Release();
		value.Destory();
		return mPoolDict.Remove(key);
	}

	private void CheckExpire()
	{
		foreach (KeyValuePair<TKey, TTLGameObjectPool<TValue>> item in mPoolDict)
		{
			var tTLGameObjectPool = item.Value;
			tTLGameObjectPool.CheckExpire();
		}
	}
}

}

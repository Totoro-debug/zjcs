// Complete original map-display application class; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.MapDisplays
{
public class MapDisplayPoolHandle : IDisposable
{
	private bool mIsDisposed;

	private EntityMapDisplayInfo mEntityMapDisplayInfo;

	private IServiceProvider mServiceProvider;

	private Queue<IECViewDriver> Pool;

	private MapDisplayAssetLease mAssetOperationHandle;

	public int LoanedCount { get; private set; }

	public MapDisplayPoolHandle(EntityMapDisplayInfo info, IMapDisplayResourceLoader resourceLoader, IServiceProvider serviceProvider)
	{


		mEntityMapDisplayInfo = info;
		mServiceProvider = serviceProvider;
		Pool = new Queue<IECViewDriver>();
		LoanedCount = 0;
		mIsDisposed = false;
		mAssetOperationHandle = resourceLoader.LoadPrefab(info.DisplayPath);
		if ((!mAssetOperationHandle.IsValid && mAssetOperationHandle.Status != 2) || (Object)(object)mAssetOperationHandle.Result == (Object)null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			Dispose();
		}
	}

	public IECViewDriver Acquire()
	{
		if (mIsDisposed)
		{
			return null;
		}
		IECViewDriver component = default;
		if (!Pool.TryDequeue(out component))
		{
			GameObject val = MapDisplayServices.Instantiate(mServiceProvider, mAssetOperationHandle.Result);
			if ((Object)(object)val == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
				return null;
			}
			component = val.GetComponent<IECViewDriver>();
			if (component == null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
				Object.Destroy((Object)(object)val);
				return null;
			}
			val.SetActive(false);
		}
		if (component == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return null;
		}
		LoanedCount++;
		return component;
	}

	public bool Recycle(IECViewDriver eCViewDriver)
	{
		if (mIsDisposed)
		{
			return false;
		}
		if (eCViewDriver == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return false;
		}
		if (!(eCViewDriver is MonoBehaviour))
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return false;
		}
		if (LoanedCount <= 0)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return false;
		}
		Pool.Enqueue(eCViewDriver);
		LoanedCount--;
		return true;
	}

	public void Dispose()
	{
		if (mIsDisposed)
		{
			return;
		}
		IECViewDriver iECViewDriver = default;
		while (Pool.TryDequeue(out iECViewDriver))
		{
			if (iECViewDriver != null)
			{
				MonoBehaviour val = (MonoBehaviour)((iECViewDriver is MonoBehaviour) ? iECViewDriver : null);
				if (val != null && !((Object)(object)val == (Object)null))
				{
					Object.Destroy((Object)(object)((Component)val).gameObject);
				}
			}
		}
		Pool = null;
		mAssetOperationHandle.Dispose();
		mAssetOperationHandle = null;
		mEntityMapDisplayInfo = null;
		mServiceProvider = null;
		LoanedCount = 0;
		mIsDisposed = true;
	}
}

}

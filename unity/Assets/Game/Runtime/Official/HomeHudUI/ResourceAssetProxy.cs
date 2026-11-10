namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public class ResourceAssetProxy<T> : Zjcs.Official.ProfessionUI.AssetProxyBase<T> where T : Object
{
	public GameObject ContainerSource;

	protected IHomeHudAssetHandle<T> mHandle;

	private Action<T> mCompletedCallback;

	private IHomeHudResourceLoader ResourceLoader => HomeHudResourceScope.Resolve(ContainerSource);

	public override T Load()
	{
		if (!IsValid)
		{
			return default;
		}
		if (mHandle != null && mHandle.IsValid)
		{
			return mHandle.WaitForAsyncComplete();
		}
		if (ResourceLoader == null)
		{
			throw new NullReferenceException("ResourceLoader");
		}
		mHandle = ResourceLoader.LoadAssetByGuid<T>(Guid);
		mHandle.Completed += HandleCompleted;
		return mHandle.Result;
	}

	public override void LoadAsync(Action<T> callback)
	{
		if (callback == null)
		{
			return;
		}
		if (!IsValid)
		{
			callback(default);
			return;
		}
		if (mHandle != null && mHandle.IsValid)
		{
			int status = mHandle.Status;
			switch ((int)status)
			{
			case 1:
				mCompletedCallback = (Action<T>)Delegate.Combine(mCompletedCallback, callback);
				return;
			case 2:
				callback(mHandle.Result);
				return;
			}
		}
		if (ResourceLoader == null)
		{
			throw new NullReferenceException("ResourceLoader");
		}
		mCompletedCallback = callback;
		mHandle = ResourceLoader.LoadAssetAsyncByGuid<T>(Guid);
		mHandle.Completed += HandleCompleted;
	}

	public override void Unload()
	{
		if (mHandle != null)
		{
			if (mHandle.IsValid)
			{
				mHandle.Dispose();
			}
			mHandle = null;
		}
	}

	private void HandleCompleted(IHomeHudAssetHandle<T> asset)
	{
		if (mHandle != null)
		{
			if (!mHandle.IsValid || (int)mHandle.Status != 2)
			{
				mHandle = null;
			}
			else if (mCompletedCallback != null)
			{
				mCompletedCallback(mHandle.Result);
				mCompletedCallback = null;
			}
		}
	}
}

}

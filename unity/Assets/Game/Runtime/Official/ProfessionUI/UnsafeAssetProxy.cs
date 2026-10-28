namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public class UnsafeAssetProxy<T> : AssetProxyBase<T> where T : Object
{
	private IProfessionUnsafeAssetHandle<T> mHandle;

	private Action<T> mCompletedCallback;

	public override T Load()
	{

		if (!IsValid)
		{
			return default;
		}
		if (mHandle != null)
		{
			if (mHandle.IsValid)
			{
				mHandle.WaitForAsyncComplete();
				if ((int)mHandle.Status == 2)
				{
					return mHandle.Result;
				}
			}
			mHandle.Release();
		}
		mHandle = ProfessionAppearanceTextureBindings.Required.LoadAssetSyncByGuid<T>(Guid);
		HandleCompleted(mHandle);
		IProfessionUnsafeAssetHandle<T> val = mHandle;
		if (val == null)
		{
			return default;
		}
		return val.Result;
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
		if (mHandle != null)
		{
			if (mHandle.IsValid)
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
			mHandle.Release();
		}
		mCompletedCallback = callback;
		mHandle = ProfessionAppearanceTextureBindings.Required.LoadAssetSyncByGuid<T>(Guid);
		mHandle.Completed += HandleCompleted;
	}

	public override void Unload()
	{
		if (mHandle != null)
		{
			mHandle.Release();
			mHandle = null;
		}
	}

	private void HandleCompleted(IProfessionUnsafeAssetHandle<T> handle)
	{

		if (mHandle != null)
		{
			if (!mHandle.IsValid || (int)mHandle.Status != 2)
			{
				mHandle.Release();
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

using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
public abstract class LoaderProxyBase<TLoader, TData, TDataProxy> : PreviewableProxyBase<TLoader, TData, TDataProxy>, IDisposable where TLoader : Object where TData : Object where TDataProxy : AssetGuidProxy<TData>
{
	private bool mIsDisposed;

	private ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();

	private IProfessionAssetOperation<TData> mCurrentAssetOperationHandle;

	public void Load(string path)
	{
		if (!mIsDisposed)
		{
			IProfessionImageResourceLoader resourceLoader = GetResourceLoader();
			if (resourceLoader != null)
			{
				mCurrentAssetOperationHandle = mDisposableBag.Add(resourceLoader.LoadAsset<TData>(path));
				HandleCompleted(mCurrentAssetOperationHandle);
			}
		}
	}

	public void LoadAsync(string path)
	{
		if (!mIsDisposed)
		{
			IProfessionImageResourceLoader resourceLoader = GetResourceLoader();
			if (resourceLoader != null)
			{
				mCurrentAssetOperationHandle = mDisposableBag.Add(resourceLoader.LoadAssetAsync<TData>(path));
				mCurrentAssetOperationHandle.Completed += HandleCompleted;
			}
		}
	}

	public void LoadByGuid(string guid)
	{
		if (!mIsDisposed)
		{
			IProfessionImageResourceLoader resourceLoader = GetResourceLoader();
			if (resourceLoader != null)
			{
				mCurrentAssetOperationHandle = mDisposableBag.Add(resourceLoader.LoadAssetByGuid<TData>(guid));
				HandleCompleted(mCurrentAssetOperationHandle);
			}
		}
	}

	public void LoadByGuidAsync(string guid)
	{
		if (!mIsDisposed)
		{
			IProfessionImageResourceLoader resourceLoader = GetResourceLoader();
			if (resourceLoader != null)
			{
				mCurrentAssetOperationHandle = mDisposableBag.Add(resourceLoader.LoadAssetAsyncByGuid<TData>(guid));
				mCurrentAssetOperationHandle.Completed += HandleCompleted;
			}
		}
	}

	public void Clear()
	{
		mCurrentAssetOperationHandle = null;
		mDisposableBag.Clear();
	}

	public void Dispose()
	{
		if (!mIsDisposed)
		{
			mCurrentAssetOperationHandle = null;
			mDisposableBag.Dispose();
			mIsDisposed = true;
		}
	}

	private void HandleCompleted(IProfessionAssetOperation<TData> assetOperationHandle)
	{
		if (!mIsDisposed && mCurrentAssetOperationHandle == assetOperationHandle)
		{
			if (mCurrentAssetOperationHandle.IsValid && (int)mCurrentAssetOperationHandle.Status == 2)
			{
				Load(mCurrentAssetOperationHandle.Result);
			}
			else
			{
				Load(default(TData));
			}
			mCurrentAssetOperationHandle = null;
		}
	}

	protected abstract IProfessionImageResourceLoader GetResourceLoader();
}

}

// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class PartitionExtension : PartitionAsset
{
	private Transform mHostTrans;

	private IPartitionInstanceFactory mServiceProvider;

	private GameObject mInstance;

	private PartitionAssetHandle<GameObject> mHandle;

	private int mRefCount;

	public override ChunkAssetType Type => ChunkAssetType.Extension;

	public PartitionExtension(ushort id, string res, Transform hostTrans, IPartitionInstanceFactory serviceProvider)
		: base(id, res)
	{
		mHostTrans = hostTrans;
		mServiceProvider = serviceProvider;
	}

	public override void Load(IPartitionResourceLoader resourceLoader)
	{
		mRefCount++;
		if (mRefCount == 1)
		{
			DidLoad(resourceLoader);
		}
	}

	private void DidLoad(IPartitionResourceLoader resourceLoader)
	{
		mLoadedState = ChunkAssetState.Loading;
		PartitionAssetHandle<GameObject> assetOperationHandle = resourceLoader.LoadAssetAsync<GameObject>(mRes);
		assetOperationHandle.Completed += OnLoaded;
		mHandle = assetOperationHandle;
	}

	public override void Unload()
	{
		mRefCount--;
		if (mRefCount == 0)
		{
			DidUnload();
		}
	}

	private void DidUnload()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (mLoadedState != ChunkAssetState.Loaded)
		{
			UnityEngine.Debug.LogError("PartitionExtension is not Loaded,");
		}
		mLoadedState = ChunkAssetState.Unloaded;
		Object.Destroy((Object)(object)mInstance);
		mInstance = null;
		ReleaseHandle();
	}

	private void OnLoaded(PartitionAssetHandle<GameObject> handle)
	{
		mLoadedState = ChunkAssetState.Loaded;
		handle.Completed -= OnLoaded;
		if (mRemoved)
		{
			ReleaseHandle();
		}
		else if (!((Object)(object)mHostTrans == (Object)null))
		{
			GameObject result = handle.Result;
			GameObject val = mServiceProvider.Instantiate(result, mHostTrans, false);
			mInstance = val;
		}
	}

	private void ReleaseHandle()
	{
		mHandle.Release(tryUnloadAsset: true);
		mHandle = null;
	}

	public override void Remove()
	{
		if (!mRemoved)
		{
			mRemoved = true;
			if (mLoadedState == ChunkAssetState.Loaded)
			{
				DidUnload();
			}
		}
	}

	protected override void OnRuntimeStateUpdate()
	{
	}
}

}

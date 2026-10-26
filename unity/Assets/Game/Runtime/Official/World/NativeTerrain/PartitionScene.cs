// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class PartitionScene : PartitionAsset
{
	private Transform mHostTrans;

	private IPartitionInstanceFactory mServiceProvider;

	private GameObject mInstance;

	private PartitionAssetHandle<GameObject> mHandle;

	public override ChunkAssetType Type => ChunkAssetType.Scene;

	public PartitionScene(ushort id, string res, Transform hostTrans, IPartitionInstanceFactory serviceProvider)
		: base(id, res)
	{
		mHostTrans = hostTrans;
		mServiceProvider = serviceProvider;
	}

	public override void Load(IPartitionResourceLoader resourceLoader)
	{
		if (mLoadedState == ChunkAssetState.Unloaded)
		{
			mLoadedState = ChunkAssetState.Loading;
			PartitionAssetHandle<GameObject> assetOperationHandle = resourceLoader.LoadAssetAsync<GameObject>(mRes);
			assetOperationHandle.Completed += OnSceneChunkLoaded;
			mHandle = assetOperationHandle;
		}
	}

	public override void Unload()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (mLoadedState != ChunkAssetState.Loaded)
		{
			UnityEngine.Debug.LogError("PartitionScene is not Loaded,");
		}
		mLoadedState = ChunkAssetState.Unloaded;
		Object.Destroy((Object)(object)mInstance);
		mInstance = null;
		ReleaseHandle();
	}

	private void OnSceneChunkLoaded(PartitionAssetHandle<GameObject> assetHandle)
	{
		mLoadedState = ChunkAssetState.Loaded;
		assetHandle.Completed -= OnSceneChunkLoaded;
		if (mRemoved)
		{
			ReleaseHandle();
		}
		else if (!((Object)(object)mHostTrans == (Object)null))
		{
			GameObject result = assetHandle.Result;
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
				Unload();
			}
		}
	}

	protected override void OnRuntimeStateUpdate()
	{
	}
}

}

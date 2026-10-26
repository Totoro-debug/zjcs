// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;
using System;

public class PartitionTile : PartitionAsset
{
	private PartitionAssetHandle<TileChunkData> mHandle;

	private Action<TileChunkData> mLoadedAction;

	private Action<TileChunkData> mUnloadAction;

	private bool mPopulated;

	public override ChunkAssetType Type => ChunkAssetType.Tile;

	public PartitionTile(ushort id, string res, Action<TileChunkData> loadedAction, Action<TileChunkData> unloadAction)
		: base(id, res)
	{
		mLoadedAction = loadedAction;
		mUnloadAction = unloadAction;
	}

	public override void Load(IPartitionResourceLoader resourceLoader)
	{
		if (mLoadedState == ChunkAssetState.Unloaded)
		{
			mLoadedState = ChunkAssetState.Loading;
			(mHandle = resourceLoader.LoadAssetAsync<TileChunkData>(mRes)).Completed += OnLoaded;
		}
	}

	public override void Unload()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (mLoadedState != ChunkAssetState.Loaded)
		{
			UnityEngine.Debug.LogError("PartitionTile is not Loaded,");
		}
		mLoadedState = ChunkAssetState.Unloaded;
		if (mPopulated)
		{
			mUnloadAction(mHandle.Result);
			mPopulated = false;
		}
		ReleaseHandle();
	}

	private void OnLoaded(PartitionAssetHandle<TileChunkData> handle)
	{
		mLoadedState = ChunkAssetState.Loaded;
		handle.Completed -= OnLoaded;
		if (mRemoved)
		{
			ReleaseHandle();
		}
		else
		{
			OnRuntimeStateUpdate();
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
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (mLoadedState != ChunkAssetState.Loaded)
		{
			return;
		}
		if (mRuntimeState == ChunkRuntimeState.Active)
		{
			if (mPopulated)
			{
				UnityEngine.Debug.LogError(" already populated");
			}
			else
			{
				mPopulated = true;
				mLoadedAction(mHandle.Result);
			}
		}
		else if (mPopulated)
		{
			mPopulated = false;
			mUnloadAction(mHandle.Result);
		}
	}
}

}

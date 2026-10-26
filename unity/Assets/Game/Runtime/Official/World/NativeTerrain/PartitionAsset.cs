// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;

public abstract class PartitionAsset
{
	protected readonly string mRes;

	protected readonly ushort mId;

	protected ChunkRuntimeState mRuntimeState = ChunkRuntimeState.Active;

	protected ChunkAssetState mLoadedState;


	protected bool mRemoved;

	public string Res => mRes;

	public ushort Id => mId;

	public abstract ChunkAssetType Type { get; }

	public ChunkAssetState LoadedState => mLoadedState;

	protected PartitionAsset(ushort id, string res)
	{
		mId = id;
		mRes = res;
	}

	public abstract void Load(IPartitionResourceLoader resourceLoader);

	public abstract void Unload();


	public abstract void Remove();

	public void SetRuntimeState(ChunkRuntimeState state)
	{
		if (mRuntimeState != state)
		{
			mRuntimeState = state;
			OnRuntimeStateUpdate();
		}
	}

	protected abstract void OnRuntimeStateUpdate();
}

}

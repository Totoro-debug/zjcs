// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;
using System.Collections.Generic;

public class ChunkData
{
	public List<PartitionAsset> Assets = new List<PartitionAsset>();

	public int IdleFrameCount;

	public void Keep()
	{
		IdleFrameCount = 0;
	}

	public bool IsState(ChunkAssetState state)
	{
		if (Assets.Count == 0)
		{
			UnityEngine.Debug.LogError("Assets Count is 0");
			return false;
		}
		int i = 0;
		for (int count = Assets.Count; i < count; i++)
		{
			if (Assets[i].LoadedState != state)
			{
				return false;
			}
		}
		return true;
	}

	public bool CanUnload()
	{
		int i = 0;
		for (int count = Assets.Count; i < count; i++)
		{
			if (Assets[i].LoadedState == ChunkAssetState.Loading)
			{
				return false;
			}
		}
		return true;
	}
}

}

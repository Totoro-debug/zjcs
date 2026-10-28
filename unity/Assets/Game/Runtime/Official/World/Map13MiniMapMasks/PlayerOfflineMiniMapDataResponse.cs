using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMap
{
public class PlayerOfflineMiniMapDataResponse
{
	public int MapClassId;
	public Dictionary<MapWorldActiveableEntityType, List<int>> ActiveableEntDataDict;
	public List<int> ActiveRegions;
	public byte[] GridVisibilityByte;
	public Dictionary<int, PlayerOfflineMiniMapLiteEntityData> LiteEntityDataDict;

	public ushort GetOpCode()
	{
		return 26621;
	}
}

}

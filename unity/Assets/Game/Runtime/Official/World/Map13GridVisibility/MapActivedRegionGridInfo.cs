// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;


namespace Zjcs.Official.World.Map13GridVisibility
{
public class MapActivedRegionGridInfo : VisibilitySerializable
{

	public Pos2d[] RegionActivedGrids;

	public Pos2d[] RegionExtendVisiableGrids;

	public override ushort GetOpCode()
	{
		return 1493;
	}
}

}

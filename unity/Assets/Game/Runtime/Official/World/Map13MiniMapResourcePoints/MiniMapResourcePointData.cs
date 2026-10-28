using System;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
[Serializable]
public class MiniMapResourcePointData
{
	public int Area;

	public Pos2d PointPos;

	public MiniMapResourcePointTapItem Item;
}

}

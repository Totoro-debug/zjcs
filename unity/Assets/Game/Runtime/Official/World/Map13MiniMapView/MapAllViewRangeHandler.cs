// Complete original visible-range implementation; see Map13 minimap view source receipt.
using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
public class MapAllViewRangeHandler : IMapViewRangeHandler, IDisposable
{
	GAction<IMapViewRangeHandler> IMapViewRangeHandler.OnVisibleRangeUpdate { get; set; } = new GAction<IMapViewRangeHandler>();

	public IReadOnlyList<Pos2d> VisiblePosList => null;

	public bool IsEntityInViewRange(IMiniMapEntityBody gridTransform)
	{
		return true;
	}

	public bool IsPosInViewRange(Pos2d pos)
	{
		return true;
	}

	public bool IsRangeInViewRange(Pos2d minPos, Pos2d maxPos)
	{
		return true;
	}

	public void Dispose()
	{
	}
}

}

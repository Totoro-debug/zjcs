// Complete original visible-range implementation; see Map13 minimap view source receipt.
using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
public interface IMapViewRangeHandler : IDisposable
{
	GAction<IMapViewRangeHandler> OnVisibleRangeUpdate { get; set; }

	IReadOnlyList<Pos2d> VisiblePosList { get; }

	bool IsEntityInViewRange(IMiniMapEntityBody gridTransform);

	bool IsPosInViewRange(Pos2d pos);

	bool IsRangeInViewRange(Pos2d minPos, Pos2d maxPos);
}

}

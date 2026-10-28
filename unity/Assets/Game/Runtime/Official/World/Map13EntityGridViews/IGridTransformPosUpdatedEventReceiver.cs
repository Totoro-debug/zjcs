// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;



namespace Zjcs.Official.World.Map13EntityGridViews
{
public interface IGridTransformPosUpdatedEventReceiver : IEventReceiver
{
	void GridTransform_OnPosUpdated(Map13GridTransformData transform, Pos2d prePos);
}

}

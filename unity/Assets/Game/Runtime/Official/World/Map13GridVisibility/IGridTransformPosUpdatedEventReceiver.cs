// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;



namespace Zjcs.Official.World.Map13GridVisibility
{
public interface IGridTransformPosUpdatedEventReceiver : IEventReceiver
{
	void GridTransform_OnPosUpdated(GridTransformComponent transform, Pos2d prePos);
}

}

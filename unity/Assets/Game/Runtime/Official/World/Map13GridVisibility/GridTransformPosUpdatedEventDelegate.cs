// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;



namespace Zjcs.Official.World.Map13GridVisibility
{
public sealed class GridTransformPosUpdatedEventDelegate : EventDelegateBase<GridTransformPosUpdatedEventDelegate, IGridTransformPosUpdatedEventReceiver, GridTransformComponent, Pos2d>
{
	protected override void FireOnReceiver(IGridTransformPosUpdatedEventReceiver receiver, GridTransformComponent transform, Pos2d prePos)
	{
		receiver.GridTransform_OnPosUpdated(transform, prePos);
	}

	public override void FireEvent(GridTransformComponent transform, Pos2d prePos)
	{
		base.FireEvent(transform, prePos);
	}
}

}

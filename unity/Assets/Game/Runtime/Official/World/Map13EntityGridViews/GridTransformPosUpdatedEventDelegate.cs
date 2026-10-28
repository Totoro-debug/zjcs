// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;



namespace Zjcs.Official.World.Map13EntityGridViews
{
public sealed class GridTransformPosUpdatedEventDelegate : EventDelegateBase<GridTransformPosUpdatedEventDelegate, IGridTransformPosUpdatedEventReceiver, Map13GridTransformData, Pos2d>
{
	protected override void FireOnReceiver(IGridTransformPosUpdatedEventReceiver receiver, Map13GridTransformData transform, Pos2d prePos)
	{
		receiver.GridTransform_OnPosUpdated(transform, prePos);
	}

	public override void FireEvent(Map13GridTransformData transform, Pos2d prePos)
	{
		base.FireEvent(transform, prePos);
	}
}

}

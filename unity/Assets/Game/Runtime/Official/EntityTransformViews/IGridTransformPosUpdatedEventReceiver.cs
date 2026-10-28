// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Official.EntityTransformViews
{
public interface IGridTransformPosUpdatedEventReceiver : IEventReceiver
{
	void GridTransform_OnPosUpdated(ECGridTransformData transform, Pos2d prePos);
}

}

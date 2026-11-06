// Complete original movement implementation; see mainline character movement source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.ProfessionUI;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.EntityClientComponents
{
public enum MapMoveType
{
	Normal,
	Slide
}

}

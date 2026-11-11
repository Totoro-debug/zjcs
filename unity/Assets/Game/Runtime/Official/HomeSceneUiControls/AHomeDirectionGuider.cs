// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{

public abstract class AHomeDirectionGuider : AUIDirectionGuider
{

	protected override float GetFocusDuration()
	{
		return Services.DirectionGuideFocusDuration;
	}
}
}

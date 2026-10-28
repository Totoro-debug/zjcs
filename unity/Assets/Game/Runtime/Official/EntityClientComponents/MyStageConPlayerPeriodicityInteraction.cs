// Original client component; see client component source receipt.
using Zjcs.Official.EntityLifecycle;
namespace Zjcs.Official.EntityClientComponents
{

public class MyStageConPlayerPeriodicityInteraction : MyComponent<StageConPlayerPeriodicityInteractionComponentData, object>
{
	public int GetPeriodictyInteractionId(int index)
	{
		return Component.GetPeriodictyInteractionId(index);
	}
}

}

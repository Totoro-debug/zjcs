// Original client component; see client component source receipt.
using Zjcs.Official.EntityLifecycle;
namespace Zjcs.Official.EntityClientComponents
{

public class MyStageResultAwardExtraProp : MyComponent<StageResultAwardExtraPropComponentData, object>
{
	public object GetAwardExtraPropInfo(int compIndex)
	{
		return Component.GetAwardExtraPropInfo(compIndex);
	}
}

}

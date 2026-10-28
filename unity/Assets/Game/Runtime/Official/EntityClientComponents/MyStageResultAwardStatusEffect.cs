// Original client component; see client component source receipt.
using Zjcs.Official.EntityLifecycle;
namespace Zjcs.Official.EntityClientComponents
{

public class MyStageResultAwardStatusEffect : MyComponent<StageResultAwardStatusEffectComponentData, object>
{
	public object GetAwardStatusEffectInfo(int compIndex)
	{
		return Component.GetStatusEffectInfo(compIndex);
	}
}

}

// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{

public class PlanesMergeFunctionEntryBtn : FunctionEntryBtnBase
{
	protected override void OnBtnClick()
	{
		if (Services.CheckFunctionDisabled("PlanesIconEdit"))
		{
			Services.ShowError(53883, null, false);
			return;
		}
		Services.PopupPlanesMerge((IHomeMenuPanel panel) =>
		{
			panel.Init();
		});
	}
}
}

using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
namespace Zjcs.Official.HomeMainUiControls
{
// Original selected home UI control; local service bindings only.
public class FunctionEntrysPanel : FunctionEntrysPanelBase
{
	protected override void OnEntryBtnClick()
	{
		Windows.ReportFunctionEntryClick();
		Windows.PopupFunctionEntryDialog((IHomeMainFunctionEntryDialog dialog) =>
		{
			dialog.Init();
		});
	}
}
}

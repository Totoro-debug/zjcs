// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
public interface ITraceTaskHandler
{
	void OnStartTrace();

	void OnCancelTrace(long taskId);

	void OnTraceEnd();
}
}

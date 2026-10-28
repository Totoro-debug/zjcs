// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
namespace Zjcs.Official.EntityViews
{
public abstract class ECVisibleControlerViewBase : ECViewBase, IMyEntityViewModelControler
{
	bool IMyEntityViewModelControler.UpdateViewModelNodeShow(bool show)
	{
		return UpdateViewModelNodeShow(show);
	}

	protected abstract bool UpdateViewModelNodeShow(bool show);
}

}

// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public class ConstantClientVisibilitySupporter : ClientVisibilitySupporterBase
{
	public ConstantClientVisibilitySupporter(SpriteRendererList spriteRendererList, IMyEntityViewModelControler myEntityViewModelControler, Transform transform)
		: base(spriteRendererList, myEntityViewModelControler, transform)
	{
	}

	public override bool SetEntityVisible(int key, bool isVisible)
	{
		return false;
	}

	public override void SetForceVisible(bool forceVisible)
	{
	}

	public override bool NeedStop()
	{
		return false;
	}
}

}

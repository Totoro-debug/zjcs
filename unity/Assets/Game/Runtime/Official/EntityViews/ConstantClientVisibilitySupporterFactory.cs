// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public class ConstantClientVisibilitySupporterFactory : IClientVisibilitySupporterFactory
{
	public IClientVisibilitySupporter Create(SpriteRendererList spriteRendererList, IMyEntityViewModelControler myEntityViewModelControler, Transform transform)
	{
		return new ConstantClientVisibilitySupporter(spriteRendererList, myEntityViewModelControler, transform);
	}
}

}

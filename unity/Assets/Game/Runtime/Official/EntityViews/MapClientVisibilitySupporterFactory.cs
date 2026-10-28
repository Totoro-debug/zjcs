// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public class MapClientVisibilitySupporterFactory : IClientVisibilitySupporterFactory
{

	public IECViewMapRootLifetime MapRootManagerProxy { get; set; }

	public IClientVisibilitySupporter Create(SpriteRendererList spriteRendererList, IMyEntityViewModelControler myEntityViewModelControler, Transform transform)
	{
		return new MapECViewClientVisibilitySupporter(MapRootManagerProxy, spriteRendererList, myEntityViewModelControler, transform);
	}
}

}

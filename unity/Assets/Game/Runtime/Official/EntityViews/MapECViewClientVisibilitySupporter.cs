// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public class MapECViewClientVisibilitySupporter : ClientVisibilitySupporterBase
{
	private IECViewMapRootLifetime mMapRootMgr;

	protected override bool OwnerRootDestroying => mMapRootMgr.Destroying;

	public MapECViewClientVisibilitySupporter(IECViewMapRootLifetime mapRootMgr, SpriteRendererList spriteRendererList, IMyEntityViewModelControler myEntityViewModelControler, Transform transform)
		: base(spriteRendererList, myEntityViewModelControler, transform)
	{
		mMapRootMgr = mapRootMgr;
	}
}

}

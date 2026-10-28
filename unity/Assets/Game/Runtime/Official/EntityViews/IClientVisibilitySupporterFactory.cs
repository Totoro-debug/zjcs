// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public interface IClientVisibilitySupporterFactory
{
	IClientVisibilitySupporter Create(SpriteRendererList spriteRendererList, IMyEntityViewModelControler myEntityViewModelControler, Transform transform);
}

}

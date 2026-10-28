// Migrated original application/read-query code; see Map13 visibility-view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StorySourceFade;
using System.Collections.Generic;


namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public interface IEntityClientVisibilityHandlerFactory
{
	void GetHandlers(ClientVisibilityType visibilityType, ref List<IClientVisibilityHandler> handlers);

	IClientVisibilityHandler GetHandler(ClientVisibilityType visibilityType);

	void ReturnHanlder(IClientVisibilityHandler clientVisibilityHandler);
}

}

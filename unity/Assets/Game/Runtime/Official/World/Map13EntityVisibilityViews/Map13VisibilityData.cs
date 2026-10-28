// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;

namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public sealed class Map13VisibilityData
{
    public EcEntityClientVisibilityUpdatedEventDelegate OnClientVisibilityUpdate = new EcEntityClientVisibilityUpdatedEventDelegate();
    public ClientVisibilityType ClientVisibilityType;
    public bool IsShowNeedFadeIn;
public void AddClientVisibilityRequest(ClientVisibilityType clientVisibilityType)
	{
		ClientVisibilityType |= clientVisibilityType;
		OnClientVisibilityUpdate.FireEvent(this);
	}
public void RemoveClientVisibilityType(ClientVisibilityType clientVisibilityType)
	{
		ClientVisibilityType &= ~clientVisibilityType;
		OnClientVisibilityUpdate.FireEvent(this);
	}
}

}

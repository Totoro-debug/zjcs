// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;

namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public class Map13VisibilityData
{
    public EcEntityClientVisibilityUpdatedEventDelegate OnClientVisibilityUpdate = new EcEntityClientVisibilityUpdatedEventDelegate();
    public virtual ClientVisibilityType ClientVisibilityType { get; set; }
    public virtual bool IsShowNeedFadeIn { get; set; }
public virtual void AddClientVisibilityRequest(ClientVisibilityType clientVisibilityType)
	{
		ClientVisibilityType |= clientVisibilityType;
		OnClientVisibilityUpdate.FireEvent(this);
	}
public virtual void RemoveClientVisibilityType(ClientVisibilityType clientVisibilityType)
	{
		ClientVisibilityType &= ~clientVisibilityType;
		OnClientVisibilityUpdate.FireEvent(this);
	}
}

}

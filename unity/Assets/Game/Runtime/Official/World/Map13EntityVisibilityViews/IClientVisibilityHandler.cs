// Migrated original application/read-query code; see Map13 visibility-view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StorySourceFade;
using System;




namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public interface IClientVisibilityHandler
{
	ClientVisibilityType VisibilityType { get; }

	bool Visible { get; }

	ECViewEntityData Entity { get; }

	event Action<bool> OnVisibleChanged;

	void Init(ECViewEntityData entity, bool isNew);

	void Release();
}

}

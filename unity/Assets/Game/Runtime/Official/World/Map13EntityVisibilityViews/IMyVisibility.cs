// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;


namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public interface IMyVisibility
{
	bool IsVisible { get; }

	event Action OnVisibilityChanged;
}

}

// Original client/shared input source; see world input source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using WorldStageComponent = Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData;
using EntityOwnershipType = Zjcs.Official.EntityTipDotViews.EntityOwnershipType;
using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;
namespace Zjcs.Official.EntityWorldInput
{



[Serializable]
public class GridRect
{
	public Pos2d MinGrid;
	public Pos2d MaxGrid;

	public bool InRange(Pos2d pos)
	{
		if (pos.X >= MinGrid.X && pos.X <= MaxGrid.X && pos.Y >= MinGrid.Y)
		{
			return pos.Y <= MaxGrid.Y;
		}
		return false;
	}
}

}

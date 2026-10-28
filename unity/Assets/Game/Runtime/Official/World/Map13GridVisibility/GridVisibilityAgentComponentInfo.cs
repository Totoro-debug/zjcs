// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;





namespace Zjcs.Official.World.Map13GridVisibility
{
[Serializable]
public class GridVisibilityAgentComponentInfo : VisibilityComponentInfo
{

	public List<Pos2d> DefUnlockedPosList;

	public bool UnlockAllPos;
}

}

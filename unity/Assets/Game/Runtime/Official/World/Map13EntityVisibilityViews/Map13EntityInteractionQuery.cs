// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System.Collections.Generic;

namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public sealed class Map13EntityInteractionQuery
{
    public EcEntityPlayerInteractionUpdateEventDelegate OnInteractingPlayerUpdate = new EcEntityPlayerInteractionUpdateEventDelegate();
    // Exact source parser fields must be supplied by the catalog host.
    public bool IsInteractionUnique, IsInteractionByOther;
    public Map13EntitySourceQuery mEntSrcComp;
    public HashSet<long> InteractingPlayerIdSet { get; set; } = new HashSet<long>();
public bool CanInteract(long playerId)
	{
		if (!IsInteractionUnique)
		{
			return !InteractingPlayerIdSet.Contains(playerId);
		}
		if (!IsInteractionByOther && mEntSrcComp.Ownership.Type == EntityOwnershipType.Player && mEntSrcComp.Ownership.OwnerId != playerId)
		{
			return false;
		}
		return InteractingPlayerIdSet.Count <= 0;
	}
public bool IsPlayerInteracting(long playerId)
	{
		return InteractingPlayerIdSet.Contains(playerId);
	}
}

}

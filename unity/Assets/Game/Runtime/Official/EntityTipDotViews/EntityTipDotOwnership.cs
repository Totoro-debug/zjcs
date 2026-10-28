// Migrated original map tip-dot application code; see entity tip-dot receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityTipDotViews
{
public static class EntityTipDotOwnership
{
public static bool EntityShowTipForMe(ECEntityTipDotSourceData entitySource)
	{
		EntityOwnershipData entityOwnershipData = entitySource?.Ownership;
		if (entityOwnershipData == null || entityOwnershipData.OwnerId <= 0)
		{
			return true;
		}
		if (entityOwnershipData.Type != EntityOwnershipType.Player)
		{
			return true;
		}
		if (entityOwnershipData.OwnerId == EntityTipDotServices.Require.MainPlayerId)
		{
			return true;
		}
		EntityTipDotExtendInfo entityExtendParameterInfo = EntityTipDotServices.Require.GetExtendInfo(entitySource.Entity.ClassId);
		if (entityExtendParameterInfo == null)
		{
			return false;
		}
		return !entityExtendParameterInfo.InteractionUnique;
	}
}

}

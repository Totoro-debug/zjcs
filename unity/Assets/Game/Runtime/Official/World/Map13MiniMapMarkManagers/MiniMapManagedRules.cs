namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public static class MiniMapManagedRules
{
public static bool EntityShowTipForMe(MiniMapManagedContext context, IMiniMapSpawnEntity ent)
	{
		return EntityShowTipForMe(context, ent.GetComponent<IMiniMapManagedEntitySource>());
	}
public static bool EntityShowTipForMe(MiniMapManagedContext context, IMiniMapManagedEntitySource entitySource)
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
		if (entityOwnershipData.OwnerId == context.ManagedQueries.MainPlayerId)
		{
			return true;
		}
		MiniMapMarkExtendInfo entityExtendParameterInfo = context.MarkDataQueries.GetEntityExtend(entitySource.Entity.ClassId);
		if (entityExtendParameterInfo == null)
		{
			return false;
		}
		return !entityExtendParameterInfo.InteractionUnique;
	}
public static bool MapRegionStatusSupport(MapRegionStatus status, bool supportPublic, bool supportPrivate)
	{
		if (!supportPrivate && !supportPublic)
		{
			return false;
		}
		return status switch
		{
			MapRegionStatus.Private => supportPrivate, 
			MapRegionStatus.Public => supportPublic, 
			_ => false, 
		};
	}
public static string GetMapEntityMiniMapMark(MiniMapManagedContext context, IMiniMapSpawnEntity ent, int groupId, int mapClassId, string tag)
	{
		int area = context.MarkDataQueries.GridToArea(mapClassId, ent.GridTransform.Pos);
		return GetMapEntityMiniMapMark(context, ent.ClassId, groupId, mapClassId, area, tag);
	}
public static string GetMapEntityMiniMapMark(MiniMapManagedContext context, int entClassId, int groupId, int mapClassId, int area, string tag)
	{
		EntityMapUIDisplayInfo entityMapUIDisplayInfo = context.MarkDataQueries.GetEntityMapUIDisplay(entClassId, groupId, mapClassId, context.MarkDataQueries.GetAreaTag(mapClassId, area), tag);
		if (!string.IsNullOrEmpty(entityMapUIDisplayInfo?.MiniMapMark))
		{
			return entityMapUIDisplayInfo.MiniMapMark;
		}
		return context.MarkDataQueries.GetEntityExtend(entClassId)?.MiniMapMark;
	}
}

}

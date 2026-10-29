using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public static class MiniMapMarkSupportRules
{
public static bool IsIgnoreCheck(IMiniMapMarkSupportQueries queries, int groupId, long OwnerId, long playerId)
	{
		MiniMapPeriodicityInfo mapPeriodicityInteractionInfo = queries.GetPeriodicity(groupId);
		if (mapPeriodicityInteractionInfo == null || !mapPeriodicityInteractionInfo.IsIgnoreOwner)
		{
			return false;
		}
		return OwnerId == playerId;
	}
public static int CheckCanInteraction(IMiniMapMarkSupportQueries queries, int groupId, int count, bool isNegation = false)
	{
		MiniMapPeriodicityInfo mapPeriodicityInteractionInfo = queries.GetPeriodicity(groupId);
		if (mapPeriodicityInteractionInfo == null)
		{
			return 2157;
		}
		if (isNegation)
		{
			if (mapPeriodicityInteractionInfo.PeriodicityMaxCount > count)
			{
				return 2171;
			}
		}
		else if (mapPeriodicityInteractionInfo.PeriodicityMaxCount <= count)
		{
			return 2155;
		}
		return 0;
	}
public static int CheckCanPeriodicityInteraction(IMiniMapMarkSupportContext context, long ownerId, int periodicityInteractionId)
	{
		IMiniMapMarkSupportQueries mapPeriodicityInteractionInfoParser = context.SupportQueries;
		if (IsIgnoreCheck(mapPeriodicityInteractionInfoParser, periodicityInteractionId, ownerId, context.SupportPlayer.Id))
		{
			return 0;
		}
		int worldPeriodicityInteractionCount = context.SupportPlayer.GetWorldPeriodicityInteractionCount(periodicityInteractionId);
		return CheckCanInteraction(mapPeriodicityInteractionInfoParser, periodicityInteractionId, worldPeriodicityInteractionCount);
	}
public static long GetEntityOwnerId(IMiniMapMarkEntity ent, EntityOwnershipType filterType)
	{
		IMiniMapManagedEntitySource entitySourceComponent = ent?.GetComponent<IMiniMapManagedEntitySource>();
		if (entitySourceComponent?.Ownership == null || entitySourceComponent.Ownership.Type != filterType)
		{
			return 0L;
		}
		return entitySourceComponent.Ownership.OwnerId;
	}
public static string GetMapEntityReddotGraphName(MiniMapManagedContext minimap, IMiniMapMarkSupportContext context, IMiniMapMarkEntity ent, int groupId, int mapClassId, string tag)
	{
		Pos2d pos = ent.GridTransform.Pos;
		int key = minimap.MarkDataQueries.GridToArea(mapClassId, pos);
		string areaTag = minimap.MarkDataQueries.GetAreaTag(mapClassId, key);
		var mapGridGroundType = context.SupportQueries.GetGroundType(mapClassId, pos);
		MiniMapReddotDisplayInfo entityMapDisplayInfo = context.SupportQueries.GetEntityMapDisplay(ent.ClassId, groupId, mapClassId, areaTag, mapGridGroundType, tag);
		if (!string.IsNullOrEmpty(entityMapDisplayInfo?.ReddotGraphName))
		{
			return entityMapDisplayInfo.ReddotGraphName;
		}
		return context.SupportQueries.GetEntityExtendReddotGraphName(ent.ClassId);
	}
public static string FullName(GameObject value){var name=value.name;var parent=value.transform.parent;while(parent!=null){name=parent.name+"/"+name;parent=parent.parent;}return name;}

}

}

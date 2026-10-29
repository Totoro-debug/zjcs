using System;
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public static class MiniMapMarkDataUtils
{
public static IMiniMapMarkDataQueries RequiredQueries(IServiceProvider provider)=>(IMiniMapMarkDataQueries)provider.GetService(typeof(IMiniMapMarkDataQueries))??throw new InvalidOperationException("Original minimap entity queries are unbound.");
public static string GetMapEntityNameKey(IMiniMapMarkDataQueries queries, IMiniMapMarkEntity ent, int mapClassId)
	{
		int area = queries.GridToArea(mapClassId, ent.GridTransform.Pos);
		return GetMapEntityNameKey(queries, ent.ClassId, mapClassId, area, ent.GetEntityTag());
	}
public static string GetMapEntityNameKey(IMiniMapMarkDataQueries queries, int entClassId, int mapClassId, int area, string tag)
	{
		MiniMapMarkMapInfo mapInfo = queries.GetMapInfo(mapClassId);
		EntityMapUIDisplayInfo entityMapUIDisplayInfo = queries.GetEntityMapUIDisplay(entClassId, mapInfo.MapGroupId, mapClassId, queries.GetAreaTag(mapClassId,area), tag);
		if (!string.IsNullOrEmpty(entityMapUIDisplayInfo?.EntityNameKey))
		{
			return entityMapUIDisplayInfo.EntityNameKey;
		}
		return $"entity_{entClassId}_name";
	}
public static string GetMapEntityDescKey(IMiniMapMarkDataQueries queries, IMiniMapMarkEntity ent, int mapClassId)
	{
		int area = queries.GridToArea(mapClassId, ent.GridTransform.Pos);
		return GetMapEntityDescKey(queries, ent.ClassId, mapClassId, area, ent.GetEntityTag());
	}
public static string GetMapEntityDescKey(IMiniMapMarkDataQueries queries, int entClassId, int mapClassId, int area, string tag)
	{
		MiniMapMarkMapInfo mapInfo = queries.GetMapInfo(mapClassId);
		EntityMapUIDisplayInfo entityMapUIDisplayInfo = queries.GetEntityMapUIDisplay(entClassId, mapInfo.MapGroupId, mapClassId, queries.GetAreaTag(mapClassId,area), tag);
		if (!string.IsNullOrEmpty(entityMapUIDisplayInfo?.EntityDescKey))
		{
			return entityMapUIDisplayInfo.EntityDescKey;
		}
		return $"entity_{entClassId}_desc";
	}
public static MiniMapMarkTextKeyArg GetEcEntityDescTextKeyArg(IMiniMapMarkDataQueries queries, int mapClassId, IMiniMapMarkEntity ecEntity)
	{
		string mapEntityDescKey = GetMapEntityDescKey(queries, ecEntity, mapClassId);
		object[] entityDescParams = EntityDescParamsUtils.GetEntityDescParams(queries, mapClassId, ecEntity);
		return new MiniMapMarkTextKeyArg(mapEntityDescKey, entityDescParams);
	}
public static MiniMapMarkTextKeyArg GetLoEntityDescTextKeyArg(IMiniMapMarkDataQueries queries, int mapClassId, int loEntId)
	{
		MiniMapMarkLoEntityInfo containerEntityInfo = queries.GetContainerEntityInfo(mapClassId, loEntId);
		string entityTag = queries.GetEntityTag(mapClassId, loEntId);
		string mapEntityDescKey = GetMapEntityDescKey(queries, containerEntityInfo.DefSpawnEntityInfo.ClassId, mapClassId, containerEntityInfo.PosInfo.Area, entityTag);
		object[] loEntityDescParams = LoEntityDescParamsUtils.GetLoEntityDescParams(queries, mapClassId, containerEntityInfo);
		return new MiniMapMarkTextKeyArg(mapEntityDescKey, loEntityDescParams);
	}
}

}

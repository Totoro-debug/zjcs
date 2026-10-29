using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public class MiniMapEntityMark : MiniMapMarkBase
{
	protected IMiniMapMarkDataQueries Queries { get; }


	public override MiniMapMarkDataType DataType => MiniMapMarkDataType.Entity;

	public override bool EntityActive
	{
		get
		{
			if (StageComponent == null || EntityExtendInfo == null)
			{
				return false;
			}
			return StageComponent.StateEqual(EntityExtendInfo.CompleteState);
		}
	}

	public override List<Pos2d> BodyRange => Entity.GridTransform.GetBodyRange();

	public IMiniMapMarkEntity Entity { get; private set; }

	public EntityMapUIDisplayInfo EntityMapUIDisplayInfo { get; private set; }

	public MiniMapMarkExtendInfo EntityExtendInfo { get; private set; }

	public IMiniMapMarkStage StageComponent { get; private set; }

	public MiniMapEntityMark(int mapClassId, int region, IMiniMapMarkEntity entity, IMiniMapMarkDataQueries queries)
	{
		Queries = queries;
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		MapClassId = mapClassId;
		Region = region;
		Entity = entity;
		StageComponent = Entity.GetComponent<IMiniMapMarkStage>();
		string areaTag = Queries.GetAreaTag(MapClassId, Queries.GridToArea(MapClassId, entity.GridTransform.Pos));
		MiniMapMarkMapInfo mapInfo = Queries.GetMapInfo(mapClassId);
		EntityMapUIDisplayInfo = Queries.GetEntityMapUIDisplay(Entity.ClassId, mapInfo.MapGroupId, MapClassId, areaTag, entity.GetEntityTag());
		EntityExtendInfo = Queries.GetEntityExtend(Entity.ClassId);
		if (EntityExtendInfo == null)
		{
			UnityEngine.Debug.LogError(string.Concat(new object[] { "entity_extend_parameter.csv not exist EntityClassId[", Entity.ClassId, "], MiniMapMarkDataType:", GetType().FullName }));
		}
		IMiniMapMarkGrid gridTransform = entity.GridTransform;
		if (gridTransform != null)
		{
			GridPos = gridTransform.Pos;
			LocalPosition = GetEntityMarkPos(gridTransform);
			return;
		}
		UnityEngine.Debug.LogError(string.Concat(new object[] { "This entity not exist MyGridTransform but need show in MiniMap, EntityClassId:", entity.ClassId, ", MiniMapMarkDataType:", GetType().FullName }));
	}

	public override int GetEntityState()
	{
		return StageComponent?.State ?? 0;
	}

	public override string GetMarkNameKey()
	{
		return MiniMapMarkDataUtils.GetMapEntityNameKey(Queries, Entity, Entity.ContainerClassId);
	}

	public override MiniMapMarkTextKeyArg GetMarkDescTextArgKey()
	{
		return MiniMapMarkDataUtils.GetEcEntityDescTextKeyArg(Queries, MapClassId, Entity);
	}

	public override string GetDialogIconPrefabPath()
	{
		if (!string.IsNullOrEmpty(EntityMapUIDisplayInfo?.DialogIconPrefab))
		{
			return EntityMapUIDisplayInfo?.DialogIconPrefab;
		}
		if (!string.IsNullOrEmpty(EntityExtendInfo?.DialogIconPrefab))
		{
			return EntityExtendInfo?.DialogIconPrefab;
		}
		return null;
	}

	public override void InitDialogIconHandler(IMapEntityDialogIconHandler[] handlers)
	{
		if (handlers != null && handlers.Length != 0)
		{
			for (int i = 0; i < handlers.Length; i++)
			{
				handlers[i].Init(Entity.Original);
			}
		}
	}

	public override bool InteractPossible()
	{
		return StageComponent.IsActive();
	}

	public override void OnClick()
	{
	}

	protected Vector3 GetEntityMarkPos(IMiniMapMarkGrid gridTsf)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		if (gridTsf.Info.BodyRangeInfos.Count == 0)
		{
			throw new Exception($"Entity BodyRangeInfos is empty, EntityClassId:{gridTsf.Entity.ClassId}");
		}
		Vector3 zero = Vector3.zero;
		foreach (GridTransformComponentBodyPosInfo bodyRangeInfo in gridTsf.Info.BodyRangeInfos)
		{
			zero.x += bodyRangeInfo.Pos.X;
			zero.y += bodyRangeInfo.Pos.Y;
		}
		zero /= (float)gridTsf.Info.BodyRangeInfos.Count;
		zero.x -= gridTsf.Info.RealCenter.x;
		zero.y -= gridTsf.Info.RealCenter.y;
		zero *= 1f;
		return gridTsf.GridToPosition(gridTsf.Pos) + zero;
	}

	public static MiniMapEntityMark Create(IServiceProvider serviceProvider, MiniMapMarkType type, int mapClassId, int region, IMiniMapMarkEntity entity)
	{
		return type switch
		{
			MiniMapMarkType.Entry => new MiniMapEntityEntryMark(mapClassId, region, entity, MiniMapMarkDataUtils.RequiredQueries(serviceProvider)), 
			MiniMapMarkType.WayPoint => new MiniMapEntityWayPointMark(mapClassId, region, entity, MiniMapMarkDataUtils.RequiredQueries(serviceProvider)), 
			_ => new MiniMapEntityMark(mapClassId, region, entity, MiniMapMarkDataUtils.RequiredQueries(serviceProvider)), 
		};
	}
}

}

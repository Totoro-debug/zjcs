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
public class MiniMapOfflineMark : MiniMapMarkBase
{
	protected IMiniMapMarkDataQueries Queries { get; }

	public int LoEntityId;

	public int EntityClassId;

	public int State;

	private List<Pos2d> mBodyRange;


	public override MiniMapMarkDataType DataType => MiniMapMarkDataType.Offline;

	public override bool EntityActive
	{
		get
		{
			if (EntityExtendInfo == null)
			{
				return false;
			}
			return State == EntityExtendInfo.CompleteState;
		}
	}

	public override List<Pos2d> BodyRange => mBodyRange;

	public EntityMapUIDisplayInfo EntityMapUIDisplayInfo { get; private set; }

	public MiniMapMarkExtendInfo EntityExtendInfo { get; private set; }

	public MiniMapMarkLoEntityInfo EntityWrapInfo { get; private set; }

	public MiniMapOfflineMark(int mapClassId, int loEntId, int entClassId, int state, IMiniMapMarkDataQueries queries)
	{
		Queries = queries;
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		MapClassId = mapClassId;
		LoEntityId = loEntId;
		EntityClassId = entClassId;
		State = state;
		EntityWrapInfo = Queries.GetContainerEntityInfo(MapClassId, LoEntityId);
		if (EntityWrapInfo == null)
		{
			UnityEngine.Debug.LogError(string.Concat(new object[] { "Map not exist this entity, MapClassId:", MapClassId, ", LoEntityId:", LoEntityId, ", MiniMapMarkDataType:", GetType().FullName }));
		}
		string areaTag = Queries.GetAreaTag(MapClassId, EntityWrapInfo.PosInfo.Area);
		MiniMapMarkMapInfo mapInfo = Queries.GetMapInfo(mapClassId);
		string entityTag = Queries.GetEntityTag(mapClassId, LoEntityId);
		EntityMapUIDisplayInfo = Queries.GetEntityMapUIDisplay(EntityClassId, mapInfo.MapGroupId, MapClassId, areaTag, entityTag);
		EntityExtendInfo = Queries.GetEntityExtend(EntityClassId);
		if (EntityExtendInfo == null)
		{
			UnityEngine.Debug.LogError(string.Concat(new object[] { "entity_extend_parameter.csv not exist EntityClassId[", EntityClassId, "], MiniMapMarkDataType:", GetType().FullName }));
		}
		IMiniMapMarkEntity commonPrefab = Queries.GetCommonPrefab(EntityWrapInfo.DefSpawnEntityInfo.ClassId);
		if (commonPrefab == null)
		{
			UnityEngine.Debug.LogError(string.Concat(new object[] { "Map not exist this entity, MapClassId:", MapClassId, ", LoEntityId:", LoEntityId, ", MiniMapMarkDataType:", GetType().FullName }));
		}
		Region = EntityWrapInfo.PosInfo.Region;
		IMiniMapMarkGrid gridTransform = commonPrefab.GridTransform;
		if (gridTransform != null)
		{
			GridPos = EntityWrapInfo.PosInfo.Pos;
			LocalPosition = GetEntityMarkPos(gridTransform, GridPos);
			mBodyRange = gridTransform.GetBodyRange();
			return;
		}
		UnityEngine.Debug.LogError(string.Concat(new object[] { "This entity not exist MyGridTransform but need show in MiniMap, MapClassId:", MapClassId, ", LoEntityId:", LoEntityId, ", EntityClassId:", EntityWrapInfo.DefSpawnEntityInfo.ClassId, ", MiniMapMarkDataType:", GetType().FullName }));
	}

	public override int GetEntityState()
	{
		return State;
	}

	public override string GetMarkNameKey()
	{
		int area = Queries.GridToArea(MapClassId, GridPos);
		string entityTag = Queries.GetEntityTag(MapClassId, LoEntityId);
		return MiniMapMarkDataUtils.GetMapEntityNameKey(Queries, EntityClassId, MapClassId, area, entityTag);
	}

	public override MiniMapMarkTextKeyArg GetMarkDescTextArgKey()
	{
		return MiniMapMarkDataUtils.GetLoEntityDescTextKeyArg(Queries, MapClassId, LoEntityId);
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
				handlers[i].Init(new MapEntityDialogIconLiteEntityData
				{
					MapClassId = MapClassId,
					LoEntityId = LoEntityId,
					EntityClassId = EntityClassId,
					State = State
				});
			}
		}
	}

	public override bool InteractPossible()
	{
		return EntityExtendInfo.CompleteState == State;
	}

	public override void OnClick()
	{
	}

	protected Vector3 GetEntityMarkPos(IMiniMapMarkGrid gridTsf, Pos2d pos)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
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
		Vector2 val = gridTsf.Info.RealCenter * 1f;
		return Queries.MapGridToPos(MapClassId, pos) + new Vector3(val.x, val.y, 0f) + zero;
	}

	public static MiniMapOfflineMark Create(IServiceProvider serviceProvider, MiniMapMarkType type, int mapClassId, int loEntId, int entClassId, int state)
	{
		return type switch
		{
			MiniMapMarkType.Entry => new MiniMapOfflineEntryMark(mapClassId, loEntId, entClassId, state, MiniMapMarkDataUtils.RequiredQueries(serviceProvider)), 
			MiniMapMarkType.WayPoint => new MiniMapOfflineWayPointMark(mapClassId, loEntId, entClassId, state, MiniMapMarkDataUtils.RequiredQueries(serviceProvider)), 
			_ => new MiniMapOfflineMark(mapClassId, loEntId, entClassId, state, MiniMapMarkDataUtils.RequiredQueries(serviceProvider)), 
		};
	}
}

}

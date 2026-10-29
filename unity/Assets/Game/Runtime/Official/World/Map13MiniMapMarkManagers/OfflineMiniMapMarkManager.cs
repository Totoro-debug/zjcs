using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public class OfflineMiniMapMarkManager : MonoBehaviour, IMiniMapMark, IMiniMapMarkSpawnSupporter
{
	public Zjcs.Official.World.NativeMap13Labels.GAction<MiniMapMarkUIBase> OnOfflineMapMarkAdded = new Zjcs.Official.World.NativeMap13Labels.GAction<MiniMapMarkUIBase>();

	public MiniMapManagedContext MiniMap;
	private MiniMapManagedContext MarkContext => MiniMap;

	public Transform EntitySlot;

	private Dictionary<Pos2d, MiniMapMarkUIBase> mPosMarkUiDict = new Dictionary<Pos2d, MiniMapMarkUIBase>();

	public IServiceProvider ServiceProvider { get; set; }

	public void Init(int mapClassId, PlayerOfflineMiniMapDataResponse data)
	{
		IMiniMapMarkDataQueries entityExtendParameterInfoParser = MarkContext.MarkDataQueries;
		foreach (KeyValuePair<int, PlayerOfflineMiniMapLiteEntityData> item in data.LiteEntityDataDict)
		{
			MiniMapMarkLoEntityInfo containerEntityInfo = MarkContext.MarkDataQueries.GetContainerEntityInfo(mapClassId, item.Key);
			bool num = data.ActiveRegions != null && data.ActiveRegions.Contains(containerEntityInfo.PosInfo.Region);
			MiniMapMarkExtendInfo entityExtendParameterInfo = entityExtendParameterInfoParser.GetEntityExtend(containerEntityInfo.DefSpawnEntityInfo.ClassId);
			if (num || (item.Value.State != entityExtendParameterInfo.CompleteState && item.Value.State >= 0) || !entityExtendParameterInfo.MiniMapMarkHideInEnd)
			{
				CreateEntityIconObject(mapClassId, item.Key, item.Value.EntityClassId, item.Value.State, containerEntityInfo.PosInfo.Area);
			}
		}
		foreach (int activeRegion in data.ActiveRegions)
		{
			foreach (MiniMapMarkLoEntityInfo item2 in MarkContext.ManagedQueries.GetEntityByMapRegion(mapClassId, activeRegion))
			{
				MiniMapMarkExtendInfo entityExtendParameterInfo2 = entityExtendParameterInfoParser.GetEntityExtend(item2.DefSpawnEntityInfo.ClassId);
				if (entityExtendParameterInfo2.MiniMapMarkShowInPublic && entityExtendParameterInfo2.MiniMapMarkShowInOffline)
				{
					CreateEntityIconObject(mapClassId, item2.LoEntityId, item2.DefSpawnEntityInfo.ClassId, entityExtendParameterInfo2.CompleteState, item2.PosInfo.Area);
				}
			}
		}
	}

	private void OnDestroy()
	{
		mPosMarkUiDict.Clear();
	}

	void IMiniMapMarkSpawnSupporter.GetAllMarkUI(List<MiniMapMarkUIBase> result)
	{
		if (mPosMarkUiDict.Count <= 0)
		{
			return;
		}
		result.Clear();
		foreach (MiniMapMarkUIBase value in mPosMarkUiDict.Values)
		{
			result.Add(value);
		}
	}

	public bool TryGetMark(Pos2d pos, out MiniMapMarkUIBase data)
	{
		return mPosMarkUiDict.TryGetValue(pos, out data);
	}

	protected void CreateEntityIconObject(int mapClassId, int loEntId, int entClassId, int state, int area)
	{
		MiniMapMarkMapInfo mapInfo = MarkContext.MarkDataQueries.GetMapInfo(mapClassId);
		string entityTag = MarkContext.MarkDataQueries.GetEntityTag(mapClassId, loEntId);
		string markPath = MiniMapManagedRules.GetMapEntityMiniMapMark(MarkContext, entClassId, mapInfo.MapGroupId, mapClassId, area, entityTag);
		if (!string.IsNullOrEmpty(markPath))
		{
			MarkContext.InstantiateMarkAsync(markPath, EntitySlot, (MiniMapMarkUIBase markUI) =>
			{
				MiniMapOfflineMark mark = MiniMapOfflineMark.Create(ServiceProvider, markUI.MarkType, mapClassId, loEntId, entClassId, state);
				markUI.Init(MiniMap, mark);
				markUI.MarkAssetPath = markPath;
				RegistMark(markUI);
			});
		}
	}

	private void RegistMark(MiniMapMarkUIBase markUI)
	{
		foreach (Pos2d item in markUI.BodyRange)
		{
			mPosMarkUiDict[markUI.GridPos + item] = markUI;
		}
		OnOfflineMapMarkAdded.Invoke(markUI);
	}
}

}

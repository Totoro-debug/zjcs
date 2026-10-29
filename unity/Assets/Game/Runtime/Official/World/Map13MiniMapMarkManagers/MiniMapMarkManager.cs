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
[RequireComponent(typeof(MiniMapMarkSpawnManager))]
public abstract class MiniMapMarkManager : MiniMapManagerBase, IEcContainerAddEntityEventReceiver, IEventReceiver, IEcContainerRemoveEntityEventReceiver, IEcContainerReplaceEntityEventReceiver
{
	private MiniMapMarkSpawnManager mSpawnManager;

	protected int mapClassId;

	protected IMiniMapManagedAreaAgent mAreaAgent;

	protected MiniMapManagedMapPartInfo mMapInfo;

	private bool mInited;

	private bool mStatusSupport;

	private List<EcEntityIdentifier> mCacheEntIds = new List<EcEntityIdentifier>();

	private MiniMapManagedContext MarkContext => (MiniMapManagedContext)MiniMap;
	private MiniMapManagedContext MapRootManagerProxy => MarkContext;

	protected abstract MapRegionStatus HandleStatus { get; }

	public override bool MapActived
	{
		get
		{
			if (mStatusSupport)
			{
				return base.MapActived;
			}
			return false;
		}
	}

	private void Awake()
	{
		mSpawnManager = ((Component)this).GetComponent<MiniMapMarkSpawnManager>();
		int num = MiniMap.MapClassId;
		MiniMapManagedMapPartInfo mapPartInfo = MarkContext.ManagedQueries.GetMapPartInfo(num);
		mStatusSupport = mapPartInfo.RegionStatusSupported(HandleStatus);
	}

	protected override void OnDestroy()
	{
		mapClassId = 0;
		mMapInfo = null;
		mAreaAgent = null;
		RemoveListener();
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (MapActived)
		{
			TryInit();
		}
	}

	private void TryInit()
	{
		if (mInited)
		{
			return;
		}
		mInited = true;
		mapClassId = MapRootManagerProxy.CurrentMapClassId;
		mMapInfo = MarkContext.ManagedQueries.GetMapPartInfo(mapClassId);
		mAreaAgent = MapRootManagerProxy.ManagedAreaAgent;
		mSpawnManager.Init(MapRootManagerProxy.ManagedGridGround);
		List<MapRegionStatus> currentMapStatus = MarkContext.ManagedQueries.GetCurrentMapStatus();
		if (currentMapStatus != null)
		{
			for (int i = 0; i < currentMapStatus.Count; i++)
			{
				RefreshRegion(i, currentMapStatus[i]);
			}
		}
		EcEntityIdentifier ecEntityIdentifier = default;
		MiniMapMarkUIBase miniMapMarkUIBase = default;
		foreach (KeyValuePair<EcEntityIdentifier, MiniMapMarkUIBase> item in mSpawnManager.MarkUiDict)
		{
			ecEntityIdentifier = item.Key;
			miniMapMarkUIBase = item.Value;
			EcEntityIdentifier entId = ecEntityIdentifier;
			MiniMapMarkUIBase markUI = miniMapMarkUIBase;
			OnUiMarkAdded(markUI, entId);
		}
		AddListener();
	}

	protected virtual void RefreshRegion(int region, MapRegionStatus status)
	{
		if (MapRegionStatusFilter(status))
		{
			return;
		}
		mCacheEntIds.Clear();
		if (mSpawnManager.RegionEntityDict.TryGetValue(region, out var value))
		{
			mCacheEntIds.AddRange(value);
			foreach (EcEntityIdentifier mCacheEntId in mCacheEntIds)
			{
				DestroyMark(mCacheEntId);
			}
		}
		foreach (int area in MarkContext.GetRegion(mapClassId, region).Areas)
		{
			Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity> areaEntity = mAreaAgent.GetAreaEntity(area);
			if (areaEntity == null)
			{
				continue;
			}
			foreach (KeyValuePair<EcEntityIdentifier, IMiniMapSpawnEntity> item in areaEntity)
			{
				if (!MapRegionStatusFilter(item.Value.Id.ContainerId) && !mSpawnManager.MarkUiDict.ContainsKey(item.Key))
				{
					CreateMark(region, item.Value);
				}
			}
		}
	}

	protected virtual void CreateMark(int region, IMiniMapSpawnEntity ent, MiniMapMarkExtendInfo extendInfo = null)
	{
		if (extendInfo == null)
		{
			extendInfo = MarkContext.MarkDataQueries.GetEntityExtend(ent.ClassId);
		}
		MiniMapMarkMapInfo mapInfo = MarkContext.MarkDataQueries.GetMapInfo(mapClassId);
		string mapEntityMiniMapMark = MarkContext.GetMapEntityMiniMapMark(ent, mapInfo.MapGroupId, mapClassId, ent.GetEntityTag());
		if (!string.IsNullOrEmpty(mapEntityMiniMapMark))
		{
			IMiniMapManagedStage component = ent.GetComponent<IMiniMapManagedStage>();
			if (CreateEntityIconCheck(region, component, extendInfo))
			{
				mSpawnManager.CreateMark(mapEntityMiniMapMark, region, ent);
			}
		}
	}

	protected void DestroyMark(EcEntityIdentifier entId)
	{
		mSpawnManager.DestroyMark(entId);
	}

	protected abstract bool CreateEntityIconCheck(int region, IMiniMapManagedStage worldStage, MiniMapMarkExtendInfo info);

	private bool MapRegionStatusFilter(long containerId)
	{
		if (!MarkContext.ManagedQueries.TryGetStatusByContainerId(containerId, out var status))
		{
			return true;
		}
		return MapRegionStatusFilter(status);
	}

	private bool MapRegionStatusFilter(MapRegionStatus status)
	{
		if (mMapInfo.RegionStatusOnlySupport(HandleStatus))
		{
			return false;
		}
		return status != HandleStatus;
	}

	private void OnUiMarkAdded(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
		if (mInited && !MapRegionStatusFilter(entId.ContainerId))
		{
			RegistMark(markUI, entId);
		}
	}

	private void OnUiMarkRemoved(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
		if (mInited)
		{
			UnregistMark(markUI, entId);
		}
	}

	void IEcContainerAddEntityEventReceiver.Container_OnEntityAdd(IMiniMapSpawnEntity ent)
	{
		if (ent.ClassId > 0 && ent.GridTransform != null && ent.GetComponent<IMiniMapManagedGridMover>() == null && !MapRegionStatusFilter(ent.Id.ContainerId))
		{
			int region = mAreaAgent.GridToRegion(mapClassId, ent.GridTransform.Pos);
			CreateMark(region, ent);
		}
	}

	void IEcContainerRemoveEntityEventReceiver.Container_OnEntityRemove(IMiniMapSpawnEntity entity, object data)
	{
		DestroyMark(entity.Id);
	}

	public virtual void Container_OnEntityReplace(IMiniMapSpawnEntity entity, EcEntityIdentifier preEntId)
	{
		mSpawnManager.ReplaceEntity(entity, preEntId);
	}

	protected void OnMapVisibilityUpdate(int mapClassId, int region, MapRegionStatus preVisibility, MapRegionStatus visibility)
	{
		RefreshRegion(region, visibility);
	}

	protected virtual void AddListener()
	{
		MarkContext.ManagedEvents.AddEntityEvent += (IEcContainerAddEntityEventReceiver)this;
		MarkContext.ManagedEvents.RemoveEntityEvent += (IEcContainerRemoveEntityEventReceiver)this;
		MarkContext.ManagedEvents.ReplaceEntityEvent += (IEcContainerReplaceEntityEventReceiver)this;
		MarkContext.ManagedEvents.OnMapStatusUpdate += OnMapVisibilityUpdate;
		MiniMapMarkSpawnManager miniMapMarkSpawnManager = mSpawnManager;
		miniMapMarkSpawnManager.OnUiMarkAdded = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Combine(miniMapMarkSpawnManager.OnUiMarkAdded, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkAdded));
		MiniMapMarkSpawnManager miniMapMarkSpawnManager2 = mSpawnManager;
		miniMapMarkSpawnManager2.OnUiMarkRemoved = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Combine(miniMapMarkSpawnManager2.OnUiMarkRemoved, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkRemoved));
	}

	protected virtual void RemoveListener()
	{
		MiniMapMarkSpawnManager miniMapMarkSpawnManager = mSpawnManager;
		miniMapMarkSpawnManager.OnUiMarkAdded = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Remove(miniMapMarkSpawnManager.OnUiMarkAdded, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkAdded));
		MiniMapMarkSpawnManager miniMapMarkSpawnManager2 = mSpawnManager;
		miniMapMarkSpawnManager2.OnUiMarkRemoved = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Remove(miniMapMarkSpawnManager2.OnUiMarkRemoved, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkRemoved));
		MarkContext.ManagedEvents.AddEntityEvent -= (IEcContainerAddEntityEventReceiver)this;
		MarkContext.ManagedEvents.RemoveEntityEvent -= (IEcContainerRemoveEntityEventReceiver)this;
		MarkContext.ManagedEvents.ReplaceEntityEvent -= (IEcContainerReplaceEntityEventReceiver)this;
		if (MarkContext.HasMainPlayer)
		{
			MarkContext.ManagedEvents.OnMapStatusUpdate -= OnMapVisibilityUpdate;
		}
	}

	protected abstract void RegistMark(MiniMapMarkUIBase markUI, EcEntityIdentifier entId);

	protected abstract void UnregistMark(MiniMapMarkUIBase markUI, EcEntityIdentifier entId);
}

}

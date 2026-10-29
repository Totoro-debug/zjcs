using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
public class MiniMapMarkSpawnManager : MapUIBase, IMiniMapMark, IFullScrMiniMapStateListener, IMiniMapMarkSpawnSupporter
{
	private struct SpawnMarkCacheData
	{
		public string MarkPath { get; set; }

		public int Region { get; set; }

		public IMiniMapSpawnEntity Entity { get; set; }
	}

	private const float kClearPoolDelaySecond = 10f;

	public Action<MiniMapMarkUIBase, EcEntityIdentifier> OnUiMarkAdded;

	public Action<MiniMapMarkUIBase, EcEntityIdentifier> OnUiMarkRemoved;

	[SerializeField]
	private MiniMapSpawnContext mMinimap;

	private Dictionary<EcEntityIdentifier, MiniMapMarkUIBase> mMarkUiDict = new Dictionary<EcEntityIdentifier, MiniMapMarkUIBase>();

	private Dictionary<int, List<EcEntityIdentifier>> mRegionEntityDict = new Dictionary<int, List<EcEntityIdentifier>>();

	private Dictionary<EcEntityIdentifier, SpawnMarkCacheData> mNeedSpawnMarkDataDict = new Dictionary<EcEntityIdentifier, SpawnMarkCacheData>();

	private Dictionary<EcEntityIdentifier, SpawnMarkCacheData> mCacheSpawnMarkDataDict = new Dictionary<EcEntityIdentifier, SpawnMarkCacheData>();

	private HashSet<EcEntityIdentifier> mCacheEntityIdSet = new HashSet<EcEntityIdentifier>();

	private List<EcEntityIdentifier> mCacheEntityIds = new List<EcEntityIdentifier>();

	private HashSet<EcEntityIdentifier> mResidentMarkSet = new HashSet<EcEntityIdentifier>();

	private IMiniMapMarkGridGround mGridGround;

	private MiniMapMarkPoolHandler mPoolHandler;

	private bool mInited;

	public IServiceProvider ServiceProvider { get; set; }

	public IMiniMapMarkPerformance PerformanceMiniMapMarkProxy { get; set; }

	public IReadOnlyDictionary<EcEntityIdentifier, MiniMapMarkUIBase> MarkUiDict => mMarkUiDict;

	public IReadOnlyDictionary<int, List<EcEntityIdentifier>> RegionEntityDict => mRegionEntityDict;

	public void Init(IMiniMapMarkGridGround gridGround)
	{
		if (!mInited)
		{
			mInited = true;
			mGridGround = gridGround;
			mPoolHandler = new MiniMapMarkPoolHandler(((Component)this).transform, mMinimap);
			mMinimap.OnVisibleRangeUpdate += OnVisibleRangeUpdate;
			OnVisibleRangeUpdate(mMinimap.ViewRangeHandler);
			PerformanceMiniMapMarkProxy.RegisterSpawnManager(this);
		}
	}

	protected override void OnDestroy()
	{
		PerformanceMiniMapMarkProxy.UnregisterSpawnManager(this);
		mMinimap.OnVisibleRangeUpdate -= OnVisibleRangeUpdate;
		EcEntityIdentifier[] array = mMarkUiDict.Keys.ToArray();
		foreach (EcEntityIdentifier entId in array)
		{
			DoDestroyMark(entId);
		}
		mMarkUiDict.Clear();
		mNeedSpawnMarkDataDict.Clear();
		mResidentMarkSet.Clear();
		mPoolHandler?.Release();
		mGridGround = null;
		base.OnDestroy();
	}

	public void CreateMark(string markPath, int region, IMiniMapSpawnEntity ent)
	{
		if (!mNeedSpawnMarkDataDict.TryGetValue(ent.Id, out var value))
		{
			value = new SpawnMarkCacheData
			{
				MarkPath = markPath,
				Region = region,
				Entity = ent
			};
			mNeedSpawnMarkDataDict[ent.Id] = value;
		}
		if (mMinimap.IsEntityInViewRange(ent))
		{
			DoCreateMark(value);
		}
	}

	public void DestroyMark(EcEntityIdentifier entId)
	{
		mNeedSpawnMarkDataDict.Remove(entId);
		DoDestroyMark(entId);
	}

	public void CreateResidentMark(IMiniMapSpawnEntity ent)
	{
		if (mInited && ent != null && ent.GridTransform != null && !mMarkUiDict.ContainsKey(ent.Id))
		{
			MiniMapMarkMapInfo mapInfo = mMinimap.MarkDataQueries.GetMapInfo(mMinimap.MapClassId);
			string mapEntityMiniMapMark = mMinimap.GetMapEntityMiniMapMark(ent, mapInfo.MapGroupId, mMinimap.MapClassId, ent.GetEntityTag());
			if (!string.IsNullOrEmpty(mapEntityMiniMapMark))
			{
				SpawnMarkCacheData data = new SpawnMarkCacheData
				{
					MarkPath = mapEntityMiniMapMark,
					Region = 0,
					Entity = ent
				};
				DoCreateMark(data, resident: true);
			}
		}
	}

	public void RemoveResidentMark(EcEntityIdentifier entId)
	{
		if (mResidentMarkSet.Remove(entId))
		{
			DoDestroyMark(entId);
		}
	}

	public void SetResidentMarkTipDot(EcEntityIdentifier entId, ETipDotLevelType level)
	{
		if (mResidentMarkSet.Contains(entId) && mMarkUiDict.TryGetValue(entId, out var value))
		{
			MiniMapMarkTipDotLink componentInChildren = ((Component)value).GetComponentInChildren<MiniMapMarkTipDotLink>(true);
			if ((Object)(object)componentInChildren != (Object)null && componentInChildren.TipLevelState != null)
			{
				componentInChildren.TipLevelState.SetState(level);
			}
		}
	}

	public void ReplaceEntity(IMiniMapSpawnEntity entity, EcEntityIdentifier preEntId)
	{
		if (mMarkUiDict.Remove(preEntId, out MiniMapMarkUIBase miniMapMarkUIBase))
		{
			mMarkUiDict.Add(entity.Id, miniMapMarkUIBase);
			if (mRegionEntityDict.TryGetValue(miniMapMarkUIBase.Region, out var value))
			{
				value.Remove(preEntId);
				value.Add(entity.Id);
			}
		}
		if (mNeedSpawnMarkDataDict.Remove(preEntId, out SpawnMarkCacheData value2))
		{
			mNeedSpawnMarkDataDict.Add(value2.Entity.Id, value2);
		}
	}

	public bool TryGetMark(Pos2d pos, out MiniMapMarkUIBase markUi)
	{
		markUi = null;
		IMiniMapMarkCell gamePosData = mGridGround?.GetPosData(pos);
		if (gamePosData == null || !gamePosData.ExistEntitys())
		{
			return false;
		}
		foreach (IMiniMapSpawnEntity entity in gamePosData.Entitys)
		{
			if (mMarkUiDict.TryGetValue(entity.Id, out markUi))
			{
				return true;
			}
		}
		return false;
	}

	void IMiniMapMarkSpawnSupporter.GetAllMarkUI(List<MiniMapMarkUIBase> result)
	{
		if (mMarkUiDict.Count <= 0)
		{
			return;
		}
		result.Clear();
		foreach (MiniMapMarkUIBase value in mMarkUiDict.Values)
		{
			result.Add(value);
		}
	}

	void IFullScrMiniMapStateListener.OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		if (mPoolHandler != null)
		{
			mPoolHandler.StopClearPool();
		}
	}

	void IFullScrMiniMapStateListener.OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		if (mPoolHandler != null)
		{
			mPoolHandler.StopClearPool();
			mPoolHandler.DelayClearPool(10f);
		}
	}

	private void DoCreateMark(SpawnMarkCacheData data, bool resident = false)
	{
		IMiniMapSpawnEntity entity = data.Entity;
		MiniMapMarkUIBase miniMapMarkUIBase = mPoolHandler.Borrow(entity.ClassId, data.MarkPath, ((Component)this).transform);
		if ((Object)(object)miniMapMarkUIBase == (Object)null)
		{
			return;
		}
		MiniMapEntityMark mark = MiniMapEntityMark.Create(ServiceProvider, miniMapMarkUIBase.MarkType, mMinimap.MapClassId, data.Region, entity);
		miniMapMarkUIBase.Init(mMinimap, mark);
		mMarkUiDict.Add(entity.Id, miniMapMarkUIBase);
		if (resident)
		{
			mResidentMarkSet.Add(entity.Id);
		}
		else
		{
			if (!mRegionEntityDict.TryGetValue(data.Region, out var value))
			{
				value = (mRegionEntityDict[data.Region] = new List<EcEntityIdentifier>());
			}
			value.Add(entity.Id);
		}
		OnUiMarkAdded?.Invoke(miniMapMarkUIBase, entity.Id);
	}

	private void DoDestroyMark(EcEntityIdentifier entId)
	{
		if (mMarkUiDict.Remove(entId, out MiniMapMarkUIBase miniMapMarkUIBase))
		{
			if (mRegionEntityDict.TryGetValue(miniMapMarkUIBase.Region, out var value))
			{
				value.Remove(entId);
			}
			OnUiMarkRemoved?.Invoke(miniMapMarkUIBase, entId);
			mPoolHandler.Return(miniMapMarkUIBase);
		}
	}

	private void OnVisibleRangeUpdate(IMapViewRangeHandler handler)
	{
		if (handler.VisiblePosList == null)
		{
			VisibleRangeUpdateByAllVisible();
		}
		else
		{
			VisibleRangeUpdateByRangeVisible(handler);
		}
	}

	private void VisibleRangeUpdateByAllVisible()
	{
		EcEntityIdentifier ecEntityIdentifier = default;
		SpawnMarkCacheData spawnMarkCacheData = default;
		foreach (KeyValuePair<EcEntityIdentifier, SpawnMarkCacheData> item in mNeedSpawnMarkDataDict)
		{
			ecEntityIdentifier = item.Key;
			spawnMarkCacheData = item.Value;
			SpawnMarkCacheData data = spawnMarkCacheData;
			IMiniMapSpawnEntity entity = data.Entity;
			if (!mMarkUiDict.ContainsKey(entity.Id) && mMinimap.IsEntityInViewRange(entity))
			{
				DoCreateMark(data);
			}
		}
	}

	private void VisibleRangeUpdateByRangeVisible(IMapViewRangeHandler handler)
	{
		mCacheSpawnMarkDataDict.Clear();
		mCacheEntityIdSet.Clear();
		mCacheEntityIds.Clear();
		foreach (Pos2d visiblePos in handler.VisiblePosList)
		{
			IMiniMapMarkCell gamePosData = mGridGround?.GetPosData(visiblePos);
			if (gamePosData == null || !gamePosData.ExistEntitys())
			{
				continue;
			}
			foreach (IMiniMapSpawnEntity entity in gamePosData.Entitys)
			{
				SpawnMarkCacheData value;
				if (mMarkUiDict.ContainsKey(entity.Id))
				{
					mCacheEntityIdSet.Add(entity.Id);
				}
				else if (mNeedSpawnMarkDataDict.TryGetValue(entity.Id, out value))
				{
					mCacheEntityIdSet.Add(entity.Id);
					mCacheSpawnMarkDataDict.TryAdd(entity.Id, value);
				}
			}
		}
		foreach (KeyValuePair<EcEntityIdentifier, MiniMapMarkUIBase> item in mMarkUiDict)
		{
			if (!mCacheEntityIdSet.Contains(item.Key) && !mResidentMarkSet.Contains(item.Key))
			{
				mCacheEntityIds.Add(item.Key);
			}
		}
		foreach (EcEntityIdentifier mCacheEntityId in mCacheEntityIds)
		{
			DoDestroyMark(mCacheEntityId);
		}
		foreach (KeyValuePair<EcEntityIdentifier, SpawnMarkCacheData> item2 in mCacheSpawnMarkDataDict)
		{
			DoCreateMark(item2.Value);
		}
	}
}

}

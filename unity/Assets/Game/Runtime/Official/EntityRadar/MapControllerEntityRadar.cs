// Original client radar implementation; see entity radar source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.EntityContainers;
using MyContainer = Zjcs.Official.EntityContainers.MyContainer;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using IGameObjectPoolItem = Zjcs.Official.StoryPlayables.IGameObjectPoolItem;
namespace Zjcs.Official.EntityRadar
{

[RequireComponent(typeof(IRadarCharacterController))]
public class MapControllerEntityRadar : MapMeCharacterUIBase, IMyContainerAddEntityEventReceiver, IEventReceiver, IMyContainerRemoveEntityEventReceiver, IGridTransformPosUpdatedEventReceiver
{
	private MyContainer mMapContainer;

	private IRadarCharacterController mMapController;

	private ECGridTransformData mGridTransformComponent;

	private int mRadarCheckDis = 1;

	private readonly List<Pos2d> mAroundPosOffsetList = new List<Pos2d>();

	private bool inited;

	private Action<ECWorldStageStateData, int, object> mOnStageStateUpdate;

	private HashSet<EcEntityIdentifier> mRangeEntityIdList = new HashSet<EcEntityIdentifier>();

	private HashSet<EcEntityIdentifier> mPreRangeEntityIdList = new HashSet<EcEntityIdentifier>();

	protected readonly Dictionary<MyEntity, MapControllerEntityRadarTipBase> mRadarTipDict = new Dictionary<MyEntity, MapControllerEntityRadarTipBase>();

	private Action<MapControllerEntityRadarTipBase> mOnTipHiden;

		public IWorldInputRootLookup MapRootManagerProxy { get; set; }

		public IRadarWorldQueries MapInput { get; set; }

	private void Awake()
	{
		mMapController = ((Component)this).GetComponent<IRadarCharacterController>();
		mOnStageStateUpdate = OnStageStateUpdate;
		mOnTipHiden = OnTipHiden;
	}

	private void Init()
	{
		mRadarCheckDis = MapInput.MapStageCostShowDistance;
		for (int i = -mRadarCheckDis; i <= mRadarCheckDis; i++)
		{
			for (int j = -mRadarCheckDis; j <= mRadarCheckDis; j++)
			{
				Pos2d item = new Pos2d(i, j);
				int num = item.Magnitude();
				if (num != 0 && num <= mRadarCheckDis)
				{
					mAroundPosOffsetList.Add(item);
				}
			}
		}
		inited = true;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (!(mMapController.MyEntity.Id != myMapController.MyEntity.Id))
		{
			if (!inited)
			{
				Init();
			}
			mMapContainer = (MyContainer)mMapController.MyEntity.myContainer;
			mMapContainer.EntityAddEvent += (IMyContainerAddEntityEventReceiver)this;
			mMapContainer.EntityRemoveEvent += (IMyContainerRemoveEntityEventReceiver)this;
			mGridTransformComponent = mMapController.MyEntity.Entity.GetComponent<ECGridTransformData>();
			mGridTransformComponent.AddPosUpdatedReceiver(this);
			RefreshRadar();
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		if (mGridTransformComponent != null)
		{
			mGridTransformComponent.RemovePosUpdatedReceiver(this);
		}
		if (((Object)(object)mMapContainer != (Object)null))
		{
			mMapContainer.EntityAddEvent -= (IMyContainerAddEntityEventReceiver)this;
			mMapContainer.EntityRemoveEvent -= (IMyContainerRemoveEntityEventReceiver)this;
		}
	}

	private void EntityEnter(EcEntityIdentifier entityId)
	{
		IMapControllerEntityRadarSupport radarSupport = GetRadarSupport(entityId);
		radarSupport?.RadarTipProvider.EntityEnter(this, radarSupport);
	}

	private void EntityLeave(EcEntityIdentifier entityId)
	{
		IMapControllerEntityRadarSupport radarSupport = GetRadarSupport(entityId);
		radarSupport?.RadarTipProvider.EntityLeave(this, radarSupport);
	}

	void IGridTransformPosUpdatedEventReceiver.GridTransform_OnPosUpdated(ECGridTransformData transform, Pos2d prePos)
	{
		RefreshRadar();
	}

	void IMyContainerAddEntityEventReceiver.MyContainer_OnEntityAdd(MyEntity entity)
	{
		if (((Object)(object)entity.myGridTransform != (Object)null) && Pos2d.Distance(mMapController.GridPos, entity.myGridTransform.Pos) <= mRadarCheckDis)
		{
			mPreRangeEntityIdList.Add(entity.Id);
			EntityEnter(entity.Id);
		}
	}

	void IMyContainerRemoveEntityEventReceiver.MyContainer_OnEntityRemove(MyEntity myEntity)
	{
		EcEntityIdentifier id = myEntity.Id;
		if (mPreRangeEntityIdList.Remove(id))
		{
			EntityLeave(id);
		}
	}

	private void RefreshRadar()
	{
		mRangeEntityIdList.Clear();
		foreach (Pos2d mAroundPosOffset in mAroundPosOffsetList)
		{
			Pos2d pos = mGridTransformComponent.Pos + mAroundPosOffset;
			IRadarGamePosData gamePosData = MapInput.FindGamePosData(pos);
			if (gamePosData == null || !gamePosData.ExistEntitys())
			{
				continue;
			}
			foreach (ECViewEntityData entity in gamePosData.Entitys)
			{
				mRangeEntityIdList.Add(entity.Id);
			}
		}
		foreach (EcEntityIdentifier mPreRangeEntityId in mPreRangeEntityIdList)
		{
			if (!mRangeEntityIdList.Contains(mPreRangeEntityId))
			{
				EntityLeave(mPreRangeEntityId);
			}
		}
		foreach (EcEntityIdentifier mRangeEntityId in mRangeEntityIdList)
		{
			if (!mPreRangeEntityIdList.Contains(mRangeEntityId))
			{
				EntityEnter(mRangeEntityId);
			}
		}
		HashSet<EcEntityIdentifier> hashSet = mRangeEntityIdList;
		HashSet<EcEntityIdentifier> hashSet2 = mPreRangeEntityIdList;
		mPreRangeEntityIdList = hashSet;
		mRangeEntityIdList = hashSet2;
	}

	public virtual MapControllerEntityRadarTipBase AddTip(IMapControllerEntityRadarSupport support, MapControllerEntityRadarTipBase prefab)
	{
		if (mRadarTipDict.TryGetValue(support.MyEntity, out var value))
		{
			InitTip(value, support);
			return value;
		}
		if (!prefab.CanShowTip(support.MyEntity))
		{
			return null;
		}
		value = MapControllerEntityRadarTipManager.Instance.GetTip(support.RadarEffectSlot, prefab);
		if ((Object)(object)value == (Object)null)
		{
			Debug.LogError("MapControllerEntityRadarTipManager GetTip failed, EntityClassId:" + support.MyEntity.ClassId + ", RadarTipType:" + prefab.TipType + ", PrefabName:" + prefab.name);
			return null;
		}
		InitTip(value, support);
		ECWorldStageView eCWorldStageView = GetECWorldStageView(value.MyEntity);
		if (eCWorldStageView != null)
		{
			eCWorldStageView.OnStateUpdate += mOnStageStateUpdate;
		}
		mRadarTipDict.Add(value.MyEntity, value);
		return value;
	}

	private void OnStageStateUpdate(ECWorldStageStateData stageComp, int preState, object param)
	{
		if (((IWorldStageOperationState)stageComp).IsActive() && MapRootManagerProxy.TryGetEntity(((Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData)stageComp).Entity.Id, out var myEnt) && mRadarTipDict.TryGetValue(myEnt, out var value))
		{
			RemoveTip(value.MyEntity);
		}
	}

	public virtual void RemoveTip(MyEntity entity, bool immidietaly = false)
	{
		if (!((Object)(object)entity == (Object)null) && mRadarTipDict.ContainsKey(entity) && mRadarTipDict.TryGetValue(entity, out var value))
		{
			_ = entity.Id;
			value.Hide(immidietaly, mOnTipHiden);
		}
	}

	private void OnTipHiden(MapControllerEntityRadarTipBase tip)
	{
		MyEntity myEntity = tip.MyEntity;
		ECWorldStageView eCWorldStageView = GetECWorldStageView(myEntity);
		if (eCWorldStageView != null)
		{
			eCWorldStageView.OnStateUpdate -= mOnStageStateUpdate;
		}
		mRadarTipDict.Remove(myEntity);
		MapControllerEntityRadarTipManager.Instance.RecycleTip(tip);
	}

	private void InitTip(MapControllerEntityRadarTipBase tip, IMapControllerEntityRadarSupport support)
	{
		tip.Init(support.MyEntity);
		tip.Show();
	}

	private ECWorldStageView GetECWorldStageView(MyEntity myEntity)
    {
        if ((Object)(object)myEntity == (Object)null) return null;
        ECViewDriver componentInChildren = ((Component)myEntity).GetComponentInChildren<ECViewDriver>(true);
        if ((Object)(object)componentInChildren == (Object)null)
        { Debug.LogError("物件" + myEntity.ClassId + "没有ECViewDriver"); return null; }
        ECWorldStageView view = componentInChildren.GetView<ECWorldStageView>();
        if (view == null)
        { Debug.LogError("物件" + myEntity.ClassId + "没有ECWorldStageView"); return null; }
        return view;
    }

	private IMapControllerEntityRadarSupport GetRadarSupport(EcEntityIdentifier entityId)
	{
		if ((Object)(object)mMapContainer == (Object)null)
		{
			return null;
		}
		MyEntity entity = mMapContainer.GetEntity(entityId);
		if ((Object)(object)entity == (Object)null)
		{
			return null;
		}
		return MapControllerEntityRadarSupportUtils.GetMapControllerEntityRadarSupport(entity);
	}
}

}

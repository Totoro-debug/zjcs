using System;
using System.Collections.Generic;
using System.Threading;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.ProfessionUI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
public class MiniMapPrivateRegionMaskManager : MapMeCharacterUIBase
{
	[Serializable]
	public class RegionTilemapData
	{
		public Pos2d RegionMinPos;

		public Pos2d RegionMaxPos;

		public AssetGuidProxy<GameObject> RegionTilemapAssetGuidProxy;

		public Transform RegionTilemapSlot;
	}

	private const int kInactivedRegionZ = -1;

	private const int kActivedRegionZ = 0;

	private const float kClearDelaySecond = 10f;

	public MiniMapRegionContext MiniMap;

	[SerializeField]
	private Transform[] mRegionTilemapSlots;

	[SerializeField]
	private RegionTilemapData[] mRegionTilemapDatas;

	private Dictionary<int, GameObject> mRegionGameObjectDict = new Dictionary<int, GameObject>();

	private readonly MiniMapRegionAssetBag mDisposableBag = new MiniMapRegionAssetBag();

	private CancellationTokenSource mCancellationTokenSource;

	private List<int> mCacheList = new List<int>();

	protected override void Start()
	{
		base.Start();
		MiniMap.OnVisibleRangeUpdate += OnVisibleRangeUpdate;
		if (MiniMap.ViewRangeHandler != null)
		{
			OnVisibleRangeUpdate(MiniMap.ViewRangeHandler);
		}
	}

	protected override void OnDestroy()
	{
		MiniMap.OnVisibleRangeUpdate -= OnVisibleRangeUpdate;
		StopClearPool();
		mRegionGameObjectDict.Clear();
		mDisposableBag.Dispose();
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		List<MapRegionStatus> mapStatusListInTeam = MiniMap.GetMapStatusListInTeam(MiniMap.MapClassId);
		if (mapStatusListInTeam != null)
		{
			for (int i = 0; i < mapStatusListInTeam.Count; i++)
			{
				UnlockRegion(i, mapStatusListInTeam[i] == MapRegionStatus.Public);
			}
		}
		if (MiniMap.MapClassId == MiniMap.CurrentMapClassId)
		{
			MiniMap.OnMapStatusUpdate += OnMapVisibilityUpdate;
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		if (MiniMap.HasMainPlayer)
		{
			MiniMap.OnMapStatusUpdate -= OnMapVisibilityUpdate;
		}
	}

	private void OnMapVisibilityUpdate(int mapClassId, int region, MapRegionStatus preVisibility, MapRegionStatus visibility)
	{
		if (MiniMap.MapClassId == mapClassId)
		{
			UnlockRegion(region, visibility == MapRegionStatus.Public);
		}
	}

	private void UnlockRegion(int region, bool isActive)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		Transform regionTilemapSlot = GetRegionTilemapSlot(region);
		if (!((Object)(object)regionTilemapSlot == (Object)null))
		{
			Vector3 localPosition = regionTilemapSlot.localPosition;
			regionTilemapSlot.localPosition = new Vector3(localPosition.x, localPosition.y, (float)((!isActive) ? (-1) : 0));
		}
	}

	private Transform GetRegionTilemapSlot(int region)
	{
		if (region < 0 || region >= mRegionTilemapSlots.Length)
		{
			return null;
		}
		return mRegionTilemapSlots[region];
	}

	private void OnVisibleRangeUpdate(IMapViewRangeHandler handler)
	{
		RefreshVisiableRegionTilemap();
	}

	private void RefreshVisiableRegionTilemap()
	{
		bool flag = false;
		for (int num = mRegionTilemapDatas.Length - 1; num >= 0; num--)
		{
			UpdateRegionTilemap(num, mRegionTilemapDatas[num], out var needDelayClear);
			flag |= needDelayClear;
		}
		if (flag)
		{
			StopClearPool();
			DelayClearPool(10f);
		}
	}

	private void UpdateRegionTilemap(int region, RegionTilemapData data, out bool needDelayClear)
	{
		needDelayClear = false;
		bool flag = MiniMap.IsRangeInViewRange(data.RegionMinPos, data.RegionMaxPos);
		if (!mRegionGameObjectDict.TryGetValue(region, out var value))
		{
			if (flag)
			{
				IMiniMapRegionAssetLease assetOperationHandle = mDisposableBag.Add(MiniMap.LoadAssetByGuid(data.RegionTilemapAssetGuidProxy.AssetGuid));
				if ((Object)(object)assetOperationHandle.Result != (Object)null)
				{
					value = MiniMap.InstantiateRegion(assetOperationHandle.Result, data.RegionTilemapSlot, false);
					mRegionGameObjectDict[region] = value;
				}
			}
		}
		else
		{
			value.SetActive(flag);
			if (!flag)
			{
				needDelayClear = true;
			}
		}
	}

	private void StopClearPool()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	private void DelayClearPool(float delayS)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		mCancellationTokenSource = new CancellationTokenSource();
		UniTaskExtensions.Forget(DoLelayDestroYield(delayS, mCancellationTokenSource.Token));
	}

	// The original compiler-generated state machine awaits this exact player-loop
	// timing, then destroys only null/inactive entries in dictionary order.
	private async UniTask DoLelayDestroYield(float delayS, CancellationToken cancellationToken)
	{
		await UniTask.WaitForSeconds(delayS, false, (PlayerLoopTiming)8, cancellationToken, false);
		mCacheList.Clear();
		foreach (KeyValuePair<int, GameObject> current in mRegionGameObjectDict)
		{
			GameObject value = current.Value;
			if (value == null || !value.activeSelf)
			{
				mCacheList.Add(current.Key);
				if (value != null) Object.Destroy(value);
			}
		}
		foreach (int key in mCacheList) mRegionGameObjectDict.Remove(key);
		mCancellationTokenSource = null;
	}
}

}

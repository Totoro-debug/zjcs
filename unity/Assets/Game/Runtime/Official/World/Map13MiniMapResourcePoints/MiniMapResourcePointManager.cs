using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
public class MiniMapResourcePointManager : MapUIBase
{
	[SerializeField]
	private MonoDictionary<int, Zjcs.Official.ProfessionUI.AssetGuidProxy<GameObject>> mResourceTypePrefabGuidDict;

	[SerializeField]
	private MonoDictionary<int, Zjcs.Official.ProfessionUI.AssetGuidProxy<GameObject>> mResourceLayerPrefabGuidDict;

	private MiniMapResourcePointContext mMinimap;

	private Dictionary<int, MiniMapResourcePointBoard> mBoardDict = new Dictionary<int, MiniMapResourcePointBoard>();

	private HashSet<int> mActiveBroadSet = new HashSet<int>();

	private MiniMapResourcePointDisposableBag mMiniMapResourcePointDisposableBag;

	private MiniMapResourcePointContext ResourceLoader => mMinimap;
	public MiniMapResourcePointContext Context => mMinimap;



	public int MapClassId => mMinimap.MapClassId;

	public void Init(MiniMapResourcePointContext minimap)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		mMinimap = minimap;
		Services = minimap.LabelServices;
		mMiniMapResourcePointDisposableBag = default;
	}

	protected override void OnDestroy()
	{
		mMiniMapResourcePointDisposableBag.Dispose();
		base.OnDestroy();
	}

	public void SetBoard(MiniMapResourcePointBoard board)
	{
		mBoardDict.Add(board.RefreshGroup, board);
	}

	public bool IsBoardActive(int refreshGroup)
	{
		if (!mBoardDict.TryGetValue(refreshGroup, out var value))
		{
			return false;
		}
		return ((Component)value).gameObject.activeSelf;
	}

	public void Select(int refreshGroup, bool guide = false, string iconPath = null)
	{
		if (mResourceTypePrefabGuidDict.ContainsKey(refreshGroup) && !IsBoardActive(refreshGroup))
		{
			ShowBoard(refreshGroup, guide, iconPath);
		}
	}

	public void CancelSelect(int refreshGroup, bool guide = false)
	{
		HideBoard(refreshGroup, guide);
	}

	public AreaMiniMapResourcePointData GetSelectResourcePointDatas(int refreshGroup, int area)
	{
		MiniMapResourcePointBoard board = GetBoard(refreshGroup);
		if ((Object)(object)board == (Object)null)
		{
			return null;
		}
		return board.GetAreaData(area);
	}

	public IEnumerable<KeyValuePair<int, Zjcs.Official.ProfessionUI.AssetGuidProxy<GameObject>>> ForeachLayer()
	{
		foreach (KeyValuePair<int, Zjcs.Official.ProfessionUI.AssetGuidProxy<GameObject>> item in mResourceLayerPrefabGuidDict)
		{
			yield return item;
		}
	}

	public void GetRefreshGroupSetByItemId(int itemId, HashSet<int> set)
	{
		set = set ?? new HashSet<int>();
		set.Clear();
		MiniMapResourcePointItemLocationData locationInfos = mMinimap.GetItemLocationInfo(itemId);
		if (locationInfos.RuleIds == null || locationInfos.RuleIds.Count <= 0)
		{
			return;
		}
		foreach (int ruleId in locationInfos.RuleIds)
		{
			MiniMapResourcePointRefreshGroupInfo byAwardRule = mMinimap.GetRefreshGroupsByAwardRule(ruleId);
			if (byAwardRule == null)
			{
				continue;
			}
			foreach (int item in byAwardRule.RefreshGroupSet)
			{
				if (mResourceLayerPrefabGuidDict.ContainsKey(item))
				{
					set.Add(item);
				}
			}
		}
	}

	public void GetRefreshGroupSetByMonsterId(int monsterId, HashSet<int> set)
	{
		set = set ?? new HashSet<int>();
		set.Clear();
		MiniMapResourcePointRefreshGroupInfo byMonsterId = mMinimap.GetRefreshGroupsByMonsterId(monsterId);
		if (byMonsterId == null)
		{
			return;
		}
		foreach (int item in byMonsterId.RefreshGroupSet)
		{
			if (mResourceLayerPrefabGuidDict.ContainsKey(item))
			{
				set.Add(item);
			}
		}
	}

	private MiniMapResourcePointBoard GetBoard(int refreshGroup)
	{
		if (mBoardDict.TryGetValue(refreshGroup, out var value))
		{
			return value;
		}
		if (!mResourceLayerPrefabGuidDict.TryGetValue(refreshGroup, out var value2))
		{
			return null;
		}
		try
		{
			IResourcePointAssetLease<GameObject> assetOperationHandle = MiniMapResourcePointDisposable.AddTo<IResourcePointAssetLease<GameObject>>(ResourceLoader.LoadPrefabByGuid(value2.AssetGuid), ref mMiniMapResourcePointDisposableBag);
			if ((Object)(object)assetOperationHandle.Result != (Object)null)
			{
				mMinimap.InstantiatePrefab(assetOperationHandle.Result, ((Component)this).transform, false);
			}
		}
		catch (Exception exception)
		{
			MiniMapResourcePointErrorHandler message = new MiniMapResourcePointErrorHandler(61, 2, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("MiniMapResourcePointManager加载资源失败 MapClassId: ");
				message.AppendFormatted(MapClassId, 0, null, "MapClassId");
				message.AppendLiteral(" refreshGroup: ");
				message.AppendFormatted(refreshGroup, 0, null, "refreshGroup");
			}
			MiniMapResourcePointDiagnostic.Error(exception, ref message, null, "GetBoard", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MapResourcePoint/MiniMapResourcePointManager.cs", 168);
		}
		mBoardDict.TryGetValue(refreshGroup, out value);
		return value;
	}

	private void ShowBoard(int refreshGroup, bool guide, string iconPath)
	{
		if (mActiveBroadSet.Contains(refreshGroup))
		{
			return;
		}
		MiniMapResourcePointBoard board = GetBoard(refreshGroup);
		if (!((Object)(object)board == (Object)null))
		{
			board.SetCustomIconPath(iconPath);
			((Component)board).gameObject.SetActive(true);
			if (!guide)
			{
				mActiveBroadSet.Add(refreshGroup);
			}
		}
	}

	private void HideBoard(int refreshGroup, bool guide)
	{
		if (!guide || !mActiveBroadSet.Contains(refreshGroup))
		{
			MiniMapResourcePointBoard board = GetBoard(refreshGroup);
			if (!((Object)(object)board == (Object)null))
			{
				((Component)board).gameObject.SetActive(false);
				board.SetCustomIconPath(null);
				mActiveBroadSet.Remove(refreshGroup);
			}
		}
	}
}

}

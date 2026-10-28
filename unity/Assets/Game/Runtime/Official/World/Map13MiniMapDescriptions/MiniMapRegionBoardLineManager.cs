using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.Map13GridVisibility;
using MapRegionStatus=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
public class MiniMapRegionBoardLineManager : MonoBehaviour
{
	private const int kFindPathCallLimit = 1000;

	[SerializeField]
	private MiniMapDescriptionContext mMiniMap;

	[SerializeField]
	private MiniMapRegionDescManager mDescManager;

	private int mMapClassId;

	private bool mInited;

	private LineRendererDashedLine mDashedLinePrefab;

	private int mFindPathCallCount;

	private List<Pos2d> mCachePath = new List<Pos2d>();

	private List<List<Pos2d>> mCachePathList = new List<List<Pos2d>>();

	private List<LineRendererDashedLine> mLineRenderers = new List<LineRendererDashedLine>();

	private ObjectPool<LineRendererDashedLine> mPool;

	private Func<LineRendererDashedLine> mLineCreateFunc;

	private Action<LineRendererDashedLine> mOnLineGetAction;

	private Action<LineRendererDashedLine> mOnLineReleaseAction;

	private void Awake()
	{
		mMapClassId = mMiniMap.MapClassId;
		mMiniMap.LabelServices.Player.OnMapStatusUpdate += MainPlayer_OnMapStatusUpdate;
		mDescManager.OnRegionDescManagerInited += MDescManager_OnRegionDescManagerInited;
		mLineCreateFunc = LineCreate;
		mOnLineGetAction = OnLineGet;
		mOnLineReleaseAction = OnLineRelease;
	}

	private void OnDestroy()
	{
		if (mMiniMap.LabelServices.Player != null)
		{
			mMiniMap.LabelServices.Player.OnMapStatusUpdate -= MainPlayer_OnMapStatusUpdate;
		}
		mDescManager.OnRegionDescManagerInited -= MDescManager_OnRegionDescManagerInited;
	}

	private void MDescManager_OnRegionDescManagerInited()
	{
		mDescManager.OnRegionDescManagerInited -= MDescManager_OnRegionDescManagerInited;
		GameObject dashedLinePrefab = mMiniMap.MiniMapSetting.DescSetting.DashedLinePrefab;
		mDashedLinePrefab = dashedLinePrefab.GetComponent<LineRendererDashedLine>();
		mMiniMap.QuerySceneContainer(this);
		mPool = new ObjectPool<LineRendererDashedLine>(mLineCreateFunc, mOnLineGetAction, mOnLineReleaseAction, (Action<LineRendererDashedLine>)null, true, 10, 10000);
		mInited = true;
		DoRefresh();
	}

	private void MainPlayer_OnMapStatusUpdate(int mapClassId, int region, MapRegionStatus preStatus, MapRegionStatus status)
	{
		if (mapClassId == mMapClassId && mInited)
		{
			DoRefresh();
		}
	}

	private void DoRefresh()
	{
		List<List<Pos2d>> allPath = GetAllPath();
		if (allPath != null)
		{
			DrawAllLine(allPath);
		}
	}

	private void DrawAllLine(List<List<Pos2d>> allPath)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		ReturnAllLine();
		if (!mMiniMap.TryGetMapLiteData(mMapClassId, out var liteInfo))
		{
			return;
		}
		Vector3 val = liteInfo.MapGroundOriginPos / 2f;
		foreach (List<Pos2d> item in allPath)
		{
			LineRendererDashedLine lineRendererDashedLine = Borrow();
			lineRendererDashedLine.SetCount(item.Count);
			mLineRenderers.Add(lineRendererDashedLine);
			for (int num = item.Count - 1; num >= 0; num--)
			{
				lineRendererDashedLine.SetPoint(num, liteInfo.GridToPos(item[num]) / 2f + val);
			}
		}
	}

	private void ReturnAllLine()
	{
		for (int num = mLineRenderers.Count - 1; num >= 0; num--)
		{
			Return(mLineRenderers[num]);
		}
		mLineRenderers.Clear();
	}

	private LineRendererDashedLine Borrow()
	{
		return mPool.Get();
	}

	private void Return(LineRendererDashedLine line)
	{
		mPool.Release(line);
	}

	private LineRendererDashedLine LineCreate()
	{
		return mMiniMap.InstantiateDisplay(mDashedLinePrefab.gameObject, transform).GetComponent<LineRendererDashedLine>();
	}

	private void OnLineGet(LineRendererDashedLine line)
	{
		((Component)line).gameObject.SetActive(true);
	}

	private void OnLineRelease(LineRendererDashedLine line)
	{
		((Component)line).gameObject.SetActive(false);
	}

	private List<List<Pos2d>> GetAllPath()
	{
		Dictionary<Pos2d, List<Pos2d>> dictionary = new Dictionary<Pos2d, List<Pos2d>>();
		Dictionary<int, List<Pos2d>> mapRegionPosList = mMiniMap.GetMapRegionPosList(mMapClassId);
		if (mapRegionPosList == null)
		{
			return null;
		}
		foreach (KeyValuePair<int, MiniMapRegionDescItem> item in mDescManager.RegionDescItemDict)
		{
			MiniMapRegionDescItem value = item.Value;
			if (value.IsRegionUnlocked && value.RegionStatus != MapRegionStatus.Public && mapRegionPosList.TryGetValue(item.Key, out var value2))
			{
				UpdateRegionBorderEdgeDict(item.Key, value2, dictionary);
			}
		}
		mCachePathList.Clear();
		Dictionary<Pos2d, HashSet<Pos2d>> dictionary2 = new Dictionary<Pos2d, HashSet<Pos2d>>();
		while (dictionary.Count > dictionary2.Count && TryFindPath(dictionary, dictionary2))
		{
		}
		mCachePath.Clear();
		return mCachePathList;
	}

	private void UpdateRegionBorderEdgeDict(int region, List<Pos2d> regionPosList, Dictionary<Pos2d, List<Pos2d>> posEdgeDict)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		foreach (KeyValuePair<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> item in PrivateRegionUtility.GetGridsBorderLineDict(regionPosList))
		{
			foreach (KeyValuePair<Direction, (Vector2, Vector2)> item2 in item.Value)
			{
				AddPos(posEdgeDict, item2.Value.Item1, item2.Value.Item2);
			}
		}
		static void AddPos(Dictionary<Pos2d, List<Pos2d>> dict, Vector2 pos1, Vector2 pos2)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			Pos2d pos2d = new Pos2d(Mathf.RoundToInt(pos1.x * 2f), Mathf.RoundToInt(pos1.y * 2f));
			Pos2d pos2d2 = new Pos2d(Mathf.RoundToInt(pos2.x * 2f), Mathf.RoundToInt(pos2.y * 2f));
			if (!dict.TryGetValue(pos2d, out var value))
			{
				value = (dict[pos2d] = new List<Pos2d>());
			}
			value.Add(pos2d2);
			if (!dict.TryGetValue(pos2d2, out value))
			{
				value = (dict[pos2d2] = new List<Pos2d>());
			}
			value.Add(pos2d);
		}
	}

	private bool TryFindPath(Dictionary<Pos2d, List<Pos2d>> posEdgeDict, Dictionary<Pos2d, HashSet<Pos2d>> visitedEdgeDict)
	{
		Pos2d? pos2d = GetStartPos(posEdgeDict, visitedEdgeDict);
		if (!pos2d.HasValue)
		{
			return false;
		}
		mCachePath.Clear();
		mFindPathCallCount = 0;
		FindPath(pos2d.Value, posEdgeDict, visitedEdgeDict);
		return true;
		static Pos2d? GetStartPos(Dictionary<Pos2d, List<Pos2d>> dictionary, Dictionary<Pos2d, HashSet<Pos2d>> dictionary2)
		{
			foreach (KeyValuePair<Pos2d, List<Pos2d>> item in dictionary)
			{
				if (!dictionary2.TryGetValue(item.Key, out var value))
				{
					return item.Key;
				}
				foreach (Pos2d item2 in item.Value)
				{
					if (!value.Contains(item2))
					{
						return item2;
					}
				}
			}
			return null;
		}
	}

	public void FindPath(Pos2d startPos, Dictionary<Pos2d, List<Pos2d>> posEdgeDict, Dictionary<Pos2d, HashSet<Pos2d>> visitedEdgeDict)
	{
		if (++mFindPathCallCount >= 1000)
		{
			return;
		}
		if (posEdgeDict.TryGetValue(startPos, out var value))
		{
			foreach (Pos2d item in value)
			{
				if (!visitedEdgeDict.TryGetValue(startPos, out var value2) || !value2.Contains(item))
				{
					if (mCachePath.Count == 0)
					{
						mCachePath.Add(startPos);
					}
					mCachePath.Add(item);
					UpdatePosEdgeDict(item, startPos, visitedEdgeDict);
					FindPath(item, posEdgeDict, visitedEdgeDict);
				}
			}
		}
		if (mCachePath.Count > 0)
		{
			mCachePathList.Add(new List<Pos2d>(mCachePath));
			mCachePath.Clear();
		}
		static void UpdatePosEdgeDict(Pos2d pos2d, Pos2d endPos, Dictionary<Pos2d, HashSet<Pos2d>> dict)
		{
			if (!dict.TryGetValue(pos2d, out var value3))
			{
				value3 = (dict[pos2d] = new HashSet<Pos2d>());
			}
			value3.Add(endPos);
			if (!dict.TryGetValue(endPos, out value3))
			{
				value3 = (dict[endPos] = new HashSet<Pos2d>());
			}
			value3.Add(pos2d);
		}
	}
}

}

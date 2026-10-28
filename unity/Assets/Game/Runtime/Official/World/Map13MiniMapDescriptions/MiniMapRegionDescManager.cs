using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.Map13GridVisibility;
using MapRegionStatus=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
public class MiniMapRegionDescManager : MonoBehaviour
{
	public MiniMapDescriptionContext MiniMap;

	public Transform DescSlot;

	public MonoDictionary<MapRegionType, MiniMapRegionDescItem> RegionDescItemPrefabDict;

	private IMiniMapGridMover mGridMover;

	private Dictionary<int, MiniMapRegionDescItem> mRegionDescItemDict = new Dictionary<int, MiniMapRegionDescItem>();

	private MapRootEntityLiteSingleInfo mRootEntityLiteInfo;

	public MapRootEntityLiteSingleInfo RootEntityLiteInfo => mRootEntityLiteInfo;

	public IReadOnlyDictionary<int, MiniMapRegionDescItem> RegionDescItemDict => mRegionDescItemDict;

	public event Action OnRegionDescManagerInited;

	public void Init(int mapClassId, GridVisibilityAgentComponent gridVisAgent)
	{
		if (MiniMap.TryGetMapLiteData(MiniMap.MapClassId, out mRootEntityLiteInfo))
		{
			HashSet<int> visibleRegionSet = GetVisibleRegionSet((int x, int y) => gridVisAgent.GetPosVisibility(x, y));
			InitSub(visibleRegionSet);
		}
	}

	public void Init(MapGroundGridBoolData gridVisLitData)
	{
		if (MiniMap.TryGetMapLiteData(MiniMap.MapClassId, out mRootEntityLiteInfo))
		{
			HashSet<int> visibleRegionSet = GetVisibleRegionSet((int x, int y) => gridVisLitData.GetPosValue(x, y));
			InitSub(visibleRegionSet);
		}
	}

	public bool IsUsableJunctionPos(int x, int y, int currentRegion)
	{
		if (!RootEntityLiteInfo.TryGetPosData(x, y, out var info))
		{
			return false;
		}
		if (!Walkable(info.GroundType))
		{
			return false;
		}
		MiniMapDescriptionContext mapRegionInfoParser = MiniMap;
		foreach (Pos2d fourDir in Pos2d.FourDirs)
		{
			if (RootEntityLiteInfo.TryGetPosData(x + fourDir.X, y + fourDir.Y, out info))
			{
				int num = mapRegionInfoParser.AreaToRegion(MiniMap.MapClassId, info.Area);
				if (num >= 0 && num != currentRegion && Walkable(info.GroundType))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void InitSub(HashSet<int> unlockRegions)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		mGridMover = MiniMap.GetSpawnGridMover(MiniMap.MapClassId);
		foreach (Transform item in DescSlot)
		{
			Object.Destroy((Object)(object)((Component)item).gameObject);
		}
		mRegionDescItemDict.Clear();
		Dictionary<int, List<Pos2d>> mapRegionPosList = MiniMap.GetMapRegionPosList(MiniMap.MapClassId);
		IReadOnlyDictionary<int, MiniMapRegionInfo> mapRegionDict = MiniMap.GetMapRegionDict(MiniMap.MapClassId);
		List<Vector3> mapAllRegionCenter = GetMapAllRegionCenter(mapRegionDict);
		foreach (MiniMapRegionInfo value3 in mapRegionDict.Values)
		{
			if (!RegionDescItemPrefabDict.TryGetValue(value3.RegionTag, out var value))
			{
				MiniMapDescriptionErrorHandler message = new MiniMapDescriptionErrorHandler(73, 1, out var enabled);
				if (enabled)
				{
					message.AppendLiteral("MiniMapRegionDescManager的RegionDescItemPrefabDict没配置Region类型为[");
					message.AppendFormatted(value3.RegionTag, 0, null, "regionInfo.RegionTag");
					message.AppendLiteral("]情况下的prefab");
				}
				MiniMapDescriptionDiagnostic.Error(ref message, null, "InitSub", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/Support/MiniMapRegionDescManager.cs", 87);
				return;
			}
			mapRegionPosList.TryGetValue(value3.Region, out var value2);
			MiniMapRegionDescItem miniMapRegionDescItem = MiniMap.InstantiateDescription(value, DescSlot);
			((Component)miniMapRegionDescItem).transform.localPosition = mapAllRegionCenter[value3.Region];
			miniMapRegionDescItem.Init(this, value3.Region, unlockRegions.Contains(value3.Region), MiniMap.MiniMapSetting?.DescSetting, value2);
			mRegionDescItemDict.Add(value3.Region, miniMapRegionDescItem);
		}
		OnRegionDescManagerInited?.Invoke();
	}

	private bool Walkable(GroundType groundType)
	{
		if (mGridMover == null)
		{
			return false;
		}
		return mGridMover.Walkable(groundType);
	}

	private List<Vector3> GetMapAllRegionCenter(IReadOnlyDictionary<int, MiniMapRegionInfo> regionInfoDict)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		List<Vector3> list = new List<Vector3>(regionInfoDict.Count);
		for (int num = regionInfoDict.Count - 1; num >= 0; num--)
		{
			list.Add(Vector3.zero);
		}
		foreach (MiniMapRegionInfo value in regionInfoDict.Values)
		{
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (int area in value.Areas)
			{
				MapRootEntityLiteAreaInfo mapRootEntityLiteAreaInfo = RootEntityLiteInfo.AreaInfos[area];
				num2 += mapRootEntityLiteAreaInfo.TotalPos.X;
				num3 += mapRootEntityLiteAreaInfo.TotalPos.Y;
				num4 += mapRootEntityLiteAreaInfo.TotalCount;
			}
			list[value.Region] = RootEntityLiteInfo.GridToPos(new Pos2d(num2 / num4, num3 / num4));
		}
		return list;
	}

	private HashSet<int> GetVisibleRegionSet(Func<int, int, bool> GetPosVisibility)
	{
		int mapClassId = MiniMap.MapClassId;
		MiniMapDescriptionContext mapRegionInfoParser = MiniMap;
		mGridMover = MiniMap.GetSpawnGridMover(mapClassId);
		HashSet<int> hashSet = new HashSet<int>();
		for (int i = 0; i < RootEntityLiteInfo.MapGroundSize.Y; i++)
		{
			MapRootEntityLiteGridInfo[] array = RootEntityLiteInfo.PosDataArray[i];
			for (int j = 0; j < RootEntityLiteInfo.MapGroundSize.X; j++)
			{
				MapRootEntityLiteGridInfo mapRootEntityLiteGridInfo = array[j];
				if (mapRootEntityLiteGridInfo.Area >= 0 && !hashSet.Contains(mapRootEntityLiteGridInfo.Area) && Walkable(mapRootEntityLiteGridInfo.GroundType) && GetPosVisibility(j, i))
				{
					int num = mapRegionInfoParser.AreaToRegion(mapClassId, mapRootEntityLiteGridInfo.Area);
					if (num <= 0 || IsUsableJunctionPos(j, i, num))
					{
						hashSet.Add(mapRootEntityLiteGridInfo.Area);
					}
				}
			}
		}
		HashSet<int> hashSet2 = new HashSet<int>();
		foreach (int item in hashSet)
		{
			int num2 = mapRegionInfoParser.AreaToRegion(MiniMap.MapClassId, item);
			if (num2 >= 0)
			{
				hashSet2.Add(num2);
			}
		}
		List<int> worldMapProgresses = MiniMap.GetWorldMapProgresses(mapClassId);
		int count = worldMapProgresses.Count;
		for (int k = 0; k < count; k++)
		{
			if (worldMapProgresses[k] > 0)
			{
				hashSet2.Add(k);
			}
		}
		return hashSet2;
	}
}

}

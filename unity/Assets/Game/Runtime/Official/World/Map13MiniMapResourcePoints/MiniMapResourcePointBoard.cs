using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
public class MiniMapResourcePointBoard : MapUIBase, IMiniMapResourcePointBoard
{
	[SerializeField]
	private int mRefreshGroup;

	[SerializeField]
	private List<MiniMapResourcePointData> mPointDatas;

	private MiniMapResourcePointManager mManager;

	private Dictionary<int, AreaMiniMapResourcePointData> mAreaDataDict;

	private string mRefreshGroupIconPath;

	private string mCustomIconPath;

	public int RefreshGroup => mRefreshGroup;
	public MiniMapResourcePointContext Context => mManager.Context;

	private void Awake()
	{
		mManager = ((Component)((Component)this).transform.parent).GetComponent<MiniMapResourcePointManager>();
		if ((Object)(object)mManager == (Object)null)
		{
			MiniMapResourcePointErrorHandler message = new MiniMapResourcePointErrorHandler(74, 1, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("Without MiniMapResourcePointManager in parent gameobject, gameObject.name:");
				message.AppendFormatted(((Object)this).name, 0, null, "name");
			}
			MiniMapResourcePointDiagnostic.Error(ref message, null, "Awake", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MapResourcePoint/MiniMapResourcePointBoard.cs", 30);
			return;
		}
		Services = Context.LabelServices;
		mAreaDataDict = new Dictionary<int, AreaMiniMapResourcePointData>();
		MiniMapResourcePointEntityGroupInfo mapEntityGroupInfo = Context.GetRefreshGroupInfo(mRefreshGroup);
		mRefreshGroupIconPath = mapEntityGroupInfo.Icon;
		foreach (MiniMapResourcePointData mPointData in mPointDatas)
		{
			mPointData.Item.Init(this);
			if (!mAreaDataDict.TryGetValue(mPointData.Area, out var value))
			{
				AreaMiniMapResourcePointData areaMiniMapResourcePointData = (mAreaDataDict[mPointData.Area] = new AreaMiniMapResourcePointData());
				value = areaMiniMapResourcePointData;
				value.Area = mPointData.Area;
			}
			value.Datas.Add(mPointData);
		}
		foreach (KeyValuePair<int, AreaMiniMapResourcePointData> item in mAreaDataDict)
		{
			item.Value.SetActive(active: false);
		}
		mManager.SetBoard(this);
		((Component)this).gameObject.SetActive(false);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Context.OnMapStatusUpdate += MainPlayer_OnMapVisibilityUpdate;
		RefreshShow();
	}

	protected override void OnDisableAndInited()
	{
		if (Context.HasMainPlayer)
		{
			Context.OnMapStatusUpdate -= MainPlayer_OnMapVisibilityUpdate;
		}
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnMapVisibilityUpdate(int mapClassId, int region, MapRegionStatus preVisibility, MapRegionStatus visibility)
	{
		RefreshShow();
	}

	private void RefreshShow()
	{
		int mapClassId = mManager.MapClassId;
		MiniMapResourcePointContext mapRegionInfoParser = Context;
		foreach (KeyValuePair<int, AreaMiniMapResourcePointData> item in mAreaDataDict)
		{
			int curRegion = mapRegionInfoParser.AreaToRegion(mapClassId, item.Key);
			bool active = Context.GetMapRegionStatus(mapClassId, curRegion) == MapRegionStatus.Public;
			item.Value.SetActive(active);
		}
	}

	public void SetCustomIconPath(string iconPath)
	{
		mCustomIconPath = iconPath;
	}

	public string GetIconPath()
	{
		if (!string.IsNullOrEmpty(mCustomIconPath))
		{
			return mCustomIconPath;
		}
		return mRefreshGroupIconPath;
	}

	public AreaMiniMapResourcePointData GetAreaData(int area)
	{
		if (mAreaDataDict == null)
		{
			return null;
		}
		mAreaDataDict.TryGetValue(area, out var value);
		return value;
	}
}

}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniMapFullScreenPanel : MonoBehaviour, LoopScrollDataSource, IMiniMapHudFullScreenPanel
{
	public MiniMapFullScreenContext Context { get; set; }
	private const string cMiniMapPathFormat = "Map/Map_{0}/MiniMap/MiniMap_{0}";

	private const float kTabSwitchCooldownS = 0.5f;

	public Action<int> OnMiniMapChange;

	public Slider Slider;

	public MiniMapCameraController CameraController;

	public MiniMapPanelTitle PanelTitle;

	public LoopHorizontalScrollRect ScrollRect;

	[Header("打勾表示需要显示切换小地图标签的情况下显示的gameobject")]
	[SerializeField]
	private StateSwitcher<bool> mChangeMapTabSwitcher;

	[SerializeField]
	private MiniMapMarkUIFilter mMiniMapMarkUIFilter;

	private List<int> mActivedMapClassIdList = new List<int>();

	private int mMiniMapSwitchTabGroup;

	private MainlineMiniMapRoot mMiniMap;

	private int mMiniMapRequestVersion;

	private Action<int> mSwitchMiniMap;

	private long mTabSwitchTick;

	private readonly Dictionary<int, MiniMapSwitchTabItem> mMiniMapItemDict = new Dictionary<int, MiniMapSwitchTabItem>();

	private MiniMapSpawnDisposableBag mDisposableBag;




	public int CurrentMapClassId { get; private set; }

	public void Init(int openFromMapClassId)
	{
		mMiniMapSwitchTabGroup = Context.GetMapInfo(openFromMapClassId)?.MiniMapSwitchTabGroup ?? (-1);
	}

	private void Awake()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		mDisposableBag = default;
		MiniMapCameraController cameraController = CameraController;
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Combine(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		CurrentMapClassId = Context.CurrentMapClassId;
		RefreshActivedMapClassIdList();
		void RefreshActivedMapClassIdList()
		{
			MiniMapPanelMapInfo[] all = Context.GetAllMapInfos();
			foreach (MiniMapPanelMapInfo mapInfo in all)
			{
				if (mMiniMapSwitchTabGroup >= 0 && mMiniMapSwitchTabGroup == mapInfo.MiniMapSwitchTabGroup && Context.IsWorldMapVisited(mapInfo.ClassId))
				{
					mActivedMapClassIdList.Add(mapInfo.ClassId);
				}
			}
			mSwitchMiniMap = OnTabClicked;
			ScrollRect.dataSource = this;
			ScrollRect.totalCount = mActivedMapClassIdList.Count;
			ScrollRect.RefillCells();
		}
	}

	private void OnDestroy()
	{
		MiniMapCameraController cameraController = CameraController;
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Remove(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		mMiniMapItemDict.Clear();
		mDisposableBag.Dispose();
	}

	private void OnEnable()
	{
		SwitchMiniMap(Context.CurrentMapClassId);
		((UnityEvent<float>)(object)Slider.onValueChanged).AddListener((UnityAction<float>)OnSliderChangeValue);
		MainlineMiniMapRoot minimap = Context.CurrentMiniMap;
		if ((Object)(object)minimap.NonMaskableManager != (Object)null)
		{
			minimap.NonMaskableManager.OnShow(CameraController);
		}
		Context.HideWorldUI();
		mChangeMapTabSwitcher.SetState(minimap.MiniMapSetting.ShowMapSwitch);
	}

	private void OnDisable()
	{
		((UnityEvent<float>)(object)Slider.onValueChanged).RemoveListener((UnityAction<float>)OnSliderChangeValue);
		if ((Object)(object)mMiniMap == (Object)null || mMiniMap.MapClassId != Context.CurrentMapClassId)
		{
			SwitchMiniMap(Context.CurrentMapClassId);
		}
		HideCurrentMiniMap(isDisable: true);
		MainlineMiniMapRoot minimap = Context.CurrentMiniMap;
		if ((Object)(object)minimap.NonMaskableManager != (Object)null)
		{
			minimap.NonMaskableManager.OnHide(CameraController);
		}
		if (Context != null)
		{
			Context.ShowWorldUI();
		}
	}

	private void OnTabClicked(int mapClassId)
	{
		if (!(Context.CalcElapsedSecond(mTabSwitchTick) < 0.5))
		{
			mTabSwitchTick = Context.GetTick();
			SwitchMiniMap(mapClassId);
		}
	}

	private void SwitchMiniMap(int mapClassId)
	{
		RefreshMiniMap(mapClassId);
		CurrentMapClassId = mapClassId;
		int num = default;
		MiniMapSwitchTabItem miniMapSwitchTabItem = default;
		foreach (KeyValuePair<int, MiniMapSwitchTabItem> item in mMiniMapItemDict)
		{
			num = item.Key;
			miniMapSwitchTabItem = item.Value;
			miniMapSwitchTabItem.OnMiniMapChange(mapClassId);
		}
		OnMiniMapChange?.Invoke(mapClassId);
	}

	private void RefreshMiniMap(int mapClassId)
	{
		PanelTitle.SetKey(MiniMapPanelLanguage.GetMapNameKey(mapClassId));
		if (!IsSameMiniMap(mMiniMap, mapClassId))
		{
			HideCurrentMiniMap();
			ShowMiniMap(mapClassId);
		}
		static bool IsSameMiniMap(MainlineMiniMapRoot minimap, int num)
		{
			if ((Object)(object)minimap != (Object)null)
			{
				return num == minimap.MapClassId;
			}
			return false;
		}
	}

	private void HideCurrentMiniMap(bool isDisable = false)
	{
		mMiniMapRequestVersion++;
		if (!((Object)(object)mMiniMap == (Object)null))
		{
			OnHideMiniMap(mMiniMap, isDisable);
			mMiniMap = null;
		}
		void OnHideMiniMap(MainlineMiniMapRoot minimap, bool flag)
		{
			minimap.OnHideFullScreenMiniMap(CameraController);
			minimap.OnWantToCloseFullScreen -= Close;
			if (minimap.MapClassId != Context.CurrentMapClassId)
			{
				Context.ReleaseMiniMap(minimap);
			}
			else if (!flag)
			{
				((Component)minimap).gameObject.SetActive(false);
			}
		}
	}

	private void ShowMiniMap(int mapClassId)
	{
		if (mapClassId == Context.CurrentMapClassId)
		{
			ShowLoadedMiniMap(mMiniMap = Context.CurrentMiniMap);
			return;
		}
		int requestVersion = ++mMiniMapRequestVersion;
		try
		{
			Context.InstantiateMiniMapAsync(string.Format("Map/Map_{0}/MiniMap/MiniMap_{0}", mapClassId), ((Component)Context.CurrentMiniMap).transform.parent, (MainlineMiniMapRoot miniMap) =>
			{
				OnMiniMapLoaded(miniMap, mapClassId, requestVersion);
			});
		}
		catch (Exception exception)
		{
			Debug.LogException(new Exception("Minimap switch to mapClassId[" + mapClassId + "] failed", exception));
			SwitchMiniMap(Context.CurrentMapClassId);
		}
	}

	private void OnMiniMapLoaded(MainlineMiniMapRoot miniMap, int mapClassId, int requestVersion)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)miniMap == (Object)null)
		{
			Debug.LogError("MiniMapFullScreenPanel.OnMiniMapLoaded 加载小地图失败, mapClassId:" + mapClassId);
		}
		else if (requestVersion != mMiniMapRequestVersion)
		{
			Context.ReleaseMiniMap(miniMap);
		}
		else
		{
			mMiniMap = miniMap;
			((Component)miniMap).transform.position = ((Component)Context.CurrentMiniMap).transform.position;
			ShowLoadedMiniMap(miniMap);
		}
	}

	private void ShowLoadedMiniMap(MainlineMiniMapRoot miniMap)
	{
		((Component)miniMap).gameObject.SetActive(true);
		miniMap.OnWantToCloseFullScreen += Close;
		miniMap.OnShowFullScreenMiniMap(CameraController);
		mMiniMapMarkUIFilter.Refresh(miniMap, ((Component)miniMap).GetComponentInChildren<MiniMapMarkSpawnManager>(true));
	}

	private void OnSliderChangeValue(float value)
	{
		CameraController.SetCameraOrthographicSize(CameraController.mySizeMin + (CameraController.mySizeMax - CameraController.mySizeMin) * value);
	}

	private void OnCameraOrthographicSizeUpdate(MiniMapCameraController cameraController)
	{
		Slider.SetValueWithoutNotify(cameraController.GetZoomRate());
	}

	private void Close()
	{
		Context.CloseWindow(gameObject);
	}

	public void ProvideData(Transform transform, int idx)
	{
		int num = mActivedMapClassIdList[idx];
		MiniMapSwitchTabItem component = ((Component)transform).GetComponent<MiniMapSwitchTabItem>();
		mMiniMapItemDict[num] = component;
		MiniMapPanelMapInfo mapInfo = Context.GetMapInfo(num);
		Sprite result = LoadTabSprite(mapInfo.MiniMapIcon);
		component.Init(num, CurrentMapClassId, result, mSwitchMiniMap);
	}

    private Sprite LoadTabSprite(string path)
    {
        var lease = Context.LoadSprite(path);
        mDisposableBag.Add(lease);
        return lease.Result;
    }
}
}

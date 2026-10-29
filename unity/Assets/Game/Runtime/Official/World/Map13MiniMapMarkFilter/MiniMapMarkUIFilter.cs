using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
public class MiniMapMarkUIFilter : MonoBehaviour
{
	private static MiniMapMarkUIFilterSetting sFallbackFilterData;

	[SerializeField]
	private GameObject mEntry;

	[SerializeField]
	private GameObject mEntryItemPrefab;

	[SerializeField]
	private Transform mEntryContent;

	[SerializeField]
	private Button mEntryBtn;

	[SerializeField]
	private GameObject mFilter;

	[SerializeField]
	private GameObject mFilterItemPrefab;

	[SerializeField]
	private Transform mFilterContent;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private StateSwitcher<MiniMapMarkUIFilterSetting.SelectionType> mSelectionTypeSwticher;

	private GameObjectPool<MarkUIFilterEntryItem> mEntryItemsPool;

	private GameObjectPool<MiniMapMarkFilterItem> mFilterItemsPool;

	private MiniMapFilterMarkInfo[] mMarkInfos;

	private Dictionary<string, MiniMapMarkFilterItem> mFilterItems;

	private List<MarkUIFilterEntryItem> mEntryItems;

	private HashSet<GameObject> mFilterHitGroup;

	private Dictionary<string, IMiniMapDescriptionAssetLease<Sprite>> mMarkResourcesCache;

	private MiniMapManagedContext mMiniMap;

	private MiniMapMarkSpawnManager mMarkSpawnMgr;

	private IMiniMapMarkSpawnSupporter[] mSupports;

	private List<MiniMapMarkUIBase> mMarkUIBuffer;

	private Dictionary<string, int> mResourceLayerMatcher;

	private int mMapClassId;

	private bool mInited;

	private MiniMapSpawnDisposableBag mDisposableBag;

    public IMiniMapMarkFilterServices Services { get;set; }
    public IMiniMapFilterPersistentState PersistentState { get;set; }
    private IMiniMapMarkFilterServices EventSystemManager=>Services;
    private IMiniMapMarkFilterServices ResourceLoader=>Services;
    private IMiniMapMarkFilterServices InputOperationModule=>Services;
    private MiniMapResourcePointManager ResourcePointManager=>((IMiniMapFilterMapContext)mMiniMap).ResourcePointManager;

	private bool mEntryState
	{
		get
		{
			return mEntry.activeInHierarchy;
		}
		set
		{
			mEntry.SetActive(value);
			mFilter.SetActive(!value);
		}
	}

	private MiniMapMarkUIFilterSetting FilterDataProxy => PersistentState.State ?? sFallbackFilterData ?? (sFallbackFilterData = new MiniMapMarkUIFilterSetting());

	private void TryInit()
	{
		if (!mInited)
		{
			mEntryItemsPool = Services.CreatePool<MarkUIFilterEntryItem>(mEntryContent, mEntryItemPrefab);
			mFilterItemsPool = Services.CreatePool<MiniMapMarkFilterItem>(mFilterContent, mFilterItemPrefab);
			mEntryItems = new List<MarkUIFilterEntryItem>();
			mFilterItems = new Dictionary<string, MiniMapMarkFilterItem>();
			mMarkUIBuffer = new List<MiniMapMarkUIBase>();
			mFilterHitGroup = new HashSet<GameObject>();
			mResourceLayerMatcher = new Dictionary<string, int>();
			mMarkResourcesCache = new Dictionary<string, IMiniMapDescriptionAssetLease<Sprite>>();
			mMarkInfos = Services.GetAllMarkInfos();
			mInited = true;
		}
		mResourceLayerMatcher.Clear();
		foreach (var item in ResourcePointManager.ForeachLayer())
		{
			mResourceLayerMatcher.Add(item.Key.ToString(), item.Key);
		}
	}

	private void Awake()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		mDisposableBag = default;
		mEntryBtn.onClick.AddListener(OnEntryClick);
		mButton.onClick.AddListener(OnSelectAllButtonClick);
		InputOperationModule.HookOnInput(OnGlobalPointerDown);
	}

	private void OnDestroy()
	{
		if ((Object)(object)mMarkSpawnMgr != (Object)null)
		{
			MiniMapMarkSpawnManager miniMapMarkSpawnManager = mMarkSpawnMgr;
			miniMapMarkSpawnManager.OnUiMarkAdded = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Remove(miniMapMarkSpawnManager.OnUiMarkAdded, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkAdded));
		}
		mEntryItemsPool.Destory();
		mFilterItemsPool.Destory();
		InputOperationModule.UnhookOnInput(OnGlobalPointerDown);
		foreach (KeyValuePair<string, IMiniMapDescriptionAssetLease<Sprite>> item in mMarkResourcesCache)
		{
			item.Value.Dispose();
		}
		mDisposableBag.Dispose();
	}

	private void OnEnable()
	{
		mEntryState = true;
	}

	private void OnEntryClick()
	{
		mEntryState = false;
		Refresh();
	}

	private void OnSelectAllButtonClick()
	{
		bool flag = mSelectionTypeSwticher.State != MiniMapMarkUIFilterSetting.SelectionType.All;
		foreach (KeyValuePair<string, MiniMapMarkFilterItem> mFilterItem in mFilterItems)
		{
			MiniMapMarkFilterItem value = mFilterItem.Value;
			if (value.Selected == flag)
			{
				continue;
			}
			value.Selected = flag;
			if (mResourceLayerMatcher.TryGetValue(value.MarkName, out var value2))
			{
				if (flag)
				{
					ResourcePointManager.Select(value2);
				}
				else
				{
					ResourcePointManager.CancelSelect(value2);
				}
			}
		}
		MiniMapFilterMarkInfo[] array = mMarkInfos;
		foreach (MiniMapFilterMarkInfo miniMapMarkInfo in array)
		{
			FilterDataProxy.FilterState[miniMapMarkInfo.Name] = flag;
		}
		FilterDataProxy.SelectionState = (flag ? MiniMapMarkUIFilterSetting.SelectionType.All : MiniMapMarkUIFilterSetting.SelectionType.None);
		TrySyncFilterData();
		mSelectionTypeSwticher.SetState(FilterDataProxy.SelectionState);
		RefreshMiniMapMarksFilterState();
	}

	private void OnGlobalPointerDown(OnGlobalPointerDownResult result)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (mEntryState)
		{
			return;
		}
		foreach (RaycastResult item in EventSystemManager.ForeachRaycastResults(result.ScreenPos))
		{
			RaycastResult current = item;
			if (mFilterHitGroup.Contains(current.gameObject))
			{
				return;
			}
		}
		mEntryState = true;
	}

	private void OnMarkValueChanged(string markName, bool isOn)
	{
		if (mResourceLayerMatcher.TryGetValue(markName, out var value))
		{
			if (isOn)
			{
				ResourcePointManager.Select(value);
			}
			else
			{
				ResourcePointManager.CancelSelect(value);
			}
		}
		RefreshMiniMapMarksFilterState();
		MiniMapMarkUIFilterSetting.SelectionType selectionType = GetSelectionType();
		FilterDataProxy.SelectionState = selectionType;
		mSelectionTypeSwticher.SetState(selectionType);
		bool value2 = selectionType == MiniMapMarkUIFilterSetting.SelectionType.All;
		if (selectionType == MiniMapMarkUIFilterSetting.SelectionType.Free)
		{
			FilterDataProxy.FilterState[markName] = isOn;
		}
		else
		{
			MiniMapFilterMarkInfo[] array = mMarkInfos;
			foreach (MiniMapFilterMarkInfo miniMapMarkInfo in array)
			{
				FilterDataProxy.FilterState[miniMapMarkInfo.Name] = value2;
			}
		}
		TrySyncFilterData();
	}

	private void OnUiMarkAdded(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
		RefreshMiniMapMarksFilterState();
	}

	private void RefreshEntryItems()
	{
		foreach (MarkUIFilterEntryItem mEntryItem in mEntryItems)
		{
			mEntryItemsPool.Return(mEntryItem);
		}
		mEntryItems.Clear();
		MiniMapFilterMarkInfo[] array = mMarkInfos;
		foreach (MiniMapFilterMarkInfo miniMapMarkInfo in array)
		{
			if (!miniMapMarkInfo.ShowOnFilterEntry || (!miniMapMarkInfo.ShowOnMapClassId.Contains(mMapClassId) && !miniMapMarkInfo.ShowOnMapGroupId.Contains(mMiniMap.MarkDataQueries.GetMapInfo(mMapClassId).MapGroupId)))
			{
				continue;
			}
			MarkUIFilterEntryItem markUIFilterEntryItem = mEntryItemsPool.Borrow(mEntryContent);
			if (!mMarkResourcesCache.TryGetValue(miniMapMarkInfo.Image, out var value))
			{
				value = ResourceLoader.LoadSprite(miniMapMarkInfo.Image);
				if ((Object)(object)value.Result != (Object)null)
				{
					mMarkResourcesCache.Add(miniMapMarkInfo.Image, value);
				}
				else
				{
					Debug.LogError("MiniMapMarkUIFilter RefreshEntryItems: Failed to load sprite for mark " + miniMapMarkInfo.Name + " with path " + miniMapMarkInfo.Image);
				}
			}
			markUIFilterEntryItem.Init(value.Result, miniMapMarkInfo.Description);
			mEntryItems.Add(markUIFilterEntryItem);
		}
	}

	private void RefreshFilterItems()
	{
		foreach (MiniMapMarkFilterItem value in mFilterItems.Values)
		{
			mFilterItemsPool.Return(value);
		}
		mFilterItems.Clear();
		MiniMapFilterMarkInfo[] array = mMarkInfos;
		foreach (MiniMapFilterMarkInfo miniMapMarkInfo in array)
		{
			if (miniMapMarkInfo.ShowOnMapClassId.Contains(mMapClassId) || miniMapMarkInfo.ShowOnMapGroupId.Contains(mMiniMap.MarkDataQueries.GetMapInfo(mMapClassId).MapGroupId))
			{
				mFilterItems.Add(miniMapMarkInfo.Name, AcquireMark(miniMapMarkInfo));
			}
		}
		MiniMapMarkUIFilterSetting.SelectionType selectionType = GetSelectionType();
		mSelectionTypeSwticher.SetState(selectionType);
		if (selectionType != MiniMapMarkUIFilterSetting.SelectionType.Free)
		{
			FilterDataProxy.SelectionState = selectionType;
			TrySyncFilterData();
		}
		mFilterHitGroup.Clear();
		mFilterHitGroup.Add(mFilter.gameObject);
		Transform[] componentsInChildren = mFilter.GetComponentsInChildren<Transform>(true);
		foreach (Transform val in componentsInChildren)
		{
			mFilterHitGroup.Add(((Component)val).gameObject);
		}
	}

	private MiniMapMarkUIFilterSetting.SelectionType GetSelectionType()
	{
		bool flag = true;
		bool flag2 = true;
		foreach (MiniMapMarkFilterItem value in mFilterItems.Values)
		{
			if (value.Selected)
			{
				flag = false;
			}
			else
			{
				flag2 = false;
			}
		}
		if (!flag && !flag2)
		{
			return MiniMapMarkUIFilterSetting.SelectionType.Free;
		}
		if (!flag2)
		{
			return MiniMapMarkUIFilterSetting.SelectionType.None;
		}
		return MiniMapMarkUIFilterSetting.SelectionType.All;
	}

	private MiniMapMarkFilterItem AcquireMark(MiniMapFilterMarkInfo markInfo)
	{
		MiniMapMarkFilterItem miniMapMarkFilterItem = mFilterItemsPool.Borrow(mFilterContent);
		if (!mMarkResourcesCache.TryGetValue(markInfo.Image, out var value))
		{
			value = ResourceLoader.LoadSprite(markInfo.Image);
			if ((Object)(object)value.Result != (Object)null)
			{
				mMarkResourcesCache.Add(markInfo.Image, value);
			}
			else
			{
				Debug.LogError("MiniMapMarkUIFilter AcquireMark: Failed to load sprite for mark " + markInfo.Name + " with path " + markInfo.Image);
			}
		}
		miniMapMarkFilterItem.Init(markInfo.Name, value.Result, markInfo.Description, OnMarkValueChanged);
		bool flag = mResourceLayerMatcher.TryGetValue(markInfo.Name, out var value2);
		if (!FilterDataProxy.FilterState.TryGetValue(markInfo.Name, out var value3) && !flag)
		{
			value3 = true;
			FilterDataProxy.FilterState.Add(markInfo.Name, value3);
		}
		miniMapMarkFilterItem.Selected = value3;
		if (flag)
		{
			if (value3)
			{
				ResourcePointManager.Select(value2);
				return miniMapMarkFilterItem;
			}
			ResourcePointManager.CancelSelect(value2);
		}
		return miniMapMarkFilterItem;
	}

	private void TrySyncFilterData()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (PersistentState.State != null)
		{
			PersistentState.WriteStateAsync();
		}
	}

	private void RefreshMiniMapMarksFilterState()
	{
		FillAllMarkUI();
		foreach (MiniMapMarkUIBase item in mMarkUIBuffer)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.MarkAssetPath);
			if (mFilterItems.TryGetValue(fileNameWithoutExtension, out var value))
			{
				((Component)item).gameObject.SetActive(value.Selected);
			}
		}
		mMarkUIBuffer.Clear();
	}

	private void Refresh()
	{
		RefreshFilterItems();
		RefreshMiniMapMarksFilterState();
		RefreshEntryItems();
	}

	public void Refresh(MiniMapManagedContext minimap, MiniMapMarkSpawnManager markSpawnMgr)
	{
		mMiniMap = minimap;
		mMarkSpawnMgr = markSpawnMgr;
		mMapClassId = minimap.MapClassId;
		mSupports = ((Component)markSpawnMgr).GetComponents<IMiniMapMarkSpawnSupporter>();
		TryInit();
		if ((Object)(object)mMarkSpawnMgr != (Object)null)
		{
			MiniMapMarkSpawnManager miniMapMarkSpawnManager = mMarkSpawnMgr;
			miniMapMarkSpawnManager.OnUiMarkAdded = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Remove(miniMapMarkSpawnManager.OnUiMarkAdded, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkAdded));
			MiniMapMarkSpawnManager miniMapMarkSpawnManager2 = mMarkSpawnMgr;
			miniMapMarkSpawnManager2.OnUiMarkAdded = (Action<MiniMapMarkUIBase, EcEntityIdentifier>)Delegate.Combine(miniMapMarkSpawnManager2.OnUiMarkAdded, new Action<MiniMapMarkUIBase, EcEntityIdentifier>(OnUiMarkAdded));
		}
		Refresh();
	}

	private void FillAllMarkUI()
	{
		mMarkUIBuffer.Clear();
		if (mSupports != null && mSupports.Length != 0)
		{
			IMiniMapMarkSpawnSupporter[] array = mSupports;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].GetAllMarkUI(mMarkUIBuffer);
			}
		}
	}
}

}

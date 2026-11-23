// Selected original home entity registration/timeline implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityRuntime
{
using Object = UnityEngine.Object;
[DefaultExecutionOrder(-2)]
public class HomeEntityManagerMono : MonoBehaviour, IHomeEntityManagerContext, IHomeEntityDataAgent
{
	public MonoDictionary<EFurniture, Transform> FurnitureSlotDict;

	public MonoDictionary<EFurniture, EFurniture> FurnitureCreateMapping;

	public MonoDictionary<HomeThemeType, Transform> HomeThemeSlotDict;

	public HomeTimelinePlayable HomeTimelinePlayable;

	private IHomeEntityCreator<EFurniture> mFurnitureCreator;

	private IHomeEntityCreator<HomeThemeType> mThemeCreator;

	private List<IHomeEntityCreator> mCreators = new List<IHomeEntityCreator>();

	private Dictionary<Type, HashSet<HomeEntityData>> mEntityDataDict = new Dictionary<Type, HashSet<HomeEntityData>>();

	private int mPreviewWeatherId;

	private Dictionary<EFurniture, Transform> mFurnitureSlotDict;

	private HomeEntityRuntimeLifetime mDisposableBag;
    public IHomeEntityRuntimeServices Services { get; set; }
    public IHomeEntityResourceLoader ResourceLoader => Services.ResourceLoader;
    public IHomeFurnitureChangePublisher FurnitureChangePublisher => Services.FurnitureChangePublisher;
    public IHomeEntityManager EntityManager => Services.EntityManager;
    public IHomeEntityPrefabTables Tables => Services.Tables;
    public HomeEntityPrefabPaths PrefabPaths => Services.PrefabPaths;
    public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        => Services.Instantiate(prefab, parent, worldPositionStays);

	public IHomeEntitySkinData PlayerData => Services.PlayerData;

	public Dictionary<Type, HashSet<HomeEntityData>> EntityDataDict => mEntityDataDict;

	public Zjcs.Official.StoryPlayables.GAction<HomeEntityData> OnEntityDataAdd { get; } = new Zjcs.Official.StoryPlayables.GAction<HomeEntityData>();

	public Zjcs.Official.StoryPlayables.GAction<HomeEntityData> OnEntityDataRemove { get; } = new Zjcs.Official.StoryPlayables.GAction<HomeEntityData>();

	public int PreviewWeatherId => mPreviewWeatherId;

	private void Awake()
	{
		EntityManager.BindMono(this);
		InitCreators();
		mDisposableBag = new HomeEntityRuntimeLifetime();
		mDisposableBag.Add(Services.SubscribeSkinPreview(OnHomeSkinPreview));
		mDisposableBag.Add(Services.SubscribeHomeChange(OnHomeChange));
		mDisposableBag.Add(Services.SubscribeFurnitureSkinChanged(OnFurnitureSkinChanged));
		mDisposableBag.Add(Services.SubscribeThemeSkinChanged(OnHomeThemeSkinChanged));
		mDisposableBag.Add(Services.SubscribeEnvironmentSkinChanged(OnPlayerEnvironmentSkinChanged));
		OnHomeChange();
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
		EntityManager.Dispose();
	}

	private void InitCreators()
	{
		mThemeCreator = new ThemeEntityCreator(new Dictionary<HomeThemeType, Transform>(HomeThemeSlotDict));
		mFurnitureSlotDict = new Dictionary<EFurniture, Transform>(FurnitureSlotDict);
		mFurnitureCreator = new FurnitureEntityCreator(mFurnitureSlotDict, FurnitureCreateMapping);
		mCreators.Add(mThemeCreator);
		mCreators.Add(mFurnitureCreator);
	}

	private void OnHomeChange()
	{
		foreach (IHomeEntityCreator mCreator in mCreators)
		{
			mCreator.ReCreateAll(this);
		}
		Services.RuntimeBindingCacheService.MarkDirty();
		Services.SetShowTipDot(Services.InMyHome);
		Services.SetHomeSceneUIActive(Services.InMyHome);
	}

	private void OnHomeSkinPreview(HomeSkinPreviewData data)
	{
		HomeEntityItemInfo itemInfo = Services.GetItemInfo(data.ItemId);
		if (itemInfo == null)
		{
			return;
		}
		if (itemInfo.ItemType == HomeEntitySkinItemType.FurnitureSkin)
		{
			FurnitureSkinInfo furnitureSkinInfo = Services.GetFurnitureSkin(data.ItemId);
			OnFurnitureSkinChanged(furnitureSkinInfo.FurnitureType, data.ItemId);
		}
		else if (itemInfo.ItemType == HomeEntitySkinItemType.HomeThemeSkin)
		{
			HomeThemeSkinInfo homeThemeSkinInfo = Services.GetThemeSkin(data.ItemId);
			OnHomeThemeSkinChanged(homeThemeSkinInfo.ThemeType, data.ItemId);
		}
		else if (itemInfo.ItemType == HomeEntitySkinItemType.HomeEnvironmentSkin)
		{
			HomeEntityEnvironmentSkinInfo homeEnvironmentSkinInfo = Services.GetEnvironmentSkin(data.ItemId);
			if (homeEnvironmentSkinInfo.EnvironmentType == HomeEnvironmentType.Weather)
			{
				mPreviewWeatherId = ((!data.IsReset) ? data.ItemId : 0);
			}
			OnHomeEnvironmentSkinChanged(homeEnvironmentSkinInfo.EnvironmentType, data.ItemId);
		}
	}

	private void OnPlayerEnvironmentSkinChanged(HomeEnvironmentType environmentType, int id)
	{
		if (environmentType == HomeEnvironmentType.Weather)
		{
			mPreviewWeatherId = 0;
		}
		OnHomeEnvironmentSkinChanged(environmentType, id);
	}

	private void OnFurnitureSkinChanged(EFurniture furnitureType, int id)
	{
		mFurnitureCreator.OnSkinChanged(furnitureType, id, this, out var _);
		Services.RuntimeBindingCacheService.MarkDirty();
	}

	private void OnHomeThemeSkinChanged(HomeThemeType themeType, int id)
	{
		mThemeCreator.OnSkinChanged(themeType, id, this, out var _);
		Services.RuntimeBindingCacheService.MarkDirty();
	}

	private void OnHomeEnvironmentSkinChanged(HomeEnvironmentType environmentType, int _)
	{
		if (environmentType == HomeEnvironmentType.Weather)
		{
			mThemeCreator.ReCreateAll(this);
			mFurnitureCreator.ReCreateAll(this);
		}
		Services.RuntimeBindingCacheService.MarkDirty();
	}

	public GameObject GetOrCreateEntity(EFurniture e)
	{
		return mFurnitureCreator.GetOrCreate(e, this);
	}

	public GameObject GetOrCreateEntity(HomeThemeType e)
	{
		return mThemeCreator.GetOrCreate(e, this);
	}

	public void OnEntityCreated(GameObject go)
	{
		TryBindPlayableClip(go);
		if (TryAddEntityData(go))
		{
			mFurnitureCreator.ReCreateAll(this);
		}
	}

	public void OnEntityWillDestroy(GameObject go)
	{
		TryUnBindPlayableClip(go);
		if (TryRemoveEntityData(go))
		{
			mFurnitureCreator.ReleaseAll(this);
		}
	}

	public void GetDatas<T>(List<T> list) where T : HomeEntityData
	{
		list.Clear();
		if (!mEntityDataDict.TryGetValue(typeof(T), out var value))
		{
			return;
		}
		foreach (HomeEntityData item in value)
		{
			list.Add((T)item);
		}
	}

	public void TryBindPlayableClip(GameObject go)
	{
		HomePlayableClipBind homePlayableClipBind = default;
		if (!go.TryGetComponent<HomePlayableClipBind>(out homePlayableClipBind))
		{
			return;
		}
		foreach (HomePlayableClipData clipData in homePlayableClipBind.ClipDatas)
		{
			HomeTimelinePlayable.AddClipPlayable(clipData.Clip, clipData.Animator);
		}
	}

	public void TryUnBindPlayableClip(GameObject go)
	{
		HomePlayableClipBind homePlayableClipBind = default;
		if (!go.TryGetComponent<HomePlayableClipBind>(out homePlayableClipBind))
		{
			return;
		}
		foreach (HomePlayableClipData clipData in homePlayableClipBind.ClipDatas)
		{
			HomeTimelinePlayable.RemoveClipPlayable(clipData.Clip);
		}
	}

	private bool TryAddEntityData(GameObject go)
	{
		bool result = false;
		HomeEntityDataProvider homeEntityDataProvider = default;
		if (go.TryGetComponent<HomeEntityDataProvider>(out homeEntityDataProvider))
		{
			Type type = default;
			List<HomeEntityData> list = default;
			foreach (KeyValuePair<Type, List<HomeEntityData>> item in homeEntityDataProvider.DataDic)
			{
				item.Deconstruct(out type, out list);
				Type key = type;
				List<HomeEntityData> list2 = list;
				if (!mEntityDataDict.TryGetValue(key, out var value))
				{
					value = new HashSet<HomeEntityData>();
					mEntityDataDict[key] = value;
				}
				foreach (HomeEntityData item2 in list2)
				{
					value.Add(item2);
					if (item2 is HomeSlotEntityData slotEntityData)
					{
						UpdateFurnitureSlotDict(slotEntityData);
						result = true;
					}
					OnEntityDataAdd.SafeInvoke(item2);
				}
			}
		}
		return result;
	}

	private bool TryRemoveEntityData(GameObject go)
	{
		bool result = false;
		HomeEntityDataProvider homeEntityDataProvider = default;
		if (go.TryGetComponent<HomeEntityDataProvider>(out homeEntityDataProvider))
		{
			Type type = default;
			List<HomeEntityData> list = default;
			foreach (KeyValuePair<Type, List<HomeEntityData>> item in homeEntityDataProvider.DataDic)
			{
				item.Deconstruct(out type, out list);
				Type key = type;
				List<HomeEntityData> list2 = list;
				if (!mEntityDataDict.TryGetValue(key, out var value))
				{
					continue;
				}
				foreach (HomeEntityData item2 in list2)
				{
					if (item2 is HomeSlotEntityData slotEntityData)
					{
						RemoveFurnitureSlotDict(slotEntityData);
						result = true;
					}
					value.Remove(item2);
					OnEntityDataRemove.SafeInvoke(item2);
				}
			}
		}
		return result;
	}

	private void UpdateFurnitureSlotDict(HomeSlotEntityData slotEntityData)
	{
		EFurniture eFurniture = default;
		Transform val = default;
		foreach (KeyValuePair<EFurniture, Transform> item in slotEntityData.FurnitureDict)
		{
			item.Deconstruct(out eFurniture, out val);
			EFurniture key = eFurniture;
			Transform value = val;
			mFurnitureSlotDict[key] = value;
		}
	}

	private void RemoveFurnitureSlotDict(HomeSlotEntityData slotEntityData)
	{
		EFurniture eFurniture = default;
		Transform val = default;
		foreach (KeyValuePair<EFurniture, Transform> item in slotEntityData.FurnitureDict)
		{
			item.Deconstruct(out eFurniture, out val);
			EFurniture key = eFurniture;
			if (FurnitureSlotDict.TryGetValue(key, out var value))
			{
				mFurnitureSlotDict[key] = value;
			}
			else
			{
				mFurnitureSlotDict.Remove(key);
			}
		}
	}
}
}

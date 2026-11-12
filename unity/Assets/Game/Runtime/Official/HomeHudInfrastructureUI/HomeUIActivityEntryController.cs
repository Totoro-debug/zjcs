// Original selected home HUD infrastructure; explicit local service bindings.
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.HomeHudUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class HomeUIActivityEntryController : MonoBehaviour
{
	public IHomeHudRightEntryServices Services { private get; set; }

	[SerializeField]
	private Transform mEntryRoot;

	private HomePanelDisposableBag mDisposableBag;

	private readonly Dictionary<int, GameObject> mSortedIdToGo = new Dictionary<int, GameObject>();



	protected void OnEnable()
	{
		mDisposableBag = default;
		Services.OnActivitiesUpdate += new Action<ActivityType>(OnActivitiesUpdate);
		Services.OnActivityPreview += new Action<IHomeHudActivityData>(OnActivityPreview);
		Services.OnActivityOpen += new Action<IHomeHudActivityData>(OnActivityOpen);
		Services.OnActivityClose += new Action<IHomeHudActivityData>(OnActivityClose);
		RefreshAllEntries();
	}

	protected void OnDisable()
	{
		Services.OnActivitiesUpdate -= new Action<ActivityType>(OnActivitiesUpdate);
		Services.OnActivityPreview -= new Action<IHomeHudActivityData>(OnActivityPreview);
		Services.OnActivityOpen -= new Action<IHomeHudActivityData>(OnActivityOpen);
		Services.OnActivityClose -= new Action<IHomeHudActivityData>(OnActivityClose);
		DestroyAllEntries();
		mDisposableBag.Dispose();
	}

	private void OnActivitiesUpdate(ActivityType type)
	{
		RefreshAllEntries();
	}

	private void OnActivityPreview(IHomeHudActivityData data)
	{
		RefreshAllEntries();
	}

	private void OnActivityOpen(IHomeHudActivityData data)
	{
		TryEnsureActivityEntry(data);
	}

	private void OnActivityClose(IHomeHudActivityData data)
	{
		foreach (HomeRightEntryUIActivityInfo sortedEntry in Services.GetSortedEntries())
		{
			if (sortedEntry.IsActivity && IsSameActivity(sortedEntry, data))
			{
				DestroyEntry(sortedEntry.SortedId);
			}
		}
	}

	private void RefreshAllEntries()
	{
		foreach (HomeRightEntryUIActivityInfo sortedEntry in Services.GetSortedEntries())
		{
			if (!sortedEntry.IsActivity)
			{
				TryInstantiateEntry(sortedEntry);
			}
			else if (IsActivityOpen(sortedEntry))
			{
				TryInstantiateEntry(sortedEntry);
			}
			else
			{
				DestroyEntry(sortedEntry.SortedId);
			}
		}
	}

	private void TryEnsureActivityEntry(IHomeHudActivityData data)
	{
		foreach (HomeRightEntryUIActivityInfo sortedEntry in Services.GetSortedEntries())
		{
			if (sortedEntry.IsActivity && IsSameActivity(sortedEntry, data) && IsActivityOpen(sortedEntry))
			{
				TryInstantiateEntry(sortedEntry);
			}
		}
	}

	private static bool IsSameActivity(HomeRightEntryUIActivityInfo info, IHomeHudActivityData data)
	{
		if (!info.ActivityType.HasValue || info.ActivityType.Value != data.ActivityType)
		{
			return false;
		}
		if (info.ActivityId.HasValue && info.ActivityId.Value > 0)
		{
			return info.ActivityId.Value == data.Id;
		}
		return true;
	}

	private bool IsActivityOpen(HomeRightEntryUIActivityInfo info)
	{
		if (!info.ActivityType.HasValue)
		{
			return false;
		}
		ActivityType value = info.ActivityType.Value;
		IHomeHudActivityData activityData;
		if (info.ActivityId.HasValue && info.ActivityId.Value > 0)
		{
			return Services.TryGetActivity(value, info.ActivityId.Value, out activityData);
		}
		Dictionary<int, IHomeHudActivityData> activityDatas;
		return Services.TryGetActivityByType(value, out activityDatas);
	}

	private void TryInstantiateEntry(HomeRightEntryUIActivityInfo info)
	{
		if (!mSortedIdToGo.ContainsKey(info.SortedId))
		{
			GameObject gameObject = HomeHudInfrastructureUtils.Instantiate(Services, HomeHudInfrastructureUtils.LoadPrefab(Services, info.EntryPath, ref mDisposableBag).Result, mEntryRoot);
			mSortedIdToGo[info.SortedId] = gameObject;
			InsertBySortedId(gameObject, info.SortedId);
		}
	}

	private void InsertBySortedId(GameObject go, int sortedId)
	{
		int num = 0;
		foreach (KeyValuePair<int, GameObject> item in mSortedIdToGo)
		{
			if (item.Key < sortedId)
			{
				num++;
			}
		}
		go.transform.SetSiblingIndex(num);
	}

	private void DestroyEntry(int sortedId)
	{
		if (mSortedIdToGo.TryGetValue(sortedId, out var value))
		{
			mSortedIdToGo.Remove(sortedId);
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
	}

	private void DestroyAllEntries()
	{
		foreach (GameObject value in mSortedIdToGo.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		mSortedIdToGo.Clear();
	}
}
}

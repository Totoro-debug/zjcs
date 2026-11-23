// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public abstract class BaseHomeEntityCreator<TKey> : IHomeEntityCreator<TKey>, IHomeEntityCreator where TKey : Enum
{
	private readonly Dictionary<TKey, HomeEntity> mEntityDict = new Dictionary<TKey, HomeEntity>();

	private readonly Dictionary<TKey, Transform> mSlotDict;

	protected BaseHomeEntityCreator(Dictionary<TKey, Transform> slotDict)
	{
		mSlotDict = slotDict;
	}

	public GameObject GetOrCreate(TKey key, IHomeEntityManagerContext context)
	{
		TKey val = ResolveKey(key);
		if (!mEntityDict.TryGetValue(val, out var value))
		{
			int currentSkinId = GetCurrentSkinId(val, context);
			string prefabPath = GetPrefabPath(currentSkinId, context);
			value = Create(val, prefabPath, context);
		}
		if (value == null)
		{

			Debug.LogError("Official BaseHomeEntityCreator.GetOrCreate");
			return null;
		}
		return value.GameObject;
	}

	private HomeEntity Create(TKey key, string prefabPath, IHomeEntityManagerContext content)
	{

		if (!mSlotDict.TryGetValue(key, out var value))
		{

			Debug.LogError("Official BaseHomeEntityCreator.Create");
			return null;
		}
		IHomeEntityPrefabHandle assetOperationHandle = content.ResourceLoader.LoadPrefab(prefabPath);
		GameObject result = assetOperationHandle.Result;
		if ((Object)(object)result == (Object)null)
		{

			Debug.LogError("Official BaseHomeEntityCreator.Create");
			return null;
		}
		GameObject val = content.Instantiate(result, value, false);
		HomeEntityCreateData homeEntityCreateData = default;
		if (val.TryGetComponent<HomeEntityCreateData>(out homeEntityCreateData))
		{
			Transform transform = val.transform;
			transform.localPosition += homeEntityCreateData.LocalPosOffset;
		}
		HomeEntity homeEntity = new HomeEntity
		{
			PrefabPath = prefabPath,
			GameObject = val,
			Handle = assetOperationHandle
		};
		mEntityDict[key] = homeEntity;
		content.OnEntityCreated(val);
		return homeEntity;
	}

	public virtual void OnSkinChanged(TKey key, int id, IHomeEntityManagerContext context, out bool isCreateNew)
	{
		isCreateNew = TryCreateAndReleaseLast(key, id, context);
		if (isCreateNew)
		{
			OnAfterSkinChanged(key, id, context);
		}
	}

	private bool TryCreateAndReleaseLast(TKey key, int id, IHomeEntityManagerContext context)
	{
		string prefabPath = GetPrefabPath(id, context);
		if (mEntityDict.TryGetValue(key, out var value) && string.Equals(prefabPath, value.PrefabPath))
		{
			return false;
		}
		if (value != null)
		{
			context.OnEntityWillDestroy(value.GameObject);
			value.Release();
		}
		Create(key, prefabPath, context);
		return true;
	}

	public void ReCreateAll(IHomeEntityManagerContext context)
	{
		TKey val = default;
		Transform val2 = default;
		foreach (KeyValuePair<TKey, Transform> item in mSlotDict)
		{
			item.Deconstruct(out val, out val2);
			TKey key = val;
			int currentSkinId = GetCurrentSkinId(key, context);
			TryCreateAndReleaseLast(key, currentSkinId, context);
		}
	}

	public void ReleaseAll(IHomeEntityManagerContext context)
	{
		foreach (HomeEntity value in mEntityDict.Values)
		{
			context.OnEntityWillDestroy(value.GameObject);
			value.Release();
		}
		mEntityDict.Clear();
	}

	protected abstract int GetCurrentSkinId(TKey key, IHomeEntityManagerContext context);

	protected abstract string GetPrefabPath(int id, IHomeEntityManagerContext context);

	protected bool TryGetSkinOverridePrefabName(int skinId, IHomeEntityManagerContext context, out string prefabName)
	{
		int key = ((context.PreviewWeatherId > 0) ? context.PreviewWeatherId : context.PlayerData.GetEnvironmentSkinId(HomeEnvironmentType.Weather));
		HomeSkinWeatherOverridePrefabInfo homeSkinWeatherOverridePrefabInfo = context.Tables.GetWeatherOverride(skinId, key);
		if (homeSkinWeatherOverridePrefabInfo == null)
		{
			prefabName = null;
			return false;
		}
		prefabName = homeSkinWeatherOverridePrefabInfo.PrefabName;
		return true;
	}

	protected virtual TKey ResolveKey(TKey key)
	{
		return key;
	}

	protected virtual void OnAfterSkinChanged(TKey key, int id, IHomeEntityManagerContext manager)
	{
		IHomeEntityChangeCreateNotify homeEntityChangeCreateNotify = default;
		if (mEntityDict.TryGetValue(key, out var value) && value.GameObject.TryGetComponent<IHomeEntityChangeCreateNotify>(out homeEntityChangeCreateNotify))
		{
			homeEntityChangeCreateNotify.OnChangeCreate();
		}
	}

	protected abstract string GetEntityTypeName();
}
}

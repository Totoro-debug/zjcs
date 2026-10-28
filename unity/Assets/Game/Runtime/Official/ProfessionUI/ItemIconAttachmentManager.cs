namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemIconAttachmentManager : IItemIconAttachmentManager, IDisposable
{
	private class ItemIconAttachmentLoader : IDisposable
	{
		private IProfessionAssetOperation<GameObject> mHandle;

		public ItemIconAttachmentLoader(IProfessionImageResourceLoader resorurceLoader, string assetGuid)
		{
			mHandle = resorurceLoader.LoadAssetByGuid<GameObject>(assetGuid);
		}

		public void Dispose()
		{
			if (mHandle != null)
			{
				mHandle.Dispose();
				mHandle = null;
			}
		}

		public GameObject GetAttachment()
		{
			if (mHandle == null || !mHandle.IsValid)
			{
			Debug.LogError("Original ItemIconAttachmentManager lifecycle warning/error.");
				return null;
			}
			return mHandle.Result;
		}
	}

	private Func<ItemIconAttachmentConfig> mConfig;

	private Transform mPoolTrans;

	private readonly ProfessionResourceOperationBag mDisposable = new ProfessionResourceOperationBag();

	private const string kPoolName = "ItemIconAttachmentPool";

	private ItemIconAttachmentPool mPool;

	private IProfessionImageResourceLoader mResorurceLoader;

	private Dictionary<ItemIconAttachmentType, ItemIconAttachmentLoader> mAttachmentPrefabLoaders;

	public ItemIconAttachmentManager(IProfessionImageResourceLoader resourceLoader, Func<ItemIconAttachmentConfig> config, GAction lowMemory)
	{
		mResorurceLoader = resourceLoader;
		mConfig = config;
		mAttachmentPrefabLoaders = new Dictionary<ItemIconAttachmentType, ItemIconAttachmentLoader>();
		CreatePool();
		mDisposable.Add(lowMemory.Subscribe(OnLowMemory));
	}

	public void Dispose()
	{
		if (mPool != null)
		{
			mPool.Dispose();
			mPool = null;
		}
		if ((Object)(object)mPoolTrans != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)mPoolTrans).gameObject);
			mPoolTrans = null;
		}
		if (mAttachmentPrefabLoaders != null)
		{
			foreach (KeyValuePair<ItemIconAttachmentType, ItemIconAttachmentLoader> mAttachmentPrefabLoader in mAttachmentPrefabLoaders)
			{
				mAttachmentPrefabLoader.Value.Dispose();
			}
			mAttachmentPrefabLoaders = null;
		}
		mDisposable.Dispose();
	}

	public GameObject GetAttachment(ItemIconAttachmentType type, Transform slot, bool autoActive)
	{
		if (mPool != null)
		{
			return mPool.GetAttachmentByType(type, slot, autoActive);
		}
		return null;
	}

	public void RecycleAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell attachment)
	{
		if (mPool != null)
		{
			mPool.RecycletAttachment(type, attachment);
		}
	}

	private void OnLowMemory()
	{
		if (mPool != null)
		{
			mPool.Release();
		}
	}

	private void CreatePool()
	{


		GameObject val = new GameObject("ItemIconAttachmentPool");
		mPoolTrans = val.transform;
		Object.DontDestroyOnLoad((Object)(object)val);
		mPool = new ItemIconAttachmentPool(OnLoadAttachmentPrefab, mPoolTrans);
	}

	private GameObject OnLoadAttachmentPrefab(ItemIconAttachmentType type)
	{
		mAttachmentPrefabLoaders.TryGetValue(type, out var value);
		if (value == null)
		{
			ItemIconAttachmentConfig.ItemIconAttachmentProxy itemIconAttachmentProxy = mConfig().Templates.First((ItemIconAttachmentConfig.ItemIconAttachmentProxy v) => type == v.Type);
			value = new ItemIconAttachmentLoader(mResorurceLoader, itemIconAttachmentProxy.GuidProxy.AssetGuid);
			mAttachmentPrefabLoaders.Add(type, value);
		}
		return value.GetAttachment();
	}
}

}

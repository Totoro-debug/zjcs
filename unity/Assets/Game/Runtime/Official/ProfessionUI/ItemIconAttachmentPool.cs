namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ItemIconAttachmentPool : IDisposable
{
	private Dictionary<ItemIconAttachmentType, GameObjectPool<ItemIconAttachmentCell>> mPools;

	private Transform mPoolRoot;

	private Func<ItemIconAttachmentType, GameObject> mOnLoaderAttachmentPrefab;

	public ItemIconAttachmentPool(Func<ItemIconAttachmentType, GameObject> onLoaderAttachmentPrefab, Transform poolRoot)
	{
		mPools = new Dictionary<ItemIconAttachmentType, GameObjectPool<ItemIconAttachmentCell>>();
		mOnLoaderAttachmentPrefab = onLoaderAttachmentPrefab;
		mPoolRoot = poolRoot;
	}

	public void Dispose()
	{
		Release();
		mPools = null;
		mOnLoaderAttachmentPrefab = null;
	}

	public void Release()
	{
		foreach (KeyValuePair<ItemIconAttachmentType, GameObjectPool<ItemIconAttachmentCell>> mPool in mPools)
		{
			mPool.Value.Release();
		}
	}

	public GameObject GetAttachmentByType(ItemIconAttachmentType type, Transform slot, bool autoActive)
	{
		return GetPool(type).BorrowGo(slot, autoActive);
	}

	public void RecycletAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell attachment)
	{
		if ((Object)null != (Object)(object)attachment)
		{
			GetPool(type).Return(attachment);
		}
	}

	private GameObjectPool<ItemIconAttachmentCell> GetPool(ItemIconAttachmentType type)
	{
		GameObjectPool<ItemIconAttachmentCell> value = null;
		if (!mPools.TryGetValue(type, out value))
		{
			value = new GameObjectPool<ItemIconAttachmentCell>(mPoolRoot, (Transform slot, GameObject itemType, bool autoActive) => CreateAttachment(slot, type, autoActive), type.ToString());
			mPools.Add(type, value);
		}
		return value;
	}

	private GameObject CreateAttachment(Transform slot, ItemIconAttachmentType type, bool autoActive)
	{
		GameObject val = mOnLoaderAttachmentPrefab(type);
		if ((Object)(object)val == (Object)null)
		{
			Debug.LogError("Original ItemIconAttachmentPool lifecycle warning/error.");
			return null;
		}
		GameObject val2 = ProfessionGameObjectPoolBridge.Instantiate(slot, val, slot, false);
		if (autoActive)
		{
			val2.SetActive(true);
		}
		return val2;
	}
}

}

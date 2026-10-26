using System;


namespace UnityEngine.UI
{

public abstract class LoopScrollRect : LoopScrollRectBase
{
	public LoopScrollSinglePrefabSource DefaultPrefabSource;

	[NonSerialized]
	[HideInInspector]
	public LoopScrollDataSource dataSource;



	public override LoopScrollPrefabSource prefabSource
	{
		get
		{
			if (mPrefabSource == null)
			{
				DefaultPrefabSource.Init(((Component)this).transform);
				mPrefabSource = DefaultPrefabSource;
			}
			return mPrefabSource;
		}
	}

	protected override void ProvideData(Transform transform, int index)
	{
		dataSource?.ProvideData(transform, GetFinalIndex(index));
	}

	protected override RectTransform GetFromTempPool(int itemIdx)
	{
		RectTransform val = null;
		if (deletedItemTypeStart > 0)
		{
			deletedItemTypeStart--;
			Transform child = ((Transform)m_Content).GetChild(0);
			val = (RectTransform)(object)((child is RectTransform) ? child : null);
			((Transform)val).SetSiblingIndex(itemIdx - itemTypeStart + deletedItemTypeStart);
		}
		else if (deletedItemTypeEnd > 0)
		{
			deletedItemTypeEnd--;
			Transform child2 = ((Transform)m_Content).GetChild(((Transform)m_Content).childCount - 1);
			val = (RectTransform)(object)((child2 is RectTransform) ? child2 : null);
			((Transform)val).SetSiblingIndex(itemIdx - itemTypeStart + deletedItemTypeStart);
		}
		else
		{
			Transform transform = prefabSource.GetObject(itemIdx).transform;
			val = (RectTransform)(object)((transform is RectTransform) ? transform : null);
			((Component)val).transform.SetParent((Transform)(object)m_Content, false);
			((Component)val).gameObject.SetActive(true);
		}
		ProvideData((Transform)(object)val, itemIdx);
		return val;
	}

	protected override void ReturnToTempPool(bool fromStart, int count)
	{
		if (fromStart)
		{
			deletedItemTypeStart += count;
		}
		else
		{
			deletedItemTypeEnd += count;
		}
	}

	protected override void ClearTempPool()
	{
		if (deletedItemTypeStart > 0)
		{
			for (int num = deletedItemTypeStart - 1; num >= 0; num--)
			{
				ClearCell(num);
			}
			deletedItemTypeStart = 0;
		}
		if (deletedItemTypeEnd > 0)
		{
			int num2 = ((Transform)m_Content).childCount - deletedItemTypeEnd;
			for (int num3 = ((Transform)m_Content).childCount - 1; num3 >= num2; num3--)
			{
				ClearCell(num3);
			}
			deletedItemTypeEnd = 0;
		}
	}
}

}

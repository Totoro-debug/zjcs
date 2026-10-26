using UnityEngine.EventSystems;

namespace UnityEngine.UI
{

[AddComponentMenu("UI/Loop Vertical Scroll Rect", 51)]
[DisallowMultipleComponent]
public class LoopVerticalScrollRect : LoopScrollRect
{
	private VerticalLayoutGroup mVerticalLayoutGroup;

	private bool mInitVerticalLayoutGroup;

	private VerticalLayoutGroup VerticalLayoutGroup
	{
		get
		{
			if (!mInitVerticalLayoutGroup && (Object)(object)mVerticalLayoutGroup == (Object)null)
			{
				mVerticalLayoutGroup = ((Component)m_Content).GetComponent<VerticalLayoutGroup>();
				mInitVerticalLayoutGroup = true;
			}
			return mVerticalLayoutGroup;
		}
	}

	protected override LoopScrollRectDirection direction { get; set; }

	protected override float GetSize(RectTransform item, bool includeSpacing)
	{
		float num = (includeSpacing ? contentSpacing : 0f);
		if ((Object)(object)m_GridLayout != (Object)null)
		{
			num += m_GridLayout.cellSize.y;
		}
		else
		{
			num = ((!(bool)((Object)(object)VerticalLayoutGroup) || !((HorizontalOrVerticalLayoutGroup)VerticalLayoutGroup).childControlHeight) ? (num + item.sizeDelta.y) : (num + LoopScrollSizeUtils.GetPreferredHeight(item)));
		}
		return num * ((Transform)m_Content).localScale.y;
	}

	protected override float GetDimension(Vector2 vector)
	{
		return vector.y;
	}

	protected override float GetAbsDimension(Vector2 vector)
	{
		return vector.y;
	}

	protected override Vector2 GetVector(float value)
	{
		return new Vector2(0f, value);
	}

	protected override void Awake()
	{
		base.Awake();
		if ((bool)((Object)(object)m_Content))
		{
			GridLayoutGroup component = ((Component)m_Content).GetComponent<GridLayoutGroup>();
			if ((Object)(object)component != (Object)null && (int)component.constraint != 1)
			{
				Debug.LogError((object)"[LoopScrollRect] unsupported GridLayoutGroup constraint");
			}
		}
	}

	protected override bool UpdateItems(ref Bounds viewBounds, ref Bounds contentBounds)
	{
		bool flag = false;
		if (viewBounds.size.y < contentBounds.min.y - viewBounds.max.y && itemTypeEnd > itemTypeStart)
		{
			int num = -1;
			if (totalCount >= 0)
			{
				num = Mathf.Max(0, totalCount - (itemTypeEnd - itemTypeStart));
			}
			float y = contentBounds.size.y;
			float num2 = (y - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines;
			ReturnToTempPool(fromStart: true, itemTypeEnd - itemTypeStart);
			itemTypeStart = itemTypeEnd;
			int num3 = Mathf.FloorToInt((contentBounds.min.y - viewBounds.max.y) / (num2 + contentSpacing));
			if (num >= 0 && itemTypeStart + num3 * contentConstraintCount > num)
			{
				num3 = Mathf.FloorToInt((float)(num - itemTypeStart) / (float)contentConstraintCount);
			}
			itemTypeStart += num3 * contentConstraintCount;
			if (totalCount >= 0)
			{
				itemTypeStart = Mathf.Max(itemTypeStart, 0);
			}
			itemTypeEnd = itemTypeStart;
			float num4 = (float)num3 * (num2 + contentSpacing);
			RectTransform val = m_Content;
			val.anchoredPosition -= new Vector2(0f, num4 + (reverseDirection ? 0f : y));
			contentBounds.center -= new Vector3(0f, num4 + y / 2f, 0f);
			contentBounds.size = Vector3.zero;
			flag = true;
		}
		if (viewBounds.min.y - contentBounds.max.y > viewBounds.size.y && itemTypeEnd > itemTypeStart)
		{
			float y2 = contentBounds.size.y;
			float num5 = (y2 - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines;
			ReturnToTempPool(fromStart: false, itemTypeEnd - itemTypeStart);
			itemTypeEnd = itemTypeStart;
			int num6 = Mathf.FloorToInt((viewBounds.min.y - contentBounds.max.y) / (num5 + contentSpacing));
			if (totalCount >= 0 && itemTypeStart - num6 * contentConstraintCount < 0)
			{
				num6 = Mathf.FloorToInt((float)itemTypeStart / (float)contentConstraintCount);
			}
			itemTypeStart -= num6 * contentConstraintCount;
			if (totalCount >= 0)
			{
				itemTypeStart = Mathf.Max(itemTypeStart, 0);
			}
			itemTypeEnd = itemTypeStart;
			float num7 = (float)num6 * (num5 + contentSpacing);
			RectTransform val2 = m_Content;
			val2.anchoredPosition += new Vector2(0f, num7 + (reverseDirection ? y2 : 0f));
			contentBounds.center += new Vector3(0f, num7 + y2 / 2f, 0f);
			contentBounds.size = Vector3.zero;
			flag = true;
		}
		if (viewBounds.min.y < contentBounds.min.y + m_ContentBottomPadding)
		{
			float num8 = NewItemAtEnd();
			float num9 = num8;
			while (num8 > 0f && viewBounds.min.y < contentBounds.min.y + m_ContentBottomPadding - num9)
			{
				num8 = NewItemAtEnd();
				num9 += num8;
			}
			if (num9 > 0f)
			{
				flag = true;
			}
		}
		else if (itemTypeEnd % contentConstraintCount != 0 && (itemTypeEnd < totalCount || totalCount < 0))
		{
			NewItemAtEnd();
		}
		if (viewBounds.max.y > contentBounds.max.y - m_ContentTopPadding)
		{
			float num10 = NewItemAtStart();
			float num11 = num10;
			while (num10 > 0f && viewBounds.max.y > contentBounds.max.y - m_ContentTopPadding + num11)
			{
				num10 = NewItemAtStart();
				num11 += num10;
			}
			if (num11 > 0f)
			{
				flag = true;
			}
		}
		if (viewBounds.min.y > contentBounds.min.y + threshold + m_ContentBottomPadding)
		{
			float num12 = DeleteItemAtEnd();
			float num13 = num12;
			while (num12 > 0f && viewBounds.min.y > contentBounds.min.y + threshold + m_ContentBottomPadding + num13)
			{
				num12 = DeleteItemAtEnd();
				num13 += num12;
			}
			if (num13 > 0f)
			{
				flag = true;
			}
		}
		if (viewBounds.max.y < contentBounds.max.y - threshold - m_ContentTopPadding)
		{
			float num14 = DeleteItemAtStart();
			float num15 = num14;
			while (num14 > 0f && viewBounds.max.y < contentBounds.max.y - threshold - m_ContentTopPadding - num15)
			{
				num14 = DeleteItemAtStart();
				num15 += num14;
			}
			if (num15 > 0f)
			{
				flag = true;
			}
		}
		if (flag)
		{
			ClearTempPool();
		}
		return flag;
	}
}

}

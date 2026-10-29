using UnityEngine.EventSystems;

namespace UnityEngine.UI
{

[AddComponentMenu("UI/Loop Horizontal Scroll Rect", 50)]
[DisallowMultipleComponent]
public class LoopHorizontalScrollRect : LoopScrollRect
{
	private HorizontalLayoutGroup mHorizontalLayoutGroup;

	private bool mInitHorizontalLayoutGroup;

	private HorizontalLayoutGroup HorizontalLayoutGroup
	{
		get
		{
			if (!mInitHorizontalLayoutGroup && (Object)(object)mHorizontalLayoutGroup == (Object)null)
			{
				mHorizontalLayoutGroup = ((Component)m_Content).GetComponent<HorizontalLayoutGroup>();
				mInitHorizontalLayoutGroup = true;
			}
			return mHorizontalLayoutGroup;
		}
	}

	protected override LoopScrollRectDirection direction { get; set; } = LoopScrollRectDirection.Horizontal;

	protected override float GetSize(RectTransform item, bool includeSpacing)
	{
		float num = (includeSpacing ? contentSpacing : 0f);
		if ((Object)(object)m_GridLayout != (Object)null)
		{
			num += m_GridLayout.cellSize.x;
		}
		else
		{
			num = ((!(bool)HorizontalLayoutGroup || !((HorizontalOrVerticalLayoutGroup)HorizontalLayoutGroup).childControlHeight) ? (num + item.sizeDelta.x) : (num + LoopScrollSizeUtils.GetPreferredWidth(item)));
		}
		return num * ((Transform)m_Content).localScale.x;
	}

	protected override float GetDimension(Vector2 vector)
	{
		return 0f - vector.x;
	}

	protected override float GetAbsDimension(Vector2 vector)
	{
		return vector.x;
	}

	protected override Vector2 GetVector(float value)
	{
		return new Vector2(0f - value, 0f);
	}

	protected override void Awake()
	{
		base.Awake();
		if ((bool)m_Content)
		{
			GridLayoutGroup component = ((Component)m_Content).GetComponent<GridLayoutGroup>();
			if ((Object)(object)component != (Object)null && (int)component.constraint != 2)
			{
				Debug.LogError((object)"[LoopScrollRect] unsupported GridLayoutGroup constraint");
			}
		}
	}

	protected override bool UpdateItems(ref Bounds viewBounds, ref Bounds contentBounds)
	{
		bool flag = false;
		if (viewBounds.size.x < contentBounds.min.x - viewBounds.max.x && itemTypeEnd > itemTypeStart)
		{
			float x = contentBounds.size.x;
			float num = (x - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines;
			ReturnToTempPool(fromStart: false, itemTypeEnd - itemTypeStart);
			itemTypeEnd = itemTypeStart;
			int num2 = Mathf.FloorToInt((contentBounds.min.x - viewBounds.max.x) / (num + contentSpacing));
			if (totalCount >= 0 && itemTypeStart - num2 * contentConstraintCount < 0)
			{
				num2 = Mathf.FloorToInt((float)itemTypeStart / (float)contentConstraintCount);
			}
			itemTypeStart -= num2 * contentConstraintCount;
			if (totalCount >= 0)
			{
				itemTypeStart = Mathf.Max(itemTypeStart, 0);
			}
			itemTypeEnd = itemTypeStart;
			float num3 = (float)num2 * (num + contentSpacing);
			RectTransform val = m_Content;
			val.anchoredPosition -= new Vector2(num3 + (reverseDirection ? x : 0f), 0f);
			contentBounds.center -= new Vector3(num3 + x / 2f, 0f, 0f);
			contentBounds.size = Vector3.zero;
			flag = true;
		}
		if (viewBounds.min.x - contentBounds.max.x > viewBounds.size.x && itemTypeEnd > itemTypeStart)
		{
			int num4 = -1;
			if (totalCount >= 0)
			{
				num4 = Mathf.Max(0, totalCount - (itemTypeEnd - itemTypeStart));
				num4 = num4 / contentConstraintCount * contentConstraintCount;
			}
			float x2 = contentBounds.size.x;
			float num5 = (x2 - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines;
			ReturnToTempPool(fromStart: true, itemTypeEnd - itemTypeStart);
			itemTypeStart = itemTypeEnd;
			int num6 = Mathf.FloorToInt((viewBounds.min.x - contentBounds.max.x) / (num5 + contentSpacing));
			if (num4 >= 0 && itemTypeStart + num6 * contentConstraintCount > num4)
			{
				num6 = Mathf.FloorToInt((float)(num4 - itemTypeStart) / (float)contentConstraintCount);
			}
			itemTypeStart += num6 * contentConstraintCount;
			if (totalCount >= 0)
			{
				itemTypeStart = Mathf.Max(itemTypeStart, 0);
			}
			itemTypeEnd = itemTypeStart;
			float num7 = (float)num6 * (num5 + contentSpacing);
			RectTransform val2 = m_Content;
			val2.anchoredPosition += new Vector2(num7 + (reverseDirection ? 0f : x2), 0f);
			contentBounds.center += new Vector3(num7 + x2 / 2f, 0f, 0f);
			contentBounds.size = Vector3.zero;
			flag = true;
		}
		if (viewBounds.max.x > contentBounds.max.x - m_ContentRightPadding)
		{
			float num8 = NewItemAtEnd();
			float num9 = num8;
			while (num8 > 0f && viewBounds.max.x > contentBounds.max.x - m_ContentRightPadding + num9)
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
		if (viewBounds.min.x < contentBounds.min.x + m_ContentLeftPadding)
		{
			float num10 = NewItemAtStart();
			float num11 = num10;
			while (num10 > 0f && viewBounds.min.x < contentBounds.min.x + m_ContentLeftPadding - num11)
			{
				num10 = NewItemAtStart();
				num11 += num10;
			}
			if (num11 > 0f)
			{
				flag = true;
			}
		}
		if (viewBounds.max.x < contentBounds.max.x - threshold - m_ContentRightPadding)
		{
			float num12 = DeleteItemAtEnd();
			float num13 = num12;
			while (num12 > 0f && viewBounds.max.x < contentBounds.max.x - threshold - m_ContentRightPadding - num13)
			{
				num12 = DeleteItemAtEnd();
				num13 += num12;
			}
			if (num13 > 0f)
			{
				flag = true;
			}
		}
		if (viewBounds.min.x > contentBounds.min.x + threshold + m_ContentLeftPadding)
		{
			float num14 = DeleteItemAtStart();
			float num15 = num14;
			while (num14 > 0f && viewBounds.min.x > contentBounds.min.x + threshold + m_ContentLeftPadding + num15)
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

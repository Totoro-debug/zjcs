// Migrated from the pinned official Framework assembly; only decompiler casts and namespace syntax are adapted.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UGUIJszx
{

[ExecuteAlways]
public abstract class HorizontalOrVerticalLayoutGroupJSZX : LayoutGroup
{
	[SerializeField]
	protected float m_Spacing;

	[SerializeField]
	protected List<float> m_SpacingList = new List<float>();

	[SerializeField]
	protected bool m_ChildForceExpandWidth = true;

	[SerializeField]
	protected bool m_ChildForceExpandHeight = true;

	[SerializeField]
	protected bool m_ChildControlWidth = true;

	[SerializeField]
	protected bool m_ChildControlHeight = true;

	[SerializeField]
	protected bool m_ChildScaleWidth;

	[SerializeField]
	protected bool m_ChildScaleHeight;

	[SerializeField]
	protected bool m_ReverseArrangement;

	public float spacing
	{
		get
		{
			return m_Spacing;
		}
		set
		{
			SetProperty<float>(ref m_Spacing, value);
		}
	}

	public List<float> spacingList
	{
		get
		{
			return m_SpacingList;
		}
		set
		{
			SetProperty<List<float>>(ref m_SpacingList, value);
		}
	}

	public bool childForceExpandWidth
	{
		get
		{
			return m_ChildForceExpandWidth;
		}
		set
		{
			SetProperty<bool>(ref m_ChildForceExpandWidth, value);
		}
	}

	public bool childForceExpandHeight
	{
		get
		{
			return m_ChildForceExpandHeight;
		}
		set
		{
			SetProperty<bool>(ref m_ChildForceExpandHeight, value);
		}
	}

	public bool childControlWidth
	{
		get
		{
			return m_ChildControlWidth;
		}
		set
		{
			SetProperty<bool>(ref m_ChildControlWidth, value);
		}
	}

	public bool childControlHeight
	{
		get
		{
			return m_ChildControlHeight;
		}
		set
		{
			SetProperty<bool>(ref m_ChildControlHeight, value);
		}
	}

	public bool childScaleWidth
	{
		get
		{
			return m_ChildScaleWidth;
		}
		set
		{
			SetProperty<bool>(ref m_ChildScaleWidth, value);
		}
	}

	public bool childScaleHeight
	{
		get
		{
			return m_ChildScaleHeight;
		}
		set
		{
			SetProperty<bool>(ref m_ChildScaleHeight, value);
		}
	}

	public bool reverseArrangement
	{
		get
		{
			return m_ReverseArrangement;
		}
		set
		{
			SetProperty<bool>(ref m_ReverseArrangement, value);
		}
	}

	protected virtual float GetWaveOffset(int index)
	{
		return 0f;
	}

	protected virtual float GetCrossAxisExtraSize()
	{
		return 0f;
	}

	protected void CalcAlongAxis(int axis, bool isVertical)
	{
		float num = ((axis == 0) ? padding.horizontal : padding.vertical);
		bool controlSize = ((axis == 0) ? m_ChildControlWidth : m_ChildControlHeight);
		bool flag = ((axis == 0) ? m_ChildScaleWidth : m_ChildScaleHeight);
		bool childForceExpand = ((axis == 0) ? m_ChildForceExpandWidth : m_ChildForceExpandHeight);
		float num2 = num;
		float num3 = num;
		float num4 = 0f;
		bool flag2 = isVertical ^ (axis == 1);
		int count = rectChildren.Count;
		for (int i = 0; i < count; i++)
		{
			RectTransform val = rectChildren[i];
			GetChildSizes(val, axis, controlSize, childForceExpand, out var min, out var preferred, out var flexible);
			if (flag)
			{
				Vector3 localScale = ((Transform)val).localScale;
				float num5 = localScale[axis];
				min *= num5;
				preferred *= num5;
				flexible *= num5;
			}
			if (flag2)
			{
				float crossAxisExtraSize = GetCrossAxisExtraSize();
				num2 = Mathf.Max(min + num + crossAxisExtraSize, num2);
				num3 = Mathf.Max(preferred + num + crossAxisExtraSize, num3);
				num4 = Mathf.Max(flexible, num4);
			}
			else
			{
				num2 += min + GetSpacing(i);
				num3 += preferred + GetSpacing(i);
				num4 += flexible;
			}
		}
		if (!flag2 && rectChildren.Count > 0)
		{
			num2 -= GetSpacing(rectChildren.Count - 1);
			num3 -= GetSpacing(rectChildren.Count - 1);
		}
		num3 = Mathf.Max(num2, num3);
		SetLayoutInputForAxis(num2, num3, num4, axis);
	}

	private float GetSpacing(int i)
	{
		if (i >= 0 && i < spacingList.Count)
		{
			return spacingList[i];
		}
		return spacing;
	}

	protected void SetChildrenAlongAxis(int axis, bool isVertical)
	{
		Rect rect = rectTransform.rect;
		Vector2 val = rect.size;
		float num = val[axis];
		bool flag = ((axis == 0) ? m_ChildControlWidth : m_ChildControlHeight);
		bool flag2 = ((axis == 0) ? m_ChildScaleWidth : m_ChildScaleHeight);
		bool childForceExpand = ((axis == 0) ? m_ChildForceExpandWidth : m_ChildForceExpandHeight);
		float alignmentOnAxis = GetAlignmentOnAxis(axis);
		bool num2 = isVertical ^ (axis == 1);
		int num3 = (m_ReverseArrangement ? (rectChildren.Count - 1) : 0);
		int num4 = ((!m_ReverseArrangement) ? rectChildren.Count : 0);
		int num5 = ((!m_ReverseArrangement) ? 1 : (-1));
		Vector3 localScale;
		if (num2)
		{
			float num6 = num - (float)((axis == 0) ? padding.horizontal : padding.vertical);
			for (int i = num3; m_ReverseArrangement ? (i >= num4) : (i < num4); i += num5)
			{
				RectTransform val2 = rectChildren[i];
				GetChildSizes(val2, axis, flag, childForceExpand, out var min, out var preferred, out var flexible);
				float num7;
				if (!flag2)
				{
					num7 = 1f;
				}
				else
				{
					localScale = ((Transform)val2).localScale;
					num7 = localScale[axis];
				}
				float num8 = num7;
				float num9 = Mathf.Clamp(num6, min, (flexible > 0f) ? num : preferred);
				float startOffset = GetStartOffset(axis, num9 * num8);
				float waveOffset = GetWaveOffset(i);
				if (flag)
				{
					SetChildAlongAxisWithScale(val2, axis, startOffset + waveOffset, num9, num8);
					continue;
				}
				val = val2.sizeDelta;
				float num10 = (num9 - val[axis]) * alignmentOnAxis;
				SetChildAlongAxisWithScale(val2, axis, startOffset + num10 + waveOffset, num8);
			}
			return;
		}
		float num11 = ((axis == 0) ? padding.left : padding.top);
		float num12 = 0f;
		float num13 = num - GetTotalPreferredSize(axis);
		if (num13 > 0f)
		{
			if (GetTotalFlexibleSize(axis) == 0f)
			{
				num11 = GetStartOffset(axis, GetTotalPreferredSize(axis) - (float)((axis == 0) ? padding.horizontal : padding.vertical));
			}
			else if (GetTotalFlexibleSize(axis) > 0f)
			{
				num12 = num13 / GetTotalFlexibleSize(axis);
			}
		}
		float num14 = 0f;
		if (GetTotalMinSize(axis) != GetTotalPreferredSize(axis))
		{
			num14 = Mathf.Clamp01((num - GetTotalMinSize(axis)) / (GetTotalPreferredSize(axis) - GetTotalMinSize(axis)));
		}
		for (int j = num3; m_ReverseArrangement ? (j >= num4) : (j < num4); j += num5)
		{
			RectTransform val3 = rectChildren[j];
			GetChildSizes(val3, axis, flag, childForceExpand, out var min2, out var preferred2, out var flexible2);
			float num15;
			if (!flag2)
			{
				num15 = 1f;
			}
			else
			{
				localScale = ((Transform)val3).localScale;
				num15 = localScale[axis];
			}
			float num16 = num15;
			float num17 = Mathf.Lerp(min2, preferred2, num14);
			num17 += flexible2 * num12;
			if (flag)
			{
				SetChildAlongAxisWithScale(val3, axis, num11, num17, num16);
			}
			else
			{
				float num18 = num17;
				val = val3.sizeDelta;
				float num19 = (num18 - val[axis]) * alignmentOnAxis;
				SetChildAlongAxisWithScale(val3, axis, num11 + num19, num16);
			}
			num11 += num17 * num16 + GetSpacing(j);
		}
	}

	private void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand, out float min, out float preferred, out float flexible)
	{
		if (!controlSize)
		{
			Vector2 sizeDelta = child.sizeDelta;
			min = sizeDelta[axis];
			preferred = min;
			flexible = 0f;
		}
		else
		{
			min = LayoutUtility.GetMinSize(child, axis);
			preferred = LayoutUtility.GetPreferredSize(child, axis);
			flexible = LayoutUtility.GetFlexibleSize(child, axis);
		}
		if (childForceExpand)
		{
			flexible = Mathf.Max(flexible, 1f);
		}
	}
}

}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LoopScrollGridLayoutGroup : GridLayoutGroup
{
	[SerializeField]
	private bool mFlexible;

	[SerializeField]
	private Vector2 mMinSpacing;

	[SerializeField]
	private int mMinConstraintCount = -1;

	[SerializeField]
	private int mMaxConstraintCount = -1;

	private bool mHorizontalCalculated;

	private bool mVerticalCalculated;

	private bool mInitFlexible;

	protected override void Awake()
	{
		base.Awake();
		InitFlexible();
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		mHorizontalCalculated = false;
		mVerticalCalculated = false;
	}

	public override void CalculateLayoutInputHorizontal()
	{
		if ((int)((GridLayoutGroup)this).constraint == 1 && !mHorizontalCalculated)
		{
			int constraintCount = CalcColumnCount();
			SetConstraintCount(constraintCount);
			base.m_Spacing = GetFlexibleSpacing(base.m_ConstraintCount);
			mHorizontalCalculated = true;
		}
		base.CalculateLayoutInputHorizontal();
	}

	public override void CalculateLayoutInputVertical()
	{
		if ((int)((GridLayoutGroup)this).constraint == 2 && !mVerticalCalculated)
		{
			int constraintCount = CalcRowCount();
			SetConstraintCount(constraintCount);
			base.m_Spacing = GetFlexibleSpacing(base.m_ConstraintCount);
			mVerticalCalculated = true;
		}
		base.CalculateLayoutInputVertical();
	}

	public void InitFlexible()
	{
		if (!mInitFlexible)
		{
			UpdateFlexible();
			mInitFlexible = true;
		}
	}

	private void UpdateFlexible()
	{
		if (mFlexible)
		{
			int flexibleCount = GetFlexibleCount();
			SetConstraintCount(flexibleCount);
			((GridLayoutGroup)this).spacing = GetFlexibleSpacing(base.m_ConstraintCount);
		}
	}

	private int GetFlexibleCount()
	{
		int result = base.m_ConstraintCount;
		Constraint constraint = ((GridLayoutGroup)this).constraint;
		if ((int)constraint != 1)
		{
			if ((int)constraint == 2)
			{
				result = CalcRowCount();
			}
		}
		else
		{
			result = CalcColumnCount();
		}
		return result;
	}

	private Vector2 GetFlexibleSpacing(int count)
	{
		Vector2 spacing = ((GridLayoutGroup)this).spacing;
		if (count == 1)
		{
			return spacing;
		}
		Constraint constraint = ((GridLayoutGroup)this).constraint;
		Rect rect;
		if ((int)constraint != 1)
		{
			if ((int)constraint == 2)
			{
				rect = base.rectTransform.rect;
				float y = rect.size.y;
				spacing.x = (y - (float)count * ((GridLayoutGroup)this).cellSize.y - (float)((LayoutGroup)this).padding.vertical) / (float)(count - 1);
			}
		}
		else
		{
			rect = base.rectTransform.rect;
			float x = rect.size.x;
			spacing.x = (x - (float)count * ((GridLayoutGroup)this).cellSize.x - (float)((LayoutGroup)this).padding.horizontal) / (float)(count - 1);
		}
		return spacing;
	}

	private int CalcColumnCount()
	{
		if ((int)((GridLayoutGroup)this).constraint != 1)
		{
			return base.m_ConstraintCount;
		}
		Rect rect = base.rectTransform.rect;
		float x = rect.size.x;
		return Mathf.Max(1, Mathf.FloorToInt((x - (float)((LayoutGroup)this).padding.horizontal + mMinSpacing.x + 0.001f) / (((GridLayoutGroup)this).cellSize.x + mMinSpacing.x)));
	}

	private int CalcRowCount()
	{
		if ((int)((GridLayoutGroup)this).constraint != 2)
		{
			return base.m_ConstraintCount;
		}
		Rect rect = base.rectTransform.rect;
		float y = rect.size.y;
		if (((GridLayoutGroup)this).cellSize.y + ((GridLayoutGroup)this).spacing.y <= 0f)
		{
			return int.MaxValue;
		}
		return Mathf.Max(1, Mathf.FloorToInt((y - (float)((LayoutGroup)this).padding.vertical + mMinSpacing.y + 0.001f) / (((GridLayoutGroup)this).cellSize.y + mMinSpacing.y)));
	}

	private void SetConstraintCount(int count)
	{
		if (mMinConstraintCount >= 0 && count < mMinConstraintCount)
		{
			count = mMinConstraintCount;
		}
		if (mMaxConstraintCount >= 0 && count > mMaxConstraintCount)
		{
			count = mMaxConstraintCount;
		}
		base.m_ConstraintCount = count;
	}
}

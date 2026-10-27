// Port of the official AwardPanelLayoutElement; only namespace and editor labels adapted.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class AwardPanelLayoutElement : UIBehaviour, ILayoutElement
{
	[SerializeField]
	private GridLayoutGroup mContentLayoutGroup;

	[SerializeField]
	private int mMinRow;

	[SerializeField]
	private int mMaxHeight;

	[SerializeField]
	private int mLayoutPriority;

	[SerializeField]
	private int mCurrentRow;

	private int mCurrentCellNum = -1;

	private float mPreferrenHeight;

	private float mMinHeight;

	private bool mIsDirty = true;

	public float preferredHeight
	{
		get
		{
			ReCalculate();
			return mPreferrenHeight;
		}
	}

	public float minWidth => -1f;

	public float preferredWidth => -1f;

	public float flexibleWidth => -1f;

	public float minHeight
	{
		get
		{
			ReCalculate();
			return mMinHeight;
		}
	}

	public float flexibleHeight => -1f;

	public int layoutPriority => mLayoutPriority;

	protected override void OnEnable()
	{
		mIsDirty = true;
	}

	protected override void OnDisable()
	{
		mIsDirty = true;
	}

	public void CalculateLayoutInputHorizontal()
	{
	}

	public void CalculateLayoutInputVertical()
	{
	}

	public void ReCalculate()
	{
		if (mIsDirty)
		{
			mIsDirty = false;
			if (mCurrentCellNum >= 0)
			{
				mCurrentRow = CalculateRowNumByCellNum(mCurrentCellNum);
			}
			mCurrentRow = ((mCurrentRow < mMinRow) ? mMinRow : mCurrentRow);
			mMinHeight = CalculateHeightByRowNum(mMinRow);
			mPreferrenHeight = CalculateHeightByRowNum(mCurrentRow);
			mPreferrenHeight = ((mPreferrenHeight > (float)mMaxHeight) ? ((float)mMaxHeight) : mPreferrenHeight);
		}
	}

	private float CalculateHeightByRowNum(int rowNum)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		RectOffset padding = ((LayoutGroup)mContentLayoutGroup).padding;
		float num = padding.top + padding.bottom;
		rowNum = ((rowNum >= 0) ? rowNum : 0);
		float num2 = mContentLayoutGroup.cellSize.y * (float)rowNum;
		int num3 = rowNum - 1;
		num3 = ((num3 >= 0) ? num3 : 0);
		float num4 = mContentLayoutGroup.spacing.y * (float)num3;
		return num + num4 + num2;
	}

	private int CalculateRowNumByCellNum(int cellNum)
	{
		((LayoutGroup)mContentLayoutGroup).CalculateLayoutInputHorizontal();
		int num = Mathf.Max(1, mContentLayoutGroup.constraintCount);
		return (cellNum + num - 1) / num;
	}

	public void SetCurrentRowNum(int rowNum)
	{
		if (mCurrentCellNum >= 0 || mCurrentRow != rowNum)
		{
			mCurrentCellNum = -1;
			mCurrentRow = rowNum;
			SetDirty();
		}
	}

	public void SetCurrentRowNumByCellNum(int cellNum)
	{
		if (mCurrentCellNum != cellNum)
		{
			mCurrentCellNum = cellNum;
			SetDirty();
		}
	}

	protected void SetDirty()
	{
		if (((UIBehaviour)this).IsActive())
		{
			Transform transform = ((Component)this).transform;
			LayoutRebuilder.MarkLayoutForRebuild((RectTransform)(object)((transform is RectTransform) ? transform : null));
			mIsDirty = true;
		}
	}
}

}

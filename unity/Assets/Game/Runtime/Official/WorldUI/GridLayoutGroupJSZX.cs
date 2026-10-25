// Migrated from the pinned official Framework assembly; only decompiler syntax and casts are adapted.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UGUIJszx
{

public class GridLayoutGroupJSZX : LayoutGroup
{
	public enum Corner
	{
		UpperLeft,
		UpperRight,
		LowerLeft,
		LowerRight,
		LowerCenter,
		UpperCenter
	}

	public enum Axis
	{
		Horizontal,
		Vertical
	}

	public enum Constraint
	{
		Flexible,
		FixedColumnCount,
		FixedRowCount
	}

	[SerializeField]
	protected Corner m_StartCorner;

	[SerializeField]
	protected Axis m_StartAxis;

	[SerializeField]
	protected Vector2 m_CellSize = new Vector2(100f, 100f);

	[SerializeField]
	protected Vector2 m_Spacing = Vector2.zero;

	[SerializeField]
	protected List<float> m_VerticalSpacingList = new List<float>();

	[SerializeField]
	protected List<float> m_HorizontalSpacingList = new List<float>();

	[SerializeField]
	protected GridLayoutSettings m_gridLayoutSettings;

	[SerializeField]
	protected Constraint m_Constraint;

	[SerializeField]
	protected int m_ConstraintCount = 2;

	public Corner startCorner
	{
		get
		{
			return m_StartCorner;
		}
		set
		{
			this.SetProperty<Corner>(ref m_StartCorner, value);
		}
	}

	public Axis startAxis
	{
		get
		{
			return m_StartAxis;
		}
		set
		{
			this.SetProperty<Axis>(ref m_StartAxis, value);
		}
	}

	public Vector2 cellSize
	{
		get
		{
			return m_CellSize;
		}
		set
		{
			this.SetProperty<Vector2>(ref m_CellSize, value);
		}
	}

	public Vector2 spacing
	{
		get
		{
			return m_Spacing;
		}
		set
		{
			this.SetProperty<Vector2>(ref m_Spacing, value);
		}
	}

	public List<float> verticalSpacingList
	{
		get
		{
			return m_VerticalSpacingList;
		}
		set
		{
			this.SetProperty<List<float>>(ref m_VerticalSpacingList, value);
		}
	}

	public List<float> horizontalSpacingList
	{
		get
		{
			return m_HorizontalSpacingList;
		}
		set
		{
			this.SetProperty<List<float>>(ref m_HorizontalSpacingList, value);
		}
	}

	public GridLayoutSettings gridLayoutSettings
	{
		get
		{
			return m_gridLayoutSettings;
		}
		set
		{
			this.SetProperty<GridLayoutSettings>(ref m_gridLayoutSettings, value);
		}
	}

	public Constraint constraint
	{
		get
		{
			return m_Constraint;
		}
		set
		{
			this.SetProperty<Constraint>(ref m_Constraint, value);
		}
	}

	public int constraintCount
	{
		get
		{
			return m_ConstraintCount;
		}
		set
		{
			this.SetProperty<int>(ref m_ConstraintCount, Mathf.Max(1, value));
		}
	}

	public override void CalculateLayoutInputHorizontal()
	{
		base.CalculateLayoutInputHorizontal();
		CheckLayout(GetGridLayoutSetting());
		int num = 0;
		int num2 = 0;
		if (m_Constraint == Constraint.FixedColumnCount)
		{
			num = (num2 = m_ConstraintCount);
		}
		else if (m_Constraint == Constraint.FixedRowCount)
		{
			num = (num2 = Mathf.CeilToInt((float)this.rectChildren.Count / (float)m_ConstraintCount - 0.001f));
		}
		else
		{
			num = 1;
			num2 = Mathf.CeilToInt(Mathf.Sqrt((float)this.rectChildren.Count));
		}
		this.SetLayoutInputForAxis((float)this.padding.horizontal + cellSize.x * (float)num + GetHorizontalTotalSpacing(num), (float)this.padding.horizontal + cellSize.x * (float)num2 + GetHorizontalTotalSpacing(num2), -1f, 0);
	}

	public override void CalculateLayoutInputVertical()
	{
		CheckLayout(GetGridLayoutSetting());
		int num = 0;
		if (m_Constraint == Constraint.FixedColumnCount)
		{
			num = Mathf.CeilToInt((float)this.rectChildren.Count / (float)m_ConstraintCount - 0.001f);
		}
		else if (m_Constraint == Constraint.FixedRowCount)
		{
			num = m_ConstraintCount;
		}
		else
		{
			Rect rect = this.rectTransform.rect;
			float maxWidth = rect.size.x - (float)this.padding.horizontal + 0.001f;
			int horizontalCellCount = GetHorizontalCellCount(maxWidth);
			num = Mathf.CeilToInt((float)this.rectChildren.Count / (float)horizontalCellCount);
		}
		float num2 = (float)this.padding.vertical + cellSize.y * (float)num + GetVerticalTotalSpacing(num);
		this.SetLayoutInputForAxis(num2, num2, -1f, 1);
	}

	public override void SetLayoutHorizontal()
	{
		SetCellsAlongAxis(0);
	}

	public override void SetLayoutVertical()
	{
		SetCellsAlongAxis(1);
	}

	private void SetCellsAlongAxis(int axis)
	{
		GridLayoutSetting gridLayoutSetting = GetGridLayoutSetting();
		CheckLayout(gridLayoutSetting);
		if (axis == 0)
		{
			for (int i = 0; i < this.rectChildren.Count; i++)
			{
				RectTransform val = this.rectChildren[i];
				base.m_Tracker.Add(this, val, (DrivenTransformProperties)16134);
				val.anchorMin = Vector2.up;
				val.anchorMax = Vector2.up;
				val.sizeDelta = cellSize;
			}
			return;
		}
		Rect rect = this.rectTransform.rect;
		float x = rect.size.x;
		rect = this.rectTransform.rect;
		float y = rect.size.y;
		int num = 1;
		int num2 = 1;
		if (m_Constraint == Constraint.FixedColumnCount)
		{
			num = m_ConstraintCount;
			num2 = Mathf.CeilToInt((float)this.rectChildren.Count / (float)num - 0.001f);
		}
		else if (m_Constraint != Constraint.FixedRowCount)
		{
			num = ((!(cellSize.x + spacing.x <= 0f)) ? GetHorizontalCellCount(x - (float)this.padding.horizontal + 0.001f) : int.MaxValue);
			num2 = ((!(cellSize.y + spacing.y <= 0f)) ? GetVerticalCellCount(y - (float)this.padding.vertical + 0.001f) : int.MaxValue);
		}
		else
		{
			num2 = m_ConstraintCount;
			num = Mathf.CeilToInt((float)this.rectChildren.Count / (float)num2 - 0.001f);
		}
		int num3 = ((startCorner != Corner.LowerLeft && startCorner != Corner.UpperLeft && startCorner != Corner.LowerCenter && startCorner != Corner.UpperCenter) ? 1 : 0);
		int num4 = ((startCorner == Corner.LowerLeft || startCorner == Corner.LowerRight || startCorner == Corner.LowerCenter) ? 1 : 0);
		int num5;
		int num6;
		int num7;
		if (startAxis == Axis.Horizontal)
		{
			num5 = num;
			num6 = Mathf.Clamp(num, 1, this.rectChildren.Count);
			num7 = Mathf.Clamp(num2, 1, Mathf.CeilToInt((float)this.rectChildren.Count / (float)num5));
		}
		else
		{
			num5 = num2;
			num7 = Mathf.Clamp(num2, 1, this.rectChildren.Count);
			num6 = Mathf.Clamp(num, 1, Mathf.CeilToInt((float)this.rectChildren.Count / (float)num5));
		}
		int num8 = this.rectChildren.Count % num5;
		int num9 = ((num8 == 0) ? num5 : num8);
		int num10 = ((startAxis == Axis.Horizontal) ? num9 : num6);
		int num11 = ((startAxis == Axis.Vertical) ? num9 : num7);
		Vector2 val2 = new Vector2((float)num10 * cellSize.x + GetHorizontalTotalSpacing(num10), (float)num11 * cellSize.y + GetVerticalTotalSpacing(num11));
		Vector2 val3 = new Vector2(this.GetStartOffset(0, val2.x), this.GetStartOffset(1, val2.y));
		List<Vector2> list = new List<Vector2>();
		if (gridLayoutSetting != null)
		{
			for (int j = 0; j < gridLayoutSetting.lineCount.Count; j++)
			{
				int num12 = gridLayoutSetting.lineCount[j];
				int num13 = ((startAxis == Axis.Horizontal) ? num12 : num6);
				int num14 = ((startAxis == Axis.Vertical) ? num12 : num7);
				Vector2 val4 = new Vector2((float)num13 * cellSize.x + GetHorizontalTotalSpacing(num13), (float)num14 * cellSize.y + GetVerticalTotalSpacing(num14));
				list.Add(new Vector2(this.GetStartOffset(0, val4.x), this.GetStartOffset(1, val4.y)));
			}
		}
		Vector2 val5 = new Vector2((float)num6 * cellSize.x + GetHorizontalTotalSpacing(num6), (float)num7 * cellSize.y + GetVerticalTotalSpacing(num7));
		Vector2 val6 = new Vector2(this.GetStartOffset(0, val5.x), this.GetStartOffset(1, val5.y));
		for (int k = 0; k < this.rectChildren.Count; k++)
		{
			Vector2 val7 = val6;
			if (startCorner == Corner.LowerCenter || startCorner == Corner.UpperCenter)
			{
				val7 = ((k + 1 > this.rectChildren.Count - num9) ? val3 : val6);
			}
			int num15;
			int num16;
			if (startAxis == Axis.Horizontal)
			{
				if (gridLayoutSetting != null)
				{
					int line = GetLine(gridLayoutSetting, k);
					num15 = GetPos(gridLayoutSetting, line, k);
					num16 = line;
					if (startCorner == Corner.LowerCenter || startCorner == Corner.UpperCenter)
					{
						val7 = ((num5 > gridLayoutSetting.lineCount[line]) ? list[line] : val6);
					}
				}
				else
				{
					num15 = k % num5;
					num16 = k / num5;
				}
			}
			else if (gridLayoutSetting != null)
			{
				int line2 = GetLine(gridLayoutSetting, k);
				num15 = line2;
				num16 = GetPos(gridLayoutSetting, line2, k);
				if (startCorner == Corner.LowerCenter || startCorner == Corner.UpperCenter)
				{
					val7 = ((num5 > gridLayoutSetting.lineCount[line2]) ? list[line2] : val6);
				}
			}
			else
			{
				num15 = k / num5;
				num16 = k % num5;
			}
			if (num3 == 1)
			{
				num15 = num6 - 1 - num15;
			}
			if (num4 == 1)
			{
				num16 = num7 - 1 - num16;
			}
			RectTransform val8 = this.rectChildren[k];
			float x2 = val7.x;
			Vector2 val9 = cellSize;
			float num17 = x2 + val9[0] * (float)num15 + GetHorizontalTotalSpacing(num15 + 1);
			val9 = cellSize;
			this.SetChildAlongAxis(val8, 0, num17, val9[0]);
			RectTransform val10 = this.rectChildren[k];
			float y2 = val7.y;
			val9 = cellSize;
			float num18 = y2 + val9[1] * (float)num16 + GetVerticalTotalSpacing(num16 + 1);
			val9 = cellSize;
			this.SetChildAlongAxis(val10, 1, num18, val9[1]);
		}
	}

	private void CheckLayout(GridLayoutSetting gridLayoutSetting)
	{
		if (gridLayoutSetting != null)
		{
			if (startAxis == Axis.Horizontal)
			{
				constraint = Constraint.FixedRowCount;
			}
			else if (startAxis == Axis.Vertical)
			{
				constraint = Constraint.FixedColumnCount;
			}
			constraintCount = gridLayoutSetting.lineCount.Count;
		}
	}

	private GridLayoutSetting GetGridLayoutSetting()
	{
		GridLayoutSetting result = null;
		if (gridLayoutSettings != null)
		{
			foreach (GridLayoutSetting setting in gridLayoutSettings.settings)
			{
				if (setting.childrenCount == this.rectChildren.Count)
				{
					result = setting;
				}
			}
		}
		return result;
	}

	private float GetHorizontalSpacing(int i)
	{
		if (i >= 0 && i < horizontalSpacingList.Count)
		{
			return horizontalSpacingList[i];
		}
		return spacing.x;
	}

	private float GetVerticalSpacing(int i)
	{
		if (i >= 0 && i < verticalSpacingList.Count)
		{
			return verticalSpacingList[i];
		}
		return spacing.y;
	}

	private int GetHorizontalCellCount(float maxWidth)
	{
		float num = 0f;
		int num2 = 0;
		while (num <= maxWidth)
		{
			num += cellSize.x + GetHorizontalSpacing(num2);
			num2++;
		}
		return Mathf.Max(1, num2 - 1);
	}

	private int GetVerticalCellCount(float maxHeight)
	{
		float num = 0f;
		int num2 = 0;
		while (num <= maxHeight)
		{
			num += cellSize.x + GetVerticalSpacing(num2);
			num2++;
		}
		return Mathf.Max(1, num2 - 1);
	}

	private float GetHorizontalTotalSpacing(int col)
	{
		float num = 0f;
		for (int i = 0; i < col - 1; i++)
		{
			num += GetHorizontalSpacing(i);
		}
		return num;
	}

	private float GetVerticalTotalSpacing(int row)
	{
		float num = 0f;
		for (int i = 0; i < row - 1; i++)
		{
			num += GetVerticalSpacing(i);
		}
		return num;
	}

	private int GetLine(GridLayoutSetting gridLayoutSetting, int i)
	{
		int num = 0;
		for (int j = 0; j < gridLayoutSetting.lineCount.Count; j++)
		{
			if (num > i)
			{
				return j - 1;
			}
			num += gridLayoutSetting.lineCount[j];
		}
		return gridLayoutSetting.lineCount.Count - 1;
	}

	private int GetPos(GridLayoutSetting gridLayoutSetting, int line, int i)
	{
		int num = 0;
		for (int j = 0; j < line; j++)
		{
			num += gridLayoutSetting.lineCount[j];
		}
		return i - num;
	}

	public GridLayoutGroupJSZX()
	{
	}
}

}

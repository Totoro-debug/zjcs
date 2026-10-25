// Migrated from the pinned official Framework assembly; only decompiler casts and namespace syntax are adapted.
using UnityEngine.UI;

namespace UGUIJszx
{

public class HorizontalLayoutGroupJSZX : HorizontalOrVerticalLayoutGroupJSZX
{
	public override void CalculateLayoutInputHorizontal()
	{
		base.CalculateLayoutInputHorizontal();
		CalcAlongAxis(0, isVertical: false);
	}

	public override void CalculateLayoutInputVertical()
	{
		CalcAlongAxis(1, isVertical: false);
	}

	public override void SetLayoutHorizontal()
	{
		SetChildrenAlongAxis(0, isVertical: false);
	}

	public override void SetLayoutVertical()
	{
		SetChildrenAlongAxis(1, isVertical: false);
	}
}

}

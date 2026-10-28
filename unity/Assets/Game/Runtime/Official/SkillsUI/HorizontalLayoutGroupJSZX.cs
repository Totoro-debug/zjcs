// Original framework-source/UGUIJszx/HorizontalLayoutGroupJSZX.cs; logging and local UI service bindings adapted.
using UnityEngine.UI;


namespace Zjcs.Official.SkillsUI
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

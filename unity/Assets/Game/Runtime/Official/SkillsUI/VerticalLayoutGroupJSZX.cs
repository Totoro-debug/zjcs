// Original framework-source/UGUIJszx/VerticalLayoutGroupJSZX.cs; logging and local UI service bindings adapted.
using UnityEngine.UI;


namespace Zjcs.Official.SkillsUI
{
public class VerticalLayoutGroupJSZX : HorizontalOrVerticalLayoutGroupJSZX
{
	protected VerticalLayoutGroupJSZX()
	{
	}

	public override void CalculateLayoutInputHorizontal()
	{
		base.CalculateLayoutInputHorizontal();
		CalcAlongAxis(0, isVertical: true);
	}

	public override void CalculateLayoutInputVertical()
	{
		CalcAlongAxis(1, isVertical: true);
	}

	public override void SetLayoutHorizontal()
	{
		SetChildrenAlongAxis(0, isVertical: true);
	}

	public override void SetLayoutVertical()
	{
		SetChildrenAlongAxis(1, isVertical: true);
	}
}
}

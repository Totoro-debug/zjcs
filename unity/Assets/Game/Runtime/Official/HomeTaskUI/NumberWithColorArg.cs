// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{


public class NumberWithColorArg : ArgBase
{
	private CultureNumberArg mInner;

	public long NumberValue { get; }

	public string Color { get; }

	public NumberWithColorArg(long val, string color)
	{
		NumberValue = val;
		Color = color;
		mInner = CultureNumberArg.Compact(val);
	}

	public override string ToString()
	{
		return $"<color={Color}>{mInner}</color>";
	}
}
}

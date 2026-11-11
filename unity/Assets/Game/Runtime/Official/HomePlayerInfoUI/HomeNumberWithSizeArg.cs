// Selected original player-info implementation; only service boundaries and C# decompilation repairs differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerInfoUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;

public class HomeNumberWithSizeArg : ArgBase
{
	public long NumberValue { get; }

	public int Size { get; }

	public HomeNumberWithSizeArg(long val, int size)
	{
		NumberValue = val;
		Size = size;
	}

	public override string ToString()
	{
		var (arg, arg2) = Culture.Number.GetNumberAndUnit(NumberValue);
		return $"<size={Size}>{arg}</size>{arg2}";
	}
}

}

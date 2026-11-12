// Original quantity control; language/color/input services explicitly bound.
namespace Zjcs.Official.HomeCountControlsUI
{
using Object=UnityEngine.Object;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
internal static class MathUtils
{
	public static long Clamp(long val, long min, long max)
	{
		return Math.Max(min, Math.Min(max, val));
	}
}

}

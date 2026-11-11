// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
internal static class MathUtils
{
	public static long Clamp(long val, long min, long max)
	{
		return Math.Max(min, Math.Min(max, val));
	}
}

}

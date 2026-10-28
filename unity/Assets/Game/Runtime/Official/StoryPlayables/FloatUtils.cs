// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;

namespace Zjcs.Official.StoryPlayables
{

public static class FloatUtils
{
	public static bool IsEqual(float a, float b)
	{
		if (a != b)
		{
			return Math.Abs(a - b) < float.Epsilon;
		}
		return true;
	}

	public static bool GreaterThan(float a, float b)
	{
		if (a > b)
		{
			return !IsEqual(a, b);
		}
		return false;
	}

	public static bool GreaterThanOrEqual(float a, float b)
	{
		if (!(a > b))
		{
			return IsEqual(a, b);
		}
		return true;
	}

	public static bool IsZero(float a)
	{
		return Math.Abs(a) < float.Epsilon;
	}

	public static bool LessThan(float a, float b)
	{
		if (a < b)
		{
			return !IsEqual(a, b);
		}
		return false;
	}

	public static bool LessThanOrEqual(float a, float b)
	{
		if (!(a < b))
		{
			return IsEqual(a, b);
		}
		return true;
	}
}

}

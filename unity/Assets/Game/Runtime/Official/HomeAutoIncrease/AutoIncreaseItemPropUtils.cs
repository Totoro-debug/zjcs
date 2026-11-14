// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
public static class AutoIncreaseItemPropUtils
{
	public static double CalcOffsetOutputAdditionTimeDuration(int propValue, double timeDuration)
	{
		if (propValue == 0)
		{
			return timeDuration;
		}
		float num = Formula.Int2Float(propValue);
		return timeDuration / (double)(1f + num);
	}
}
}

// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
internal static class TimeUtils
{
	public static double SecondToHour(double sec)
	{
		return sec / 3600.0;
	}
	public static double HourToSecond(double hour)
	{
		return hour * 3600.0;
	}
}

}

// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
public sealed class HomeAutoIncreaseClock
{
    private readonly Func<DateTime> now;
    public HomeAutoIncreaseClock(Func<DateTime> now) { this.now = now ?? throw new ArgumentNullException(nameof(now)); }
    public DateTime GetDateTime() => now();
	public double CalcElapsedHour(DateTime startTime)
	{
		return Math.Max(0.0, (GetDateTime() - startTime).TotalHours);
	}
	public double CalcElapsedSecond(DateTime startTime)
	{
		return Math.Max(0.0, (GetDateTime() - startTime).TotalSeconds);
	}
}

}

// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
public struct SegmentedIncreaseData
{
	public DateTime StartTime;

	public DateTime EndTime;

	public double IncreaseSpeed;

	public long MaxCount;
}
}

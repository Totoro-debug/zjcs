// Selected Console.dll; explicit data, clock, config and disabled-function ports.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeOutputAddition
{
public static class HomeHangUpOutputAddition
{
	public static OutputAdditionPropType GetOutputAdditionAddPercentType(this HomeOutputAdditionPlayer player, int hangUpId)
	{
		if (player.Settings.HomeHangUpAddPercentTypeDict.TryGetValue(hangUpId, out var value))
		{
			return value;
		}
		return OutputAdditionPropType.None;
	}

	public static OutputAdditionPropType GetOutputAdditionMaxStoreType(this HomeOutputAdditionPlayer player, int hangUpId)
	{
		if (player.Settings.HomeHangUpMaxStoreTypeDict.TryGetValue(hangUpId, out var value))
		{
			return value;
		}
		return OutputAdditionPropType.None;
	}

	public static long CalcOutputAdditionExtStore(this HomeOutputAdditionPlayer player, int hangUpId, long maxCount)
	{
		if (maxCount <= 0)
		{
			return 0L;
		}
		OutputAdditionPropType outputAdditionMaxStoreType = player.GetOutputAdditionMaxStoreType(hangUpId);
		int outputAdditionPropValue = player.GetOutputAdditionPropValue(outputAdditionMaxStoreType);
		if (outputAdditionPropValue <= 0)
		{
			return 0L;
		}
		return (long)((float)maxCount * Formula.Int2Float(outputAdditionPropValue));
	}

	public static long CalcOutputAdditionExtCount(this HomeOutputAdditionPlayer player, int hangUpId, double count)
	{
		if (count <= 0.0)
		{
			return 0L;
		}
		OutputAdditionPropType outputAdditionAddPercentType = player.GetOutputAdditionAddPercentType(hangUpId);
		int outputAdditionPropValue = player.GetOutputAdditionPropValue(outputAdditionAddPercentType);
		if (outputAdditionPropValue <= 0)
		{
			return 0L;
		}
		return (long)(count * (double)Formula.Int2Float(outputAdditionPropValue));
	}

}
}

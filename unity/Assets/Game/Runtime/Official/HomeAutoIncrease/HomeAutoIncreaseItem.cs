// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
public sealed class HomeAutoIncreaseItem
{
    private long _Count;
    public ItemParamAutoIncrease Parameter { get; }
    public HomeAutoIncreaseClock Clock { get; }
    public long ItemCountLimit { get; }
    public long Count => GetActualCountWithAutoIncrease();
    public long StoredCount => _Count;
    public Func<HomeAutoIncreaseItem, DateTime, DateTime, List<SegmentedIncreaseData>> Segments;
    public Action<Exception> LogException;
    public event Action<HomeAutoIncreaseItem, long, long, DateTime, double> OnAutoIncreaseCountUpdate;
    public event Action<HomeAutoIncreaseItem, double, double> OnAutoIncreaseSpeedUpdate;
    public event Action<HomeAutoIncreaseItem, long, long> OnAutoIncreaseMaxCountUpdate;
    public HomeAutoIncreaseItem(long count, ItemParamAutoIncrease parameter, long itemCountLimit, Func<DateTime> now)
    {
        _Count = count; Parameter = parameter; ItemCountLimit = itemCountLimit;
        Clock = new HomeAutoIncreaseClock(now);
    }
	public void SetCount(long count)
	{
		SetCountWithAutoIncrease(count);
	}

	private long GetActualCountWithAutoIncrease()
	{
		long count = _Count;
		ItemParamAutoIncrease itemParamAutoIncrease = Parameter;
		if (itemParamAutoIncrease == null)
		{
			return count;
		}
		List<SegmentedIncreaseData> list = Segments?.Invoke(this, itemParamAutoIncrease.LastUpdateTime, Clock.GetDateTime());
		if (list != null)
		{
			return (long)AutoIncreaseItemUtils.GetActualCountWithAutoIncreaseSegmented(itemParamAutoIncrease, count, list);
		}
		double increaseSpeed = itemParamAutoIncrease.IncreaseSpeed;
		long maxCount = itemParamAutoIncrease.MaxCount;
		if (increaseSpeed > 0.0 && count >= maxCount)
		{
			return count;
		}
		double hour = Clock.CalcElapsedHour(itemParamAutoIncrease.LastUpdateTime) + TimeUtils.SecondToHour(itemParamAutoIncrease.BonusSeconds);
		return MathUtils.Clamp((long)Math.Floor((double)count + AutoIncreaseItemUtils.CalcAutoIncreaseCount(increaseSpeed, hour) + itemParamAutoIncrease.Decimal), long.MinValue, maxCount);
	}

	private void SetCountWithAutoIncrease(long value)
	{
		ItemParamAutoIncrease itemParamAutoIncrease = Parameter;
		DateTime arg = default;
		double arg2 = 0.0;
		if (itemParamAutoIncrease != null)
		{
			double num = 0.0;
			double increaseSpeed = itemParamAutoIncrease.IncreaseSpeed;
			List<SegmentedIncreaseData> list = Segments?.Invoke(this, itemParamAutoIncrease.LastUpdateTime, Clock.GetDateTime());
			if (list != null)
			{
				num = AutoIncreaseItemUtils.GetActualCountWithAutoIncreaseSegmented(itemParamAutoIncrease, _Count, list);
			}
			else
			{
				double hour = Clock.CalcElapsedHour(itemParamAutoIncrease.LastUpdateTime) + TimeUtils.SecondToHour(itemParamAutoIncrease.BonusSeconds);
				num = (double)_Count + AutoIncreaseItemUtils.CalcAutoIncreaseCount(increaseSpeed, hour) + itemParamAutoIncrease.Decimal;
			}
			if ((increaseSpeed > 0.0 && num < (double)itemParamAutoIncrease.MaxCount) || (increaseSpeed < 0.0 && num >= 0.0) || increaseSpeed == 0.0)
			{
				itemParamAutoIncrease.Decimal = num - (double)(long)Math.Floor(num);
			}
			else
			{
				itemParamAutoIncrease.Decimal = 0.0;
			}
			arg = itemParamAutoIncrease.LastUpdateTime;
			arg2 = itemParamAutoIncrease.BonusSeconds;
			itemParamAutoIncrease.LastUpdateTime = Clock.GetDateTime();
			itemParamAutoIncrease.BonusSeconds = 0.0;
		}
		long num2 = value;
		if (itemParamAutoIncrease != null)
		{
			long maxCount = ItemCountLimit;
			if (value > maxCount)
			{
				num2 = maxCount;
			}
		}
		if (itemParamAutoIncrease != null && _Count != num2)
		{
			try
			{
				OnAutoIncreaseCountUpdate?.Invoke(this, _Count, num2, arg, arg2);
			}
			catch (Exception exception)
			{
				LogException?.Invoke(exception);
			}
		}
		_Count = num2;
	}

	public void UpdateAutoIncreaseSpeed(double speed)
	{
		ItemParamAutoIncrease itemParamAutoIncrease = Parameter;
		if (itemParamAutoIncrease == null)
		{
			return;
		}
		double increaseSpeed = itemParamAutoIncrease.IncreaseSpeed;
		if (Math.Abs(increaseSpeed - speed) < 1E-06)
		{
			return;
		}
		itemParamAutoIncrease.IncreaseSpeed = speed;
		try
		{
			OnAutoIncreaseSpeedUpdate?.Invoke(this, increaseSpeed, speed);
		}
		catch (Exception exception)
		{
			LogException?.Invoke(exception);
		}
	}

	public void UpdateAutoIncreaseMaxCount(long maxCount)
	{
		ItemParamAutoIncrease itemParamAutoIncrease = Parameter;
		if (itemParamAutoIncrease == null)
		{
			return;
		}
		long maxCount2 = itemParamAutoIncrease.MaxCount;
		if (maxCount2 == maxCount)
		{
			return;
		}
		itemParamAutoIncrease.MaxCount = maxCount;
		try
		{
			OnAutoIncreaseMaxCountUpdate?.Invoke(this, maxCount2, maxCount);
		}
		catch (Exception exception)
		{
			LogException?.Invoke(exception);
		}
	}
}

}

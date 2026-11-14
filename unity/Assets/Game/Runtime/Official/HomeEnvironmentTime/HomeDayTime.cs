using System;

namespace Zjcs.Official.HomeEnvironmentTime
{
[Serializable]
public struct HomeDayTime : IComparable<HomeDayTime>
{
	public int Hour;

	public int Minute;

	public int Second;

	public float GetDayProgress(HomeEnvironmentConfig config)
	{
		return 1f * (float)GetSecondsSinceStartOfDay(config) / (float)config.SecondsPerDay;
	}

	public int GetSecondsSinceStartOfDay(HomeEnvironmentConfig config)
	{
		return GetSecondsSinceStartOfDay(config.SecondsPerMinute, config.MinutesPerHour);
	}

	public int GetSecondsSinceStartOfDay(int secondsPerMinute, int minutesPerHour)
	{
		return (Hour * minutesPerHour + Minute) * secondsPerMinute + Second;
	}

	public HomeDayTime AddSeconds(HomeEnvironmentConfig config, int addSeconds)
	{
		HomeDayTime result = this;
		result.Second += addSeconds;
		int num = result.Second / config.SecondsPerMinute;
		result.Second -= num * config.SecondsPerMinute;
		result.Minute += num;
		int num2 = result.Minute / config.MinutesPerHour;
		result.Minute -= num2 * config.MinutesPerHour;
		result.Hour += num2;
		result.Hour %= config.HoursPerDay;
		return result;
	}

	public int CompareTo(HomeDayTime other)
	{
		int num = Hour.CompareTo(other.Hour);
		if (num != 0)
		{
			return num;
		}
		int num2 = Minute.CompareTo(other.Minute);
		if (num2 != 0)
		{
			return num2;
		}
		return Second.CompareTo(other.Second);
	}

	public static bool operator <(HomeDayTime lhs, HomeDayTime rhs)
	{
		return lhs.CompareTo(rhs) < 0;
	}

	public static bool operator >(HomeDayTime lhs, HomeDayTime rhs)
	{
		return lhs.CompareTo(rhs) > 0;
	}

	public static bool operator <=(HomeDayTime lhs, HomeDayTime rhs)
	{
		return lhs.CompareTo(rhs) <= 0;
	}

	public static bool operator >=(HomeDayTime lhs, HomeDayTime rhs)
	{
		return lhs.CompareTo(rhs) >= 0;
	}

	public static bool operator ==(HomeDayTime lhs, HomeDayTime rhs)
	{
		return lhs.CompareTo(rhs) == 0;
	}

	public static bool operator !=(HomeDayTime lhs, HomeDayTime rhs)
	{
		return !(lhs == rhs);
	}

	public override bool Equals(object obj)
	{
		if (obj is HomeDayTime homeDayTime)
		{
			return this == homeDayTime;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<int, int, int>(Hour, Minute, Second);
	}

	public override string ToString()
	{
		return $"{Hour}:{Minute}:{Second}";
	}
}

}

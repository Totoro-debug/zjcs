namespace Zjcs.Official.HomeEnvironmentTime
{
public class HomeDateTime
{
	public readonly int SecondsPerMinute;

	public readonly int MinutesPerHour;

	public readonly int HoursPerDay;

	public readonly int DaysPerMouth;

	public readonly int MonthsPerYear;

	public readonly int SecondsPerHour;

	public readonly int SecondsPerDay;

	public readonly int SecondsPerMonth;

	public readonly int SecondsPerYear;

	private int mYear;

	private int mMonth;

	private int mDay;

	private int mHour;

	private int mMinute;

	private double mSecond;

	public int Year => mYear;

	public int Month => mMonth;

	public int Day => mDay;

	public int Hour => mHour;

	public int Minute => mMinute;

	public int Second => (int)mSecond;

	public double TotalSeconds => (double)((long)mYear * (long)SecondsPerYear + mMonth * SecondsPerMonth + mDay * SecondsPerDay + mHour * SecondsPerHour + mMinute * SecondsPerMinute) + mSecond;

	public int TotalMonth => mYear * MonthsPerYear + mMonth;

	public int TotalDays => (mYear * MonthsPerYear + mMonth) * DaysPerMouth + mDay;

	public HomeDateTime(int secondsPerMinute, int minutesPerHour, int hoursPerDay, int daysPerMouth, int monthsPerYear, double totalSeconds)
	{
		SecondsPerMinute = secondsPerMinute;
		MinutesPerHour = minutesPerHour;
		HoursPerDay = hoursPerDay;
		DaysPerMouth = daysPerMouth;
		MonthsPerYear = monthsPerYear;
		SecondsPerHour = minutesPerHour * SecondsPerMinute;
		SecondsPerDay = hoursPerDay * SecondsPerHour;
		SecondsPerMonth = daysPerMouth * SecondsPerDay;
		SecondsPerYear = monthsPerYear * SecondsPerMonth;
		SetInitTime(totalSeconds);
	}

	public HomeDateTime(HomeDateTime homeDateTime)
		: this(homeDateTime.SecondsPerMinute, homeDateTime.MinutesPerHour, homeDateTime.HoursPerDay, homeDateTime.DaysPerMouth, homeDateTime.MonthsPerYear, homeDateTime.TotalSeconds)
	{
	}

	public void Update(float deltaTime)
	{
		mSecond += deltaTime;
		if (TryAddNextLevelTime(ref mSecond, SecondsPerMinute, ref mMinute) && TryAddNextLevelTime(ref mMinute, MinutesPerHour, ref mHour) && TryAddNextLevelTime(ref mHour, HoursPerDay, ref mDay) && TryAddNextLevelTime(ref mDay, DaysPerMouth, ref mMonth))
		{
			TryAddNextLevelTime(ref mMonth, MonthsPerYear, ref mYear);
		}
	}

	private void SetInitTime(double totalSeconds)
	{
		long remainSeconds = (long)totalSeconds;
		double num = totalSeconds - (double)remainSeconds;
		CalcTimeBySeconds(out mYear, ref remainSeconds, SecondsPerYear);
		CalcTimeBySeconds(out mMonth, ref remainSeconds, SecondsPerMonth);
		CalcTimeBySeconds(out mDay, ref remainSeconds, SecondsPerDay);
		CalcTimeBySeconds(out mHour, ref remainSeconds, SecondsPerHour);
		CalcTimeBySeconds(out mMinute, ref remainSeconds, SecondsPerMinute);
		mSecond = (double)remainSeconds + num;
	}

	private void CalcTimeBySeconds(out int unitTime, ref long remainSeconds, int perSecondsToNext)
	{
		long num = remainSeconds / perSecondsToNext;
		remainSeconds -= num * perSecondsToNext;
		unitTime = (int)num;
	}

	private bool TryAddNextLevelTime(ref int curLevelTime, int perTimeToNext, ref int nextLevelTime)
	{
		if (curLevelTime >= perTimeToNext)
		{
			int num = curLevelTime / perTimeToNext;
			nextLevelTime += num;
			curLevelTime -= num * perTimeToNext;
			return true;
		}
		return false;
	}

	private bool TryAddNextLevelTime(ref double curLevelTime, int perTimeToNext, ref int nextLevelTime)
	{
		int curLevelTime2 = (int)curLevelTime;
		double num = curLevelTime - (double)curLevelTime2;
		bool result = TryAddNextLevelTime(ref curLevelTime2, perTimeToNext, ref nextLevelTime);
		curLevelTime = (double)curLevelTime2 + num;
		return result;
	}

	public HomeDayTime GetHomeDayTime()
	{
		return new HomeDayTime
		{
			Hour = mHour,
			Minute = mMinute,
			Second = (int)mSecond
		};
	}

	public override string ToString()
	{
		return $"{mYear}/{mMonth}/{mDay} {mHour}:{mMinute}:{(int)mSecond}";
	}
}

}

// Selected original currency-window implementation; typed local service boundaries.
using Object=UnityEngine.Object;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
using StateSwitcher=Zjcs.Official.WorldUI.WorldStateSwitcher<bool>;
using PlayerItemDataWrap=Zjcs.Official.ProfessionUI.IProfessionItemData;
using AwardSourceData=System.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeCurrencyWindowsUI
{
public sealed class HomeQuickBuyCalendar : IHomeQuickBuyCalendar
{
    public readonly DateTime DailyResetTime;
    public readonly int TimeZone;
    public readonly DayOfWeek WeeklyStartDayOfWeek;
    static readonly DateTime sUnixEpoch = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
    public HomeQuickBuyCalendar(DateTime dailyResetTime,int timeZone,DayOfWeek weeklyStartDayOfWeek)
    { DailyResetTime=dailyResetTime; TimeZone=timeZone; WeeklyStartDayOfWeek=weeklyStartDayOfWeek; }
	public int CalcDaysByTimeOfDay(DateTime startTime, DateTime endTime, int hourOfDay, int minuteOfDay, int secondOfDay)
	{
		DateTime dateTime = new DateTime(startTime.Year, startTime.Month, startTime.Day, hourOfDay, minuteOfDay, secondOfDay);
		int num = (int)(new DateTime(endTime.Year, endTime.Month, endTime.Day, hourOfDay, minuteOfDay, secondOfDay) - dateTime).TotalDays;
		if (new TimeSpan(startTime.Hour, startTime.Minute, startTime.Second) < new TimeSpan(hourOfDay, minuteOfDay, secondOfDay))
		{
			num++;
		}
		if (new TimeSpan(endTime.Hour, endTime.Minute, endTime.Second) < new TimeSpan(hourOfDay, minuteOfDay, secondOfDay))
		{
			num--;
		}
		return num;
	}

	public int CalcDays(DateTime t1, DateTime t2)
	{
		(int, int, int) dailyResetTimeOfDay = GetDailyResetTimeOfDay();
		return CalcDaysByTimeOfDay(t1, t2, dailyResetTimeOfDay.Item1, dailyResetTimeOfDay.Item2, dailyResetTimeOfDay.Item3);
	}

	public bool IsSameDay(DateTime t1, DateTime t2)
	{
		return Math.Abs(CalcDays(t1, t2)) < 1;
	}

	public bool IsSameWeek(DateTime t1, DateTime t2)
	{
		return IsSameWeek(t1, t2, WeeklyStartDayOfWeek);
	}

	public bool IsSameWeek(DateTime t1, DateTime t2, DayOfWeek dayOfWeek)
	{
		return GetBeginningOfWeek(t1, dayOfWeek) == GetBeginningOfWeek(t2, dayOfWeek);
	}

	public DateTime GetBeginningOfWeek(DateTime date, DayOfWeek dayOfWeek)
	{
		date = GetDailyResetTimeFromDate(date);
		int num = GetDateTimeWithTimeZone(date).DayOfWeek - dayOfWeek;
		if (num < 0)
		{
			num += 7;
		}
		return date.AddDays(-num);
	}

	public (int hour, int minute, int second) GetDailyResetTimeOfDay()
	{
		DateTime dailyResetTime = DailyResetTime;
		int timeZone = TimeZone;
		int num = (dailyResetTime.Hour - timeZone) % 24;
		if (num < 0)
		{
			num += 24;
		}
		return (hour: num, minute: dailyResetTime.Minute, second: dailyResetTime.Second);
	}

	public DateTime GetDailyResetTimeFromDate(DateTime dateTime)
	{
		if (dateTime < sUnixEpoch)
		{
			dateTime = sUnixEpoch;
		}
		(int, int, int) dailyResetTimeOfDay = GetDailyResetTimeOfDay();
		DateTime result = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dailyResetTimeOfDay.Item1, dailyResetTimeOfDay.Item2, dailyResetTimeOfDay.Item3, DateTimeKind.Utc);
		if (new TimeSpan(dateTime.Hour, dateTime.Minute, dateTime.Second) < new TimeSpan(dailyResetTimeOfDay.Item1, dailyResetTimeOfDay.Item2, dailyResetTimeOfDay.Item3))
		{
			return result.AddDays(-1.0);
		}
		return result;
	}

	public DateTime GetDateTimeWithTimeZone(DateTime dateTime)
	{
		return dateTime.AddHours(TimeZone);
	}

}

}

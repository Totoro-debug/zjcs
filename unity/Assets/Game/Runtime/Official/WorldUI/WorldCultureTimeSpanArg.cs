using System;
using System.Collections.Generic;

namespace Zjcs.Official.WorldUI
{
    public enum WorldTimeSpanFormatType { Normal, Brief, Detail, Short, CeilShort, Adapted }

public sealed class WorldCultureTimeSpanArg : object
{
	public TimeSpan Value { get; }

	public WorldTimeSpanFormatType Format { get; }

	private readonly Func<string, string> localize;
	public WorldCultureTimeSpanArg(TimeSpan value, WorldTimeSpanFormatType format = WorldTimeSpanFormatType.Normal, Func<string, string> language = null)
	{
		localize = language ?? WorldMultiLanguageText.Localize ?? WorldBattleReportTextService.GetText;
		Value = value;
		Format = format;
	}

	private string TextFormatted(string key, params object[] args) => string.Format(localize(key), args);

	public override string ToString()
	{
		return Format switch
		{
			WorldTimeSpanFormatType.Normal => ToNormalString(),
			WorldTimeSpanFormatType.Brief => ToBriefString(),
			WorldTimeSpanFormatType.Detail => ToDetailString(),
			WorldTimeSpanFormatType.Short => ToShortString(isFloor: true),
			WorldTimeSpanFormatType.CeilShort => ToShortString(isFloor: false),
			WorldTimeSpanFormatType.Adapted => ToAdaptedString(),
			_ => ToNormalString(),
		};
	}

	private string ToNormalString()
	{
		int num = (int)Value.TotalDays;
		double totalHours = Value.TotalHours;
		if (num >= 2)
		{
			if (totalHours <= 0.0)
			{
				return TextFormatted("TwoDayCountdown", num);
			}
			return TextFormatted("TwoDayCountdown2", num, Value.Hours);
		}
		if (num >= 1)
		{
			if (totalHours <= 0.0)
			{
				return TextFormatted("DayCountdown", num);
			}
			return TextFormatted("DayCountdown2", num, Value.Hours);
		}
		if (totalHours >= 1.0)
		{
			return Value.ToString("hh':'mm':'ss");
		}
		return Value.ToString("mm':'ss");
	}

	private string ToBriefString()
	{
		double totalDays = Value.TotalDays;
		double totalHours = Value.TotalHours;
		if (totalDays >= 1.0)
		{
			if (totalHours <= 0.0)
			{
				return TextFormatted("DayCountdown", (int)totalDays);
			}
			return TextFormatted("DayCountdown2", (int)totalDays, Value.Hours);
		}
		if (totalHours >= 1.0)
		{
			return TextFormatted("time_hour", (int)totalHours);
		}
		double totalMinutes = Value.TotalMinutes;
		if (totalMinutes >= 1.0)
		{
			return TextFormatted("time_minute", (int)totalMinutes);
		}
		double totalSeconds = Value.TotalSeconds;
		if (totalSeconds > 0.0)
		{
			return TextFormatted("time_second", (int)Math.Ceiling(totalSeconds));
		}
		return TextFormatted("time_end");
	}

	private string ToDetailString()
	{
		double totalDays = Value.TotalDays;
		double totalHours = Value.TotalHours;
		double totalMinutes = Value.TotalMinutes;
		string text;
		if (totalDays >= 1.0)
		{
			text = "DayCountdownDetail";
		}
		else if (totalHours >= 1.0)
		{
			text = "HourCountdownDetail";
		}
		else
		{
			text = ((!(totalMinutes >= 1.0)) ? "SecondCountdownDetail" : "MinutesCountdownDetail");
		}
		string text2 = localize(text);
		if (text2 == text)
		{
			return text;
		}
		return Value.ToString(text2);
	}

	private string ToShortString(bool isFloor)
	{
		double totalDays = Value.TotalDays;
		if (totalDays >= 1.0)
		{
			return TextFormatted("DayCountdown", isFloor ? ((long)totalDays) : ((long)Math.Ceiling(totalDays)));
		}
		double totalHours = Value.TotalHours;
		if (totalHours >= 1.0)
		{
			return TextFormatted("time_hour", isFloor ? ((long)totalHours) : ((long)Math.Ceiling(totalHours)));
		}
		double totalMinutes = Value.TotalMinutes;
		if (totalMinutes >= 1.0)
		{
			return TextFormatted("time_minute", isFloor ? ((long)totalMinutes) : ((long)Math.Ceiling(totalMinutes)));
		}
		double totalSeconds = Value.TotalSeconds;
		if (totalSeconds > 0.0)
		{
			return TextFormatted("time_second", (long)Math.Ceiling(totalSeconds));
		}
		return TextFormatted("time_end");
	}

	private string ToAdaptedString()
	{
		List<string> list = new List<string>();
		int days = Value.Days;
		if (days != 0)
		{
			list.Add(TextFormatted("time_day", days));
		}
		int hours = Value.Hours;
		if (hours != 0)
		{
			list.Add(TextFormatted("time_hour", hours));
		}
		int minutes = Value.Minutes;
		if (minutes != 0)
		{
			list.Add(TextFormatted("time_minute_abbreviation", minutes));
		}
		int seconds = Value.Seconds;
		if (seconds != 0)
		{
			list.Add(TextFormatted("time_second", seconds));
		}
		if (list.Count == 0)
		{
			list.Add(TextFormatted("time_second", 0));
		}
		return string.Join(string.Empty, list);
	}
}
}

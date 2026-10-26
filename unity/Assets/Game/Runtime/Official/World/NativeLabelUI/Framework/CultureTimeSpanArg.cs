// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;
using System.Collections.Generic;


public sealed class CultureTimeSpanArg : ArgBase
{
	public TimeSpan Value { get; }

	public ETimeSpanFormatType Format { get; }

	public CultureTimeSpanArg(TimeSpan value, ETimeSpanFormatType format = ETimeSpanFormatType.Normal)
	{
		Value = value;
		Format = format;
	}

	public override string ToString()
	{
		return Format switch
		{
			ETimeSpanFormatType.Normal => ToNormalString(), 
			ETimeSpanFormatType.Brief => ToBriefString(), 
			ETimeSpanFormatType.Detail => ToDetailString(), 
			ETimeSpanFormatType.Short => ToShortString(isFloor: true), 
			ETimeSpanFormatType.CeilShort => ToShortString(isFloor: false), 
			ETimeSpanFormatType.Adapted => ToAdaptedString(), 
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
				return Culture.Text.GetTextFormatted("TwoDayCountdown", num);
			}
			return Culture.Text.GetTextFormatted("TwoDayCountdown2", num, Value.Hours);
		}
		if (num >= 1)
		{
			if (totalHours <= 0.0)
			{
				return Culture.Text.GetTextFormatted("DayCountdown", num);
			}
			return Culture.Text.GetTextFormatted("DayCountdown2", num, Value.Hours);
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
				return Culture.Text.GetTextFormatted("DayCountdown", (int)totalDays);
			}
			return Culture.Text.GetTextFormatted("DayCountdown2", (int)totalDays, Value.Hours);
		}
		if (totalHours >= 1.0)
		{
			return Culture.Text.GetTextFormatted("time_hour", (int)totalHours);
		}
		double totalMinutes = Value.TotalMinutes;
		if (totalMinutes >= 1.0)
		{
			return Culture.Text.GetTextFormatted("time_minute", (int)totalMinutes);
		}
		double totalSeconds = Value.TotalSeconds;
		if (totalSeconds > 0.0)
		{
			return Culture.Text.GetTextFormatted("time_second", (int)Math.Ceiling(totalSeconds));
		}
		return Culture.Text.GetTextFormatted("time_end");
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
		string text2 = Culture.Text.GetText(text);
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
			return Culture.Text.GetTextFormatted("DayCountdown", isFloor ? ((long)totalDays) : ((long)Math.Ceiling(totalDays)));
		}
		double totalHours = Value.TotalHours;
		if (totalHours >= 1.0)
		{
			return Culture.Text.GetTextFormatted("time_hour", isFloor ? ((long)totalHours) : ((long)Math.Ceiling(totalHours)));
		}
		double totalMinutes = Value.TotalMinutes;
		if (totalMinutes >= 1.0)
		{
			return Culture.Text.GetTextFormatted("time_minute", isFloor ? ((long)totalMinutes) : ((long)Math.Ceiling(totalMinutes)));
		}
		double totalSeconds = Value.TotalSeconds;
		if (totalSeconds > 0.0)
		{
			return Culture.Text.GetTextFormatted("time_second", (long)Math.Ceiling(totalSeconds));
		}
		return Culture.Text.GetTextFormatted("time_end");
	}

	private string ToAdaptedString()
	{
		List<string> list = new List<string>();
		int days = Value.Days;
		if (days != 0)
		{
			list.Add(Culture.Text.GetTextFormatted("time_day", days));
		}
		int hours = Value.Hours;
		if (hours != 0)
		{
			list.Add(Culture.Text.GetTextFormatted("time_hour", hours));
		}
		int minutes = Value.Minutes;
		if (minutes != 0)
		{
			list.Add(Culture.Text.GetTextFormatted("time_minute_abbreviation", minutes));
		}
		int seconds = Value.Seconds;
		if (seconds != 0)
		{
			list.Add(Culture.Text.GetTextFormatted("time_second", seconds));
		}
		if (list.Count == 0)
		{
			list.Add(Culture.Text.GetTextFormatted("time_second", 0));
		}
		return string.Join(string.Empty, list);
	}
}

}

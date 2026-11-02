using System;
using System.Collections.Generic;

namespace Zjcs.Official.Partners
{
    public sealed class PartnerConversationDayLimitRecord
    {
        public long NpcId;
        public int DayShowCount;
        public DateTime LastShowTime;
    }

    // Direct Player day-limit methods and TimeUtils reset arithmetic. The
    // caller supplies the original configuration, UTC clock and server zone.
    public sealed class PartnerConversationDayLimitRules
    {
        private readonly IPartnerConversationConfiguration configuration;
        private readonly IDictionary<long, PartnerConversationDayLimitRecord> records;
        private readonly Func<DateTime> now;
        private readonly int limit;
        private readonly DateTime resetTime;
        private readonly int timeZone;
        private static readonly DateTime sUnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public PartnerConversationDayLimitRules(IPartnerConversationConfiguration configuration,
            IDictionary<long, PartnerConversationDayLimitRecord> records, Func<DateTime> now,
            int limit, DateTime resetTime, int timeZone)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.records = records ?? throw new ArgumentNullException(nameof(records));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.limit = limit; this.resetTime = resetTime; this.timeZone = timeZone;
        }

    public bool CheckNpcDialogueHasDayLimit(long npcId, int dialogueId)
    {
        PartnerConversationNode npcDialogueInfo = configuration.Get(dialogueId, 1);
        if (npcDialogueInfo == null)
        {
            return true;
        }
        if (!npcDialogueInfo.IsDayLimit)
        {
            return false;
        }
        if (!records.TryGetValue(npcId, out var value))
        {
            return false;
        }
        if (now() >= GetDailyResetTimeFromDateWithOffset(value.LastShowTime, 1))
        {
            return false;
        }
        if (value.DayShowCount < limit)
        {
            return false;
        }
        return true;
    }

    public void RecordNpcDialogueDayLimit(long npcId, int dialogueId)
    {
        PartnerConversationNode npcDialogueInfo = configuration.Get(dialogueId, 1);
        if (npcDialogueInfo == null)
        {
        }
        else if (npcDialogueInfo.IsDayLimit)
        {
            if (!records.TryGetValue(npcId, out var value))
            {
                value = new PartnerConversationDayLimitRecord();
                value.NpcId = npcId;
                records.Add(npcId, value);
            }
            if (now() >= GetDailyResetTimeFromDateWithOffset(value.LastShowTime, 1))
            {
                value.DayShowCount = 0;
            }
            value.DayShowCount++;
            value.LastShowTime = now();
        }
    }

    public (int hour, int minute, int second) GetDailyResetTimeOfDay()
    {
        DateTime dailyResetTime = resetTime;

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

    public DateTime GetDailyResetTimeFromDateWithOffset(DateTime dateTime, int days)
    {
        return GetDailyResetTimeFromDate(dateTime).AddDays(days);
    }
    }
}

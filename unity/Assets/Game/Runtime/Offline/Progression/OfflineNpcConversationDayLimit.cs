using System;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineNpcConversationDayLimit
    {
        public long NpcId;
        public int DayShowCount;
        public long LastShowTimeTicks;
        public int ClockKind;

        public static bool IsValid(OfflineNpcConversationDayLimit value) => value != null
            && value.NpcId > 0 && value.DayShowCount >= 0
            && value.LastShowTimeTicks >= 0 && value.LastShowTimeTicks <= DateTime.MaxValue.Ticks
            && value.ClockKind >= (int)DateTimeKind.Unspecified && value.ClockKind <= (int)DateTimeKind.Local;
        public PartnerConversationDayLimitRecord ToRecord()
        {
            if (!IsValid(this)) throw new InvalidOperationException("Invalid saved conversation day limit.");
            return new PartnerConversationDayLimitRecord { NpcId = NpcId, DayShowCount = DayShowCount,
                LastShowTime = new DateTime(LastShowTimeTicks, (DateTimeKind)ClockKind) };
        }
        public static OfflineNpcConversationDayLimit FromRecord(PartnerConversationDayLimitRecord value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var result = new OfflineNpcConversationDayLimit { NpcId = value.NpcId, DayShowCount = value.DayShowCount,
                LastShowTimeTicks = value.LastShowTime.Ticks, ClockKind = (int)value.LastShowTime.Kind };
            if (!IsValid(result)) throw new ArgumentException("Invalid original conversation day limit.", nameof(value));
            return result;
        }
    }

    // Bind to the cloned character inside ApplyCharacterMutation. This class
    // never commits independently of conversation creation/history/rewards.
    public sealed class OfflinePartnerConversationDayLimits
    {
        private readonly OfflineNpcConversationProgress progress;
        private readonly System.Collections.Generic.Dictionary<long, PartnerConversationDayLimitRecord> records;
        private readonly PartnerConversationDayLimitRules rules;
        public OfflinePartnerConversationDayLimits(OfflineNpcConversationProgress progress,
            IPartnerConversationConfiguration configuration, Func<DateTime> utcClock,
            int configuredLimit, DateTime configuredResetTime, int serverTimeZoneHours)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            if (!OfflineNpcConversationProgress.IsValid(progress)) throw new ArgumentException("Invalid conversation progress.", nameof(progress));
            records = progress.DayLimits.ToDictionary(value => value.NpcId, value => value.ToRecord());
            rules = new PartnerConversationDayLimitRules(configuration, records, utcClock,
                configuredLimit, configuredResetTime, serverTimeZoneHours);
        }
        public bool CheckNpcDialogueHasDayLimit(long npcId, int dialogueId) => rules.CheckNpcDialogueHasDayLimit(npcId, dialogueId);
        public void RecordNpcDialogueDayLimit(long npcId, int dialogueId)
        {
            rules.RecordNpcDialogueDayLimit(npcId, dialogueId);
            progress.DayLimits = records.Values.Select(OfflineNpcConversationDayLimit.FromRecord).ToList();
        }
    }
}

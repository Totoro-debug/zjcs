using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineNpcConversationProgress
    {
        public List<OfflineNpcConversation> Records = new List<OfflineNpcConversation>();
        public List<OfflineNpcConversationDayLimit> DayLimits = new List<OfflineNpcConversationDayLimit>();

        public static void Normalize(OfflineNpcConversationProgress progress)
        {
            if (progress.DayLimits == null) progress.DayLimits = new List<OfflineNpcConversationDayLimit>();
        }

        public static bool IsValid(OfflineNpcConversationProgress progress)
        {
            return progress != null && progress.Records != null
                && progress.Records.All(OfflineNpcConversation.IsValid)
                && progress.Records.Select(value => value.DialogueId).Distinct().Count() == progress.Records.Count
                && progress.DayLimits != null && progress.DayLimits.All(OfflineNpcConversationDayLimit.IsValid)
                && progress.DayLimits.Select(value => value.NpcId).Distinct().Count() == progress.DayLimits.Count;
        }
    }

    // JsonUtility stores fields rather than DateTime. Keep ticks and Kind so
    // loading never reinterprets the original clock value in a different zone.
    [Serializable]
    public sealed class OfflineNpcConversation
    {
        public long PlayerId, NpcId, DialogueId;
        public int NpcClassId, DialogueClassId, CurrentNode;
        public List<int> DialogueNodes = new List<int>();
        public PartnerConversationStatus DialogueStatus;
        public long DateTimeTicks;
        public int ClockKind;

        public static OfflineNpcConversation FromRecord(int npcClassId, PartnerConversationRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var value = new OfflineNpcConversation {
                PlayerId = record.PlayerId, NpcId = record.NpcId, DialogueId = record.DialogueId,
                NpcClassId = npcClassId, DialogueClassId = record.DialogueClassId,
                CurrentNode = record.CurrentNode, DialogueStatus = record.DialogueStatus,
                DateTimeTicks = record.DateTime.Ticks, ClockKind = (int)record.DateTime.Kind,
                DialogueNodes = record.DialogueNodes == null ? null : new List<int>(record.DialogueNodes)
            };
            if (!IsValid(value)) throw new ArgumentException("Invalid original conversation record.", nameof(record));
            return value;
        }

        public PartnerConversationRecord ToRecord()
        {
            if (!IsValid(this)) throw new InvalidOperationException("Invalid saved conversation record.");
            return new PartnerConversationRecord {
                PlayerId = PlayerId, NpcId = NpcId, DialogueId = DialogueId,
                DialogueClassId = DialogueClassId, CurrentNode = CurrentNode, DialogueStatus = DialogueStatus,
                DateTime = new DateTime(DateTimeTicks, (DateTimeKind)ClockKind),
                DialogueNodes = new List<int>(DialogueNodes)
            };
        }

        public static bool IsValid(OfflineNpcConversation value)
        {
            return value != null && value.PlayerId > 0 && value.NpcId > 0 && value.DialogueId > 0
                && value.NpcClassId > 0 && value.DialogueClassId > 0 && value.CurrentNode > 0
                && value.DialogueStatus >= PartnerConversationStatus.InProgress
                && value.DialogueStatus <= PartnerConversationStatus.End
                && value.DateTimeTicks >= 0 && value.DateTimeTicks <= DateTime.MaxValue.Ticks
                && value.ClockKind >= (int)DateTimeKind.Unspecified && value.ClockKind <= (int)DateTimeKind.Local
                && value.DialogueNodes != null && value.DialogueNodes.Count > 0
                && value.DialogueNodes.All(node => node > 0)
                && value.DialogueNodes[value.DialogueNodes.Count - 1] == value.CurrentNode;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineTreasureSlotProgress
    {
        // Zero is the old per-item representation; one owns all slot state below.
        public int Format, SeasonId, ActivePlanIndex;
        public bool UpgradeUseCommonMaterailEnable, AwakeAutoMergeEnable;
        public List<OfflineTreasureElementSlots> Elements = new List<OfflineTreasureElementSlots>();
        public List<OfflineTreasureSlotPlan> Plans = new List<OfflineTreasureSlotPlan>();
        public int BlessRecordFormat;
        public List<OfflineTreasureSlotBlessRecord> BlessRecords = new List<OfflineTreasureSlotBlessRecord>();

        public static bool IsValid(OfflineTreasureSlotProgress data)
        {
            if (data == null || data.Format < 0 || data.Format > 1 || data.SeasonId < 0
                || data.ActivePlanIndex < 0 || data.ActivePlanIndex >= 32 || data.Elements == null
                || data.Elements.Count > 16 || data.Plans == null || data.Plans.Count > 32
                || data.BlessRecordFormat < 0 || data.BlessRecordFormat > 1 || data.BlessRecords == null
                || data.BlessRecords.Count > 100 || data.BlessRecordFormat == 0 && data.BlessRecords.Count != 0
                || data.BlessRecords.Any(record => record == null || record.SeasonId <= 0 || record.Elements == null
                    || record.Elements.Count > 16 || record.Elements.Any(element => element == null || element.ElementType < 0
                        || element.Slots == null || element.Slots.Count > 32 || element.Slots.Any(slot => slot == null
                            || slot.Index < 0 || slot.Index >= 32 || slot.Level < 0)
                        || element.Slots.Select(slot => slot.Index).Distinct().Count() != element.Slots.Count)
                    || record.Elements.Select(element => element.ElementType).Distinct().Count() != record.Elements.Count)
                || data.BlessRecords.Select(record => record.SeasonId).Distinct().Count() != data.BlessRecords.Count) return false;
            if (data.Format == 0) return data.Elements.Count == 0 && data.Plans.Count == 0
                && data.SeasonId == 0 && data.ActivePlanIndex == 0 && !data.UpgradeUseCommonMaterailEnable && !data.AwakeAutoMergeEnable
                && data.BlessRecordFormat == 0;
            if (data.Elements.Any(row => row == null || row.ElementType < 0 || row.ShareLevel < 1
                || row.BaseShareLevel < 1 || row.ShareLevel < row.BaseShareLevel || row.Slots == null
                || row.Slots.Count > 32 || row.Slots.Any(slot => slot == null || slot.Index < 0 || slot.Index >= 32
                    || slot.TreasureClassId < 0 || slot.BaseLevel < 1 || slot.BlessLevel < 0
                    || (long)slot.BaseLevel + slot.BlessLevel > int.MaxValue)
                || row.Slots.Select(slot => slot.Index).Distinct().Count() != row.Slots.Count)
                || data.Elements.Select(row => row.ElementType).Distinct().Count() != data.Elements.Count) return false;
            var equipped = data.Elements.SelectMany(row => row.Slots).Where(row => row.TreasureClassId != 0).Select(row => row.TreasureClassId).ToArray();
            if (equipped.Distinct().Count() != equipped.Length) return false;
            return data.Plans.All(plan => plan != null && plan.Index >= 0 && plan.Index < 32 && plan.Elements != null
                    && plan.Elements.Count <= 16 && plan.Elements.All(element => element != null
                        && element.ElementType >= 0 && element.Slots != null && element.Slots.Count <= 32
                        && element.Slots.All(slot => slot != null && slot.Index >= 0 && slot.Index < 32 && slot.TreasureClassId > 0)
                        && element.Slots.Select(slot => slot.Index).Distinct().Count() == element.Slots.Count)
                    && plan.Elements.Select(element => element.ElementType).Distinct().Count() == plan.Elements.Count)
                && data.Plans.Select(plan => plan.Index).Distinct().Count() == data.Plans.Count;
        }
    }
    [Serializable] public sealed class OfflineTreasureElementSlots
    {
        public int ElementType, ShareLevel = 1, BaseShareLevel = 1;
        public List<OfflineTreasureSlot> Slots = new List<OfflineTreasureSlot>();
    }
    [Serializable] public sealed class OfflineTreasureSlot
    {
        public int Index, TreasureClassId, BaseLevel = 1, BlessLevel;
    }
    [Serializable] public sealed class OfflineTreasureSlotPlan
    {
        public int Index;
        public List<OfflineTreasurePlanElement> Elements = new List<OfflineTreasurePlanElement>();
    }
    [Serializable] public sealed class OfflineTreasurePlanElement
    {
        public int ElementType;
        public List<OfflineTreasurePlanSlot> Slots = new List<OfflineTreasurePlanSlot>();
    }
    [Serializable] public sealed class OfflineTreasurePlanSlot { public int Index, TreasureClassId; }
    [Serializable] public sealed class OfflineTreasureSlotBlessRecord
    {
        public int SeasonId;
        public List<OfflineTreasureSlotBlessElement> Elements = new List<OfflineTreasureSlotBlessElement>();
    }
    [Serializable] public sealed class OfflineTreasureSlotBlessElement
    {
        public int ElementType;
        public List<OfflineTreasureSlotBlessLevel> Slots = new List<OfflineTreasureSlotBlessLevel>();
    }
    [Serializable] public sealed class OfflineTreasureSlotBlessLevel { public int Index, Level; }
}

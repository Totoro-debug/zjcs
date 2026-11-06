using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class DailyDungeonSettlementItem
    {
        public int ItemId;
        public long Count;
    }

    [Serializable]
    public sealed class DailyDungeonSettlementRecord
    {
        public string Receipt;
        public List<DailyDungeonSettlementItem> Items = new List<DailyDungeonSettlementItem>();
        public List<long> EquipmentInstanceIds = new List<long>();
    }

}
